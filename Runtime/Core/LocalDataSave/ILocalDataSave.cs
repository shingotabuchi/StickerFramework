using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.LocalDataSave
{
    public readonly struct LocalDataSaveReadResult
    {
        public bool Found { get; }

        public byte[] Bytes { get; }

        public LocalDataSaveReadResult(bool found, byte[] bytes)
        {
            Found = found;
            Bytes = bytes;
        }

        public static LocalDataSaveReadResult Missing => new LocalDataSaveReadResult(false, null);
    }

    public interface ILocalDataSave
    {
        UniTask<LocalDataSaveReadResult> ReadAsync(LocalDataSaveKey key, CancellationToken ct);

        UniTask WriteAsync(LocalDataSaveKey key, ReadOnlyMemory<byte> data, CancellationToken ct);

        UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct);
    }
}
