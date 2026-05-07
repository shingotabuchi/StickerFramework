using System;
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
                TransitionType.None => new NoneTransition(),
                TransitionType.Fade => new FadeTransition(),
                TransitionType.SlideFromLeft => new SlideTransition(SlideTransition.Direction.Left),
                TransitionType.SlideFromRight => new SlideTransition(SlideTransition.Direction.Right),
                TransitionType.SlideFromTop => new SlideTransition(SlideTransition.Direction.Top),
                TransitionType.SlideFromBottom => new SlideTransition(SlideTransition.Direction.Bottom),
                TransitionType.Scale => new ScaleTransition(),
                TransitionType.Animator => CreateAnimatorTransition(view),
                TransitionType.Timeline => CreateTimelineTransition(view),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported UI transition type.")
            };
        }

        static ITransition CreateAnimatorTransition(WindowView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view), "Animator transitions require a window view.");
            }

            var animator = view.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException(
                    $"Window '{view.name}' uses an Animator transition but has no Animator component.");
            }

            return new AnimatorTransition(animator, view.ShowAnimatorState, view.HideAnimatorState);
        }

        static ITransition CreateTimelineTransition(WindowView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view), "Timeline transitions require a window view.");
            }

            return new TimelineTransition(view.ShowTimeline, view.HideTimeline);
        }
    }
}
