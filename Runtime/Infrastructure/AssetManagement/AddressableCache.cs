using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using Object = UnityEngine.Object;

namespace StickerFwk.Infrastructure.AssetManagement
{
    internal class AssetHandle<T> : IAssetHandle<T> where T : Object
    {
        private readonly AddressableCache _cache;
        private readonly string _key;
        private bool _disposed;

        public AssetHandle(T asset, string key, AddressableCache cache)
        {
            Asset = asset;
            _key = key;
            _cache = cache;
        }

        public T Asset { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cache?.Release(_key);
        }
    }

    internal class PreloadHandle : IAssetHandle
    {
        private readonly AddressableCache _cache;
        private readonly IEnumerable<string> _keys;
        private bool _disposed;

        public PreloadHandle(IEnumerable<string> keys, AddressableCache cache)
        {
            _keys = keys;
            _cache = cache;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cache?.Release(_keys);
        }
    }

    internal class PreloadFromLabelHandle : IPreloadHandle
    {
        private readonly AddressableCache _cache;
        private bool _disposed;

        public PreloadFromLabelHandle(IList<string> keys, AddressableCache cache)
        {
            Keys = keys;
            _cache = cache;
        }

        public IList<string> Keys { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cache?.Release(Keys);
        }
    }

    public class AddressableCache : IDisposable, IAssetRequester
    {
        private readonly KeyedOperationGate<string> _loadingGate = new();
        private CancellationTokenSource _disposeCts = new();
        private Dictionary<string, IAddressableHandle> _handles = new();
        private bool _isDisposed;
        private Dictionary<string, int> _refCounts = new();

        public async UniTask<IAssetHandle<T>> RequestAsset<T>(
            string key,
            CancellationToken cancellationToken = default
        ) where T : Object
        {
            AddRef(key);
            try
            {
                var asset = await LoadAsync<T>(key, cancellationToken);
                return new AssetHandle<T>(asset, key, this);
            }
            catch
            {
                Release(key);
                throw;
            }
        }

