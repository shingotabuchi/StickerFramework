using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveTransaction
    {
        UniTask SaveAsync<T>(LocalDataSaveKey key, T data, CancellationToken ct) where T : class;

        UniTask CommitAsync(CancellationToken ct);

        UniTask RollbackAsync(CancellationToken ct);
    }
}
