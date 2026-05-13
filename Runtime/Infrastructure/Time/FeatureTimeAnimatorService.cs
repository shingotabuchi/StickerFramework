using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Infrastructure.Time
{
    public sealed class FeatureTimeAnimatorService : IDisposable
    {
        private readonly FeatureTimeService _timeService;
        private readonly List<AnimatorState> _animators = new();

        public FeatureTimeAnimatorService(FeatureTimeService timeService)
        {
            _timeService = timeService;
            _timeService.LocalTimeScaleChanged += OnLocalTimeScaleChanged;
        }

        public void Register(Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            _animators.Add(new AnimatorState(animator, animator.speed));
            animator.speed *= _timeService.LocalTimeScale;
        }

        public void Dispose()
        {
            _timeService.LocalTimeScaleChanged -= OnLocalTimeScaleChanged;

            foreach (var state in _animators)
            {
                if (state.Animator != null)
                {
                    state.Animator.speed = state.BaseSpeed;
                }
            }

            _animators.Clear();
        }

        private void OnLocalTimeScaleChanged(float localTimeScale)
        {
            for (var i = _animators.Count - 1; i >= 0; i--)
            {
                var state = _animators[i];
                if (state.Animator == null)
                {
                    _animators.RemoveAt(i);
                    continue;
                }

                state.Animator.speed = state.BaseSpeed * localTimeScale;
            }
        }

        private readonly struct AnimatorState
        {
            public readonly Animator Animator;
            public readonly float BaseSpeed;

            public AnimatorState(Animator animator, float baseSpeed)
            {
                Animator = animator;
                BaseSpeed = baseSpeed;
            }
        }
    }
}