        public T GetAssetImmediate<T>(string key) where T : Object
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AddressableCache));

            if (TryGetHandle(key, out var handle)) return handle.Object as T;

            Log.Warning($"No handle found for key '{key}'. Returning null.");
            return null;
        }

        public async UniTask<IAssetHandle> Preload<T>(
            IEnumerable<string> keys,
            CancellationToken cancellationToken = default,
            IProgress<float> progress = null
        ) where T : Object
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AddressableCache));

            var keysList = new List<string>(keys);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            using var _ = PoolScope.List<UniTask>(out var tasks);
            foreach (var key in keysList)
            {
                AddRef(key);
                if (TryGetHandle(key, out var handle)) continue;

                tasks.Add(LoadAsync<T>(key, linkedCts.Token, progress));
            }

            try
            {
                await UniTask.WhenAll(tasks);
                return new PreloadHandle(keysList, this);
            }
            catch
            {
                Release(keysList);
                throw;
            }
        }

        public async UniTask<IPreloadHandle> PreloadFromLabel<T>(
            string assetLabel,
            CancellationToken token = default,
            IProgress<float> progress = null) where T : Object
        {
            var keys = await AddressableManager.GetKeysByLabel(assetLabel, cancellationToken: token);
            await PreloadInternal<T>(keys, token, progress);
            return new PreloadFromLabelHandle(keys, this);
        }

        public bool IsLoaded(IEnumerable<string> keys)
        {
            foreach (var key in keys)
                if (!TryGetHandle(key, out var handle))
                    return false;

            return true;
        }

        public bool IsLoaded(string key)
        {
            return TryGetHandle(key, out _);
        }

        public void Release(IEnumerable<string> keys)
        {
            foreach (var key in keys) Release(key);
        }

        public async UniTask ReleaseFromLabel(string assetLabel, CancellationToken cancellationToken = default)
        {
            var keysToRelease =
                await AddressableManager.GetKeysByLabel(assetLabel, cancellationToken: cancellationToken);
            Release(keysToRelease);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            _disposeCts?.Cancel();
            _disposeCts?.Dispose();
            _disposeCts = null;

            ReleaseAll();
            _handles = null;

            _refCounts.Clear();
            _refCounts = null;

            _loadingGate.CancelAll();
        }

        private async UniTask PreloadInternal<T>(
            IEnumerable<string> keys,
            CancellationToken cancellationToken = default,
            IProgress<float> progress = null
        ) where T : Object
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AddressableCache));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            using var __ = PoolScope.List<UniTask>(out var tasks);
            using var ___ = PoolScope.HashSet<string>(out var keySet);
            foreach (var key in keys)
            {
                keySet.Add(key);
            }

            foreach (var key in keySet)
            {
                AddRef(key);
                if (TryGetHandle(key, out var handle)) continue;

                tasks.Add(LoadAsync<T>(key, linkedCts.Token, progress));
            }

            try
            {
                await UniTask.WhenAll(tasks);
            }
            catch
            {
                Release(keySet);
                throw;
            }
        }

        public async UniTask<T> LoadAsync<T>(
            string key,
            CancellationToken cancellationToken = default,
            IProgress<float> progress = null
        ) where T : Object
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AddressableCache));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);

            Log.Info($"Start Loading asset with key '{key}'...");
            var retryCount = 0;
            while (true)
            {
                Log.Info($"Loading asset with key '{key}'...");
                if (_isDisposed) throw new ObjectDisposedException(nameof(AddressableCache));

                if (TryGetHandle(key, out var handle)) return handle.Object as T;

                try
                {
                    await _loadingGate.WaitOrRun(key, LoadAsyncInternal, linkedCts.Token);
                }
                catch (Exception ex)
                {
                    Log.Error($"Existing loading task for key '{key}' failed: {ex}");
                    throw;
                }

                if (TryGetHandle(key, out var loadedHandle)) return loadedHandle.Object as T;

                retryCount++;
                if (retryCount <= 3)
                {
                    Log.Warning(
                        $"Loaded task completed but handle missing for key '{key}'. Retrying (attempt {retryCount}/3)...");
                    await UniTask.Yield();
                    continue;
                }

                Log.Error($"Failed to load asset of key '{key}' after loading task completed.");
                throw new Exception($"Failed to load asset of key '{key}'.");
            }

            async UniTask LoadAsyncInternal()
            {
                try
                {
                    var newHandle = await AddressableManager.LoadAsync<T>(key, progress, linkedCts.Token);

                    if (!newHandle.Succeeded)
                    {
                        Log.Error($"Failed to load asset of key '{key}'.");
                        throw new Exception($"Failed to load asset of key '{key}'.");
                    }

                    if (TryGetHandle(key, out _))
                    {
                        Log.Warning($"Key '{key}' already exists in the cache. Releasing the new handle.");
                        newHandle.Release();
                    }
                    else
                    {
                        Log.Info($"Successfully loaded asset with key '{key}'. Caching handle.");
                        _handles[key] = newHandle;
                    }
                }
                catch (OperationCanceledException)
                {
                    TryReleaseHandle(key);
                    Log.Info($"Loading asset with key '{key}' was canceled.");
                    throw;
                }
                catch (Exception ex)
                {
                    TryReleaseHandle(key);
                    Log.Error($"Failed to load asset of key '{key}': {ex}");
                    throw;
                }
            }
        }

        private bool TryGetHandle(string key, out IAddressableHandle handle)
        {
            if (_handles == null)
            {
                handle = null;
                return false;
            }

            if (_handles.TryGetValue(key, out var boxedHandle))
            {
                handle = boxedHandle;
                return true;
            }

            handle = null;
            return false;
        }

        private bool TryReleaseHandle(string key)
        {
            if (TryGetHandle(key, out var handle))
            {
                handle.Release();
                _handles.Remove(key);
                return true;
            }

            Log.Warning($"No handle found for key '{key}'. Cannot release.");
            return false;
        }

        public void Release(string key)
        {
            Log.Info($"Releasing asset with key '{key}'...");
            if (!TryReleaseRef(key)) Log.Warning($"No handle found for key '{key}'.");
        }

        public void ReleaseAll()
        {
            foreach (var handle in _handles.Values) handle.Release();
            _handles.Clear();
            _refCounts.Clear();
        }

        private void AddRef(string key)
        {
            if (_refCounts == null) return;

            if (_refCounts.TryGetValue(key, out var count))
                _refCounts[key] = count + 1;
            else
                _refCounts[key] = 1;
        }

        private bool TryReleaseRef(string key)
        {
            if (_refCounts != null && _refCounts.TryGetValue(key, out var count))
            {
                count = Math.Max(0, count - 1);
                if (count > 0)
                {
                    _refCounts[key] = count;
                    return true;
                }

                _refCounts.Remove(key);
            }

            return TryReleaseHandle(key);
        }
    }
}
