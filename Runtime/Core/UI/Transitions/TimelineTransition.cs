using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class TimelineTransition : ITransition
    {
        [SerializeField] PlayableDirector _showDirector;
        [SerializeField] PlayableDirector _hideDirector;

        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            if (isShow)
            {
                view.CanvasGroup.alpha = 1f;
            }

            var director = isShow ? _showDirector : _hideDirector;
            if (director == null)
            {
                var direction = isShow ? "show" : "hide";
                throw new InvalidOperationException($"Timeline transition is missing its {direction} PlayableDirector.");
            }

            await director.PlayAsync(ct);
        }
    }
}
