using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class SlideTransition : ITransition
    {
        public enum Direction
        {
            Left,
            Right,
            Top,
            Bottom
        }

        [SerializeField] Direction _direction = Direction.Left;

        public Direction SlideDirection
        {
            get => _direction;
            set => _direction = value;
        }

        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            var canvasGroup = view.CanvasGroup;
            var rectTransform = view.RectTransform;

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
