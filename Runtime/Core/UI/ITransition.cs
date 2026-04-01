using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    public interface ITransition
    {
        UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct);
    }
}
