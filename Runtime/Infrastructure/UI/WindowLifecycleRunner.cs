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
            StickerFwk.Core.Log.Info("FadeDbg", $"Hide enter view='{view?.name ?? "null"}' trans='{transition?.GetType().Name ?? "null"}' duration={transitionDuration} ctCancelled={ct.IsCancellationRequested}");
            view.OnBeforeHide();
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition),
                    $"Window '{view.name}' has no hide transition assigned.");
            }
            try
            {
                await transition.Play(view, false, transitionDuration, ct);
            }
            catch (OperationCanceledException)
            {
                StickerFwk.Core.Log.Warning("FadeDbg", $"Hide cancelled view='{view?.name ?? "null"}'");
                throw;
            }
            view.OnHide();
            StickerFwk.Core.Log.Info("FadeDbg", $"Hide exit view='{view?.name ?? "null"}'");
        }

        public void HideWithoutTransition(WindowView view)
        {
            StickerFwk.Core.Log.Warning("FadeDbg", $"HideWithoutTransition view='{view?.name ?? "null"}'");
            view.OnBeforeHide();
            view.OnHide();
        }
    }
}
