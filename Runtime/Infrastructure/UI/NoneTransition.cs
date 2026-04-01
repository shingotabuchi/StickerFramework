using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    public class NoneTransition : ITransition
    {
        public UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct)
        {
            canvasGroup.alpha = isShow ? 1f : 0f;
            return UniTask.CompletedTask;
        }
    }
}
