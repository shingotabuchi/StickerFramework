using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.MasterData
{
    public interface IMasterDataRepository
    {
        bool IsLoaded { get; }

        UniTask LoadAsync(CancellationToken ct = default);

        IReadOnlyList<T> GetAll<T>() where T : class, IMasterData;

        T Get<T>(string id) where T : class, IMasterData;

        bool TryGet<T>(string id, out T data) where T : class, IMasterData;
    }
}
