using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.MasterData;
using UnityEngine;

namespace StickerFwk.Infrastructure.MasterData
{
    public class MasterDataRepository : IMasterDataRepository, IDisposable
    {
        private const string MasterDataLabel = "MasterData";

        private readonly IAssetRequester _assetRequester;
        private readonly Dictionary<Type, IReadOnlyList<IMasterData>> _tables = new();
        private readonly Dictionary<Type, Dictionary<string, IMasterData>> _indices = new();

        private IPreloadHandle _preloadHandle;
        private bool _disposed;

        public MasterDataRepository(IAssetRequester assetRequester)
        {
            _assetRequester = assetRequester;
        }

        public bool IsLoaded => _preloadHandle != null;

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MasterDataRepository));
            }

            if (IsLoaded)
            {
                Log.Warning("MasterDataRepository is already loaded. Skipping.");
                return;
            }

            _preloadHandle = await _assetRequester.PreloadFromLabel<ScriptableObject>(MasterDataLabel, ct);

            foreach (var key in _preloadHandle.Keys)
            {
                var so = _assetRequester.GetAssetImmediate<ScriptableObject>(key);
                if (so is not IMasterDataScriptableObject masterDataSo)
                {
                    Log.Warning($"Asset '{key}' is not an IMasterDataScriptableObject. Skipping.");
                    continue;
                }

                var type = masterDataSo.Type;
                var data = masterDataSo.Data;

                _tables[type] = data;

                var index = new Dictionary<string, IMasterData>();
                foreach (var entry in data)
                {
                    if (!index.TryAdd(entry.Id, entry))
                    {
                        Log.Warning($"Duplicate master data id '{entry.Id}' in {type.Name}. Skipping duplicate.");
                    }
                }

                _indices[type] = index;
                Log.Info($"Loaded master data: {type.Name} ({data.Count} entries)");
            }

            Log.Info($"MasterDataRepository loaded {_tables.Count} table(s).");
        }

        public IReadOnlyList<T> GetAll<T>() where T : class, IMasterData
        {
            ThrowIfNotLoaded();

            if (_tables.TryGetValue(typeof(T), out var data))
            {
                return data as IReadOnlyList<T>;
            }

            Log.Warning($"No master data found for type {typeof(T).Name}.");
            return Array.Empty<T>();
        }

        public T Get<T>(string id) where T : class, IMasterData
        {
            if (TryGet<T>(id, out var result))
            {
                return result;
            }

            throw new KeyNotFoundException(
                $"Master data entry not found: type={typeof(T).Name}, id='{id}'"
            );
        }

        public bool TryGet<T>(string id, out T data) where T : class, IMasterData
        {
            ThrowIfNotLoaded();

            if (_indices.TryGetValue(typeof(T), out var index) &&
                index.TryGetValue(id, out var entry))
            {
                data = entry as T;
                return data != null;
            }

            data = null;
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _preloadHandle?.Dispose();
            _preloadHandle = null;
            _tables.Clear();
            _indices.Clear();
        }

        private void ThrowIfNotLoaded()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MasterDataRepository));
            }

            if (!IsLoaded)
            {
                throw new InvalidOperationException(
                    "MasterDataRepository has not been loaded. Call LoadAsync() first."
                );
            }
        }
    }
}
