using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;

namespace StickerFwk.Infrastructure.UI
{
    internal sealed class WindowLifecycleRunner
    {
        public async UniTask Show(
            WindowView view,
            TransitionType transitionType,
            float transitionDuration,
            CancellationToken ct)
        {
            view.OnBeforeShow();
            var transition = TransitionFactory.Create(transitionType, view);
            await transition.Play(view.CanvasGroup, view.RectTransform, true, transitionDuration, ct);
            view.OnShow();
        }

        public async UniTask Hide(WindowView view, CancellationToken ct)
        {
            view.OnBeforeHide();
            var transition = TransitionFactory.Create(view.HideTransition, view);
            await transition.Play(view.CanvasGroup, view.RectTransform, false, view.TransitionDuration, ct);
            view.OnHide();
        }

        public void HideWithoutTransition(WindowView view)
        {
            view.OnBeforeHide();
            view.OnHide();
        }
    }
}
