using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Presentation
{
    /// <summary>
    /// Runs presentation-only async work and lets a fresh pointer press fast-forward it.
    /// </summary>
    public static class SkippablePresentationSequence
    {
        public static UniTask RunAsync(
            Func<CancellationToken, UniTask> playAsync,
            Action completeImmediately,
            CancellationToken cancellationToken)
        {
            return RunAsync(
                playAsync,
                completeImmediately,
                PresentationPointerInput.UnityDefault,
                cancellationToken);
        }

        public static async UniTask RunAsync(
            Func<CancellationToken, UniTask> playAsync,
            Action completeImmediately,
            PresentationPointerInput pointerInput,
            CancellationToken cancellationToken)
        {
            using var skipCts = new CancellationTokenSource();
            using var playCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, skipCts.Token);
            using var skipWatchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var sequenceFinishedTcs = new UniTaskCompletionSource();
            var exitReasonTcs = new UniTaskCompletionSource<SequenceExitReason>();

            RunSequenceAsync(playAsync, playCts.Token, skipCts, cancellationToken, sequenceFinishedTcs, exitReasonTcs).Forget();
            WaitForSkipAsync(pointerInput, skipWatchCts.Token, exitReasonTcs).Forget();

            try
            {
                var exitReason = await exitReasonTcs.Task;
                if (exitReason == SequenceExitReason.Skipped)
                {
                    skipCts.Cancel();
                    await sequenceFinishedTcs.Task;
                    completeImmediately?.Invoke();

                    await pointerInput.WaitForReleaseAsync(cancellationToken);
                    return;
                }

                await sequenceFinishedTcs.Task;
            }
            finally
            {
                skipWatchCts.Cancel();
            }
        }

        static async UniTask RunSequenceAsync(
            Func<CancellationToken, UniTask> playAsync,
            CancellationToken playCancellationToken,
            CancellationTokenSource skipCts,
            CancellationToken lifecycleCancellationToken,
            UniTaskCompletionSource sequenceFinishedTcs,
            UniTaskCompletionSource<SequenceExitReason> exitReasonTcs)
        {
            try
            {
                await playAsync(playCancellationToken);
                exitReasonTcs.TrySetResult(SequenceExitReason.Completed);
            }
            catch (OperationCanceledException) when (skipCts.IsCancellationRequested && !lifecycleCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                exitReasonTcs.TrySetException(ex);
            }
            finally
            {
                sequenceFinishedTcs.TrySetResult();
            }
        }

        static async UniTask WaitForSkipAsync(
            PresentationPointerInput pointerInput,
            CancellationToken cancellationToken,
            UniTaskCompletionSource<SequenceExitReason> exitReasonTcs)
        {
            try
            {
                await pointerInput.WaitForFreshPressAsync(cancellationToken);
                exitReasonTcs.TrySetResult(SequenceExitReason.Skipped);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                exitReasonTcs.TrySetException(ex);
            }
        }

        enum SequenceExitReason
        {
            Completed,
            Skipped
        }
    }
}
