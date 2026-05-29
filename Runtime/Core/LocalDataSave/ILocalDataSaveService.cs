using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveService
    {
        UniTask<T> LoadAsync<T>(LocalDataSaveKey key, ILocalDataSaveMigrator<T> migrator, CancellationToken ct)
            where T : class, new();

        UniTask SaveAsync<T>(LocalDataSaveKey key, T data, CancellationToken ct) where T : class;

        UniTask ExecuteInTransactionAsync(
            Func<ILocalDataSaveTransaction, CancellationToken, UniTask> action,
            CancellationToken ct);

        UniTask<ILocalDataSaveTransaction> BeginTransactionAsync(CancellationToken ct);
    }
}
