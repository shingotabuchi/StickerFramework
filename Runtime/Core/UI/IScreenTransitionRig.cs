using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.UI
{
    public interface IScreenTransitionRig
    {
        UniTask Show(CancellationToken ct);
        UniTask Hide(CancellationToken ct);
        void SetProgress(float value);
    }
}
