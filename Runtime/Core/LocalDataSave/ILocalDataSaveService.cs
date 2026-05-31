using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveService
    {
        UniTask<T> LoadAsync<T>(LocalDataSaveKey key, CancellationToken ct)
            where T : class, new();

        UniTask SaveAsync<T>(LocalDataSaveKey key, T data, CancellationToken ct)
            where T : class, new();

        UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct);

        /// <summary>Returns whether saved data exists for the given key.</summary>
        UniTask<bool> ExistsAsync(LocalDataSaveKey key, CancellationToken ct);
    }
}
