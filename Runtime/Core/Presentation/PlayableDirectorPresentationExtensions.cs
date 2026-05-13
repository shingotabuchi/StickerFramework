using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Playables;

namespace StickerFwk.Core.Presentation
{
    /// <summary>
    /// Helpers for timeline-driven presentation sequences.
    /// </summary>
    public static class PlayableDirectorPresentationExtensions
    {
        const double EndEvaluationEpsilonSeconds = 0.001d;
        const double FastForwardStepSeconds = 1d / 60d;

        /// <summary>
        /// Plays a director from the start, forces hold extrapolation to avoid end-of-timeline
        /// binding flicker, then waits for end/skip and final confirmation.
        /// </summary>
        public static UniTask PlayFromStartAndWaitForEndOrSkipThenConfirmAsync(
            this PlayableDirector director,
            CancellationToken cancellationToken = default)
        {
            return director.PlayFromStartAndWaitForEndOrSkipThenConfirmAsync(
                PresentationPointerInput.UnityDefault,
                cancellationToken);
        }

        /// <summary>
        /// Plays a director from the start, forces hold extrapolation to avoid end-of-timeline
        /// binding flicker, then waits for end/skip and final confirmation.
        /// </summary>
        public static async UniTask PlayFromStartAndWaitForEndOrSkipThenConfirmAsync(
            this PlayableDirector director,
            PresentationPointerInput pointerInput,
            CancellationToken cancellationToken = default)
        {
            if (director == null)
            {
                return;
            }

            director.Stop();
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.time = 0d;
            director.Evaluate();
            director.Play();

            await director.WaitForEndOrSkipThenConfirmAsync(pointerInput, cancellationToken);
        }

        /// <summary>
        /// Waits for a playing director to end or be skipped, holds on the final evaluated frame,
        /// then waits for a fresh pointer press before returning.
        /// </summary>
        public static UniTask WaitForEndOrSkipThenConfirmAsync(
            this PlayableDirector director,
            CancellationToken cancellationToken = default)
        {
            return director.WaitForEndOrSkipThenConfirmAsync(
                PresentationPointerInput.UnityDefault,
                cancellationToken);
        }

        public static async UniTask WaitForEndOrSkipThenConfirmAsync(
            this PlayableDirector director,
            PresentationPointerInput pointerInput,
            CancellationToken cancellationToken = default)
        {
            await director.WaitForEndOrSkipAsync(pointerInput, cancellationToken);
            director.CompleteImmediate();
            await pointerInput.WaitForReleaseAsync(cancellationToken);
            await pointerInput.WaitForFreshPressAsync(cancellationToken);
            await pointerInput.WaitForReleaseAsync(cancellationToken);
        }

        public static async UniTask WaitForEndOrSkipAsync(
            this PlayableDirector director,
            PresentationPointerInput pointerInput,
            CancellationToken cancellationToken = default)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            while (director != null && director.state == PlayState.Playing)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (pointerInput.WasPressedThisFrame() || director.HasReachedEnd())
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public static void CompleteImmediate(this PlayableDirector director)
        {
            if (director == null)
            {
                return;
            }

            var endTime = director.ResolveEndTime();
            if (endTime.HasValue)
            {
                director.FastForwardEvaluate(Math.Max(0d, endTime.Value - EndEvaluationEpsilonSeconds));
            }

            director.Pause();
        }

        public static bool HasReachedEnd(this PlayableDirector director)
        {
            var endTime = director.ResolveEndTime();
            return endTime.HasValue && director.time >= Math.Max(0d, endTime.Value - EndEvaluationEpsilonSeconds);
        }

        static double? ResolveEndTime(this PlayableDirector director)
        {
            if (director == null)
            {
                return null;
            }

            var duration = director.duration;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
            {
                return null;
            }

            return duration;
        }

        static void FastForwardEvaluate(this PlayableDirector director, double targetTime)
        {
            var currentTime = Math.Max(0d, director.time);
            if (targetTime <= currentTime)
            {
                director.time = targetTime;
                director.Evaluate();
                return;
            }

            var nextTime = currentTime;
            while (nextTime < targetTime)
            {
                nextTime = Math.Min(targetTime, nextTime + FastForwardStepSeconds);
                director.time = nextTime;
                director.Evaluate();
            }
        }
    }
}
