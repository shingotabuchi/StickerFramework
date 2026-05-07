using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Strategy that plays a window's show/hide animation. Implementations are stored on
    /// <see cref="WindowView"/> via <c>[SerializeReference]</c>, so concrete types should be
    /// marked <c>[System.Serializable]</c> with a parameterless constructor and serialized
    /// fields for any per-window configuration.
    /// </summary>
    public interface ITransition
    {
        UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct);
    }
}
