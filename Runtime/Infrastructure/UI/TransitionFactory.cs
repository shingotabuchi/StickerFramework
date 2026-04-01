using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    public static class TransitionFactory
    {
        public static ITransition Create(TransitionType type, WindowView view = null)
        {
            return type switch
            {
                TransitionType.Fade => new FadeTransition(),
                TransitionType.SlideFromLeft => new SlideTransition(SlideTransition.Direction.Left),
                TransitionType.SlideFromRight => new SlideTransition(SlideTransition.Direction.Right),
                TransitionType.SlideFromTop => new SlideTransition(SlideTransition.Direction.Top),
                TransitionType.SlideFromBottom => new SlideTransition(SlideTransition.Direction.Bottom),
                TransitionType.Scale => new ScaleTransition(),
                TransitionType.Animator => new AnimatorTransition(
                    view != null ? view.GetComponent<Animator>() : null,
                    view != null ? view.ShowAnimatorState : "Show",
                    view != null ? view.HideAnimatorState : "Hide"),
                TransitionType.Timeline => new TimelineTransition(
                    view != null ? view.ShowTimeline : null,
                    view != null ? view.HideTimeline : null),
                _ => new NoneTransition()
            };
        }
    }
}
