using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSave
    {
        UniTask<byte[]> ReadAsync(LocalDataSaveKey key, CancellationToken ct);

        UniTask WriteAsync(LocalDataSaveKey key, ReadOnlyMemory<byte> data, CancellationToken ct);

        UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct);
    }
}
