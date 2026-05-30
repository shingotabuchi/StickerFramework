using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.LocalDataSave;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class LocalDataSaveService : ILocalDataSaveService
    {
        private const string Tag = "LocalDataSave";

        private readonly ILocalDataSave _localDataSave;
        private readonly ILocalDataSaveSerializer _serializer;

        public LocalDataSaveService(
            ILocalDataSave localDataSave,
            ILocalDataSaveSerializer serializer)
        {
            _localDataSave = localDataSave;
            _serializer = serializer;
        }

        public async UniTask<T> LoadAsync<T>(LocalDataSaveKey key, CancellationToken ct)
            where T : class, new()
        {
            var bytes = await _localDataSave.ReadAsync(key, ct);
            if (bytes == null || bytes.Length == 0)
            {
                return new T();
            }

            try
            {
                var value = _serializer.Deserialize<T>(bytes);
                return value ?? new T();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"Failed to load local data '{key}'; using defaults. {ex}");
                return new T();
            }
        }

        public async UniTask SaveAsync<T>(LocalDataSaveKey key, T data, CancellationToken ct)
            where T : class, new()
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var bytes = _serializer.Serialize(data);
            await _localDataSave.WriteAsync(key, bytes, ct);
        }

        public UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct)
        {
            return _localDataSave.DeleteAsync(key, ct);
        }
    }
}
