using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Playables;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class TimelineTransition : ITransition
    {
        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            var targets = view.GetComponent<TimelineTransitionTargets>();
            if (targets == null)
            {
                throw new InvalidOperationException(
                    $"Window '{view.name}' uses TimelineTransition but is missing a {nameof(TimelineTransitionTargets)} component on its root.");
            }

            var director = isShow ? targets.ShowDirector : targets.HideDirector;
            if (director == null)
            {
                var direction = isShow ? "show" : "hide";
                throw new InvalidOperationException(
                    $"{nameof(TimelineTransitionTargets)} on window '{view.name}' is missing its {direction} PlayableDirector.");
            }

            await director.PlayAsync(ct);
        }
    }
}
