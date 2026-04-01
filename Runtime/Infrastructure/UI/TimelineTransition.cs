using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.Playables;

namespace StickerFwk.Infrastructure.UI
{
    public class TimelineTransition : ITransition
    {
        readonly PlayableDirector _showDirector;
        readonly PlayableDirector _hideDirector;

        public TimelineTransition(PlayableDirector showDirector, PlayableDirector hideDirector)
        {
            _showDirector = showDirector;
            _hideDirector = hideDirector;
        }

        public async UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct)
        {
            var director = isShow ? _showDirector : _hideDirector;
            if (director == null)
            {
                canvasGroup.alpha = isShow ? 1f : 0f;
                return;
            }

            await director.PlayAsync(ct);
        }
    }
}
