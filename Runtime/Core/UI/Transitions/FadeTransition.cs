using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

            if (duration <= 0f)
            {
                canvasGroup.alpha = endAlpha;
                return;
            }

            var elapsed = 0f;
            // Yield once before starting the timer so a giant first-frame deltaTime
            // (e.g. right after SceneManager.LoadSceneAsync completes) doesn't consume
            // the entire transition in a single tick.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            canvasGroup.alpha = endAlpha;
        }
    }
}
