using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    public class ScaleTransition : ITransition
    {
        const float MinScale = 0.85f;

        public async UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct)
        {
            var startScale = isShow ? MinScale : 1f;
            var endScale = isShow ? 1f : MinScale;
            var startAlpha = isShow ? 0f : 1f;
            var endAlpha = isShow ? 1f : 0f;

            rectTransform.localScale = Vector3.one * startScale;
            canvasGroup.alpha = startAlpha;

            if (duration <= 0f)
            {
                rectTransform.localScale = Vector3.one * endScale;
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
                rectTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            rectTransform.localScale = Vector3.one * endScale;
            canvasGroup.alpha = endAlpha;
        }
    }
}
