using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    public class SlideTransition : ITransition
    {
        public enum Direction
        {
            Left,
            Right,
            Top,
            Bottom
        }

        readonly Direction _direction;

        public SlideTransition(Direction direction)
        {
            _direction = direction;
        }

        public async UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct)
        {
            var size = rectTransform.rect.size;
            var offset = _direction switch
            {
                Direction.Left => new Vector2(-size.x, 0f),
                Direction.Right => new Vector2(size.x, 0f),
                Direction.Top => new Vector2(0f, size.y),
                Direction.Bottom => new Vector2(0f, -size.y),
                _ => Vector2.zero
            };

            var startPos = isShow ? offset : Vector2.zero;
            var endPos = isShow ? Vector2.zero : offset;
            rectTransform.anchoredPosition = startPos;
            canvasGroup.alpha = 1f;

            if (duration <= 0f)
            {
                rectTransform.anchoredPosition = endPos;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            rectTransform.anchoredPosition = endPos;
        }
    }
}
