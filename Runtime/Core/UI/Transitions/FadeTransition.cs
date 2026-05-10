using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class FadeTransition : ITransition
    {
        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            var canvasGroup = view.CanvasGroup;
            var startAlpha = isShow ? 0f : 1f;
            var endAlpha = isShow ? 1f : 0f;
            canvasGroup.alpha = startAlpha;
            Log.Info("FadeTransition", $"Play start view='{view.name}' isShow={isShow} duration={duration} startAlpha={startAlpha} endAlpha={endAlpha} canvasEnabled={(canvasGroup.GetComponentInParent<Canvas>()?.enabled.ToString() ?? "null")} ctCancelled={ct.IsCancellationRequested}");

            if (duration <= 0f)
            {
                canvasGroup.alpha = endAlpha;
                Log.Info("FadeTransition", $"Play short-circuit (duration<=0) view='{view.name}' isShow={isShow}");
                return;
            }

            var elapsed = 0f;
            // Yield once before starting the timer so a giant first-frame deltaTime
            // (e.g. right after SceneManager.LoadSceneAsync completes) doesn't consume
            // the entire transition in a single tick.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            var iterations = 0;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                iterations++;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            canvasGroup.alpha = endAlpha;
            Log.Info("FadeTransition", $"Play complete view='{view.name}' isShow={isShow} iterations={iterations} elapsed={elapsed}");
        }
    }
}
