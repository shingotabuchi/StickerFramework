using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Initialization
{
    public interface IRootInitService
    {
        UniTask Initialization { get; }
    }
}
