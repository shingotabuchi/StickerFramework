using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    public class FadeTransition : ITransition
    {
        public async UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct)
        {
            var startAlpha = isShow ? 0f : 1f;
            var endAlpha = isShow ? 1f : 0f;
            canvasGroup.alpha = startAlpha;

            if (duration <= 0f)
            {
                canvasGroup.alpha = endAlpha;
                return;
            }

            var elapsed = 0f;
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
