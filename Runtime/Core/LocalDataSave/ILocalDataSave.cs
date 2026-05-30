using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSave
    {
        /// <summary>
        /// Reads raw bytes for the given key, or returns <c>null</c> when no data has been saved for the key.
        /// </summary>
        UniTask<byte[]> ReadAsync(LocalDataSaveKey key, CancellationToken ct);

        /// <summary>
        /// Writes raw bytes for the given key, replacing any previously saved data.
        /// </summary>
        UniTask WriteAsync(LocalDataSaveKey key, ReadOnlyMemory<byte> data, CancellationToken ct);

        /// <summary>
        /// Deletes saved data for the given key when it exists.
        /// </summary>
        UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct);
    }
}
