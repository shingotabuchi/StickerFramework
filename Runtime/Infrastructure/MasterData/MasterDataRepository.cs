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
        private readonly Dictionary<Type, List<IMasterData>> _tables = new();
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

            var preloadHandle = await _assetRequester.PreloadFromLabel<ScriptableObject>(MasterDataLabel, ct);
            var tables = new Dictionary<Type, List<IMasterData>>();
            var indices = new Dictionary<Type, Dictionary<string, IMasterData>>();

            try
            {
                foreach (var key in preloadHandle.Keys)
                {
                    var so = _assetRequester.GetAssetImmediate<ScriptableObject>(key);
                    if (so is not IMasterDataScriptableObject masterDataSo)
                    {
                        var actualType = so != null ? so.GetType().Name : "null";
                        throw new InvalidOperationException(
                            $"Asset '{key}' loaded under label '{MasterDataLabel}' is not an {nameof(IMasterDataScriptableObject)} (actual type: {actualType}). " +
                            "All assets tagged with the MasterData label must implement IMasterDataScriptableObject.");
                    }

                    var type = masterDataSo.Type;
                    var data = masterDataSo.Data;

                    if (!tables.TryGetValue(type, out var table))
                    {
                        table = new List<IMasterData>();
                        tables.Add(type, table);
                    }

                    if (!indices.TryGetValue(type, out var index))
                    {
                        index = new Dictionary<string, IMasterData>();
                        indices.Add(type, index);
                    }

                    foreach (var entry in data)
                    {
                        if (!index.TryAdd(entry.Id, entry))
                        {
                            throw new InvalidOperationException(
                                $"Duplicate master data id '{entry.Id}' detected for type {type.Name} while loading asset '{key}'. " +
                                "Master data ids must be unique within a type.");
                        }

                        table.Add(entry);
                    }

                    Log.Info($"Loaded master data asset: {key} ({type.Name}, {data.Count} entries)");
                }
            }
            catch
            {
                preloadHandle.Dispose();
                throw;
            }

            _preloadHandle = preloadHandle;
            foreach (var pair in tables)
            {
                _tables.Add(pair.Key, pair.Value);
            }

            foreach (var pair in indices)
            {
                _indices.Add(pair.Key, pair.Value);
            }

            Log.Info($"MasterDataRepository loaded {_tables.Count} table(s).");
        }

        public IReadOnlyList<T> GetAll<T>() where T : class, IMasterData
        {
            ThrowIfNotLoaded();

            if (_tables.TryGetValue(typeof(T), out var data))
            {
                var result = new T[data.Count];
                for (var i = 0; i < data.Count; i++)
                {
                    result[i] = data[i] as T;
                }

                return result;
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
