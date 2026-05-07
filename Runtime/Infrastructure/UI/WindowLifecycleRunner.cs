using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;

namespace StickerFwk.Infrastructure.UI
{
    internal sealed class WindowLifecycleRunner
    {
        public async UniTask Show(
            WindowView view,
            ITransition transition,
            float transitionDuration,
            CancellationToken ct)
        {
            view.OnBeforeShow();
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition),
                    $"Window '{view.name}' has no show transition assigned.");
            }
            await transition.Play(view, true, transitionDuration, ct);
            view.OnShow();
        }

        public async UniTask Hide(
            WindowView view,
            ITransition transition,
            float transitionDuration,
            CancellationToken ct)
        {
            view.OnBeforeHide();
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition),
                    $"Window '{view.name}' has no hide transition assigned.");
            }
            await transition.Play(view, false, transitionDuration, ct);
            view.OnHide();
        }

        public void HideWithoutTransition(WindowView view)
        {
            view.OnBeforeHide();
            view.OnHide();
        }
    }
}
