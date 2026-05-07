using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StickerFwk.Core
{
    /// <summary>
    /// Coalesces concurrent async operations keyed by <typeparamref name="TKey"/>: if an operation
    /// for a given key is already in flight, subsequent calls await the same task instead of
    /// starting a new one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Thread-safety:</b> This type is <b>not</b> thread-safe. The internal dictionary is
    /// accessed without synchronization, so all members must be invoked from a single thread.
    /// </para>
    /// <para>
    /// In typical Unity + UniTask usage this is fine because UniTask continuations resume on the
    /// Unity main thread (PlayerLoop) by default, so callers that only ever invoke this gate from
    /// the main thread are safe. If you await operations that resume on a background thread (for
    /// example via <c>UniTask.SwitchToThreadPool</c>) and then call back into the gate, you must
    /// switch back to the main thread first, or provide your own synchronization.
    /// </para>
    /// </remarks>
    public sealed class KeyedOperationGate<TKey>
    {
        private readonly Dictionary<TKey, UniTaskCompletionSource<bool>> _inflight = new();

        private UniTask WaitOrRunInternal(TKey key, Func<UniTask> operation, out bool isOwner)
        {
            if (_inflight.TryGetValue(key, out var existing))
            {
                isOwner = false;
                return existing.Task;
            }

            var completionSource = new UniTaskCompletionSource<bool>();
            _inflight[key] = completionSource;
            isOwner = true;

            RunOperation(key, operation, completionSource).Forget();
            return completionSource.Task;
        }

        public async UniTask WaitOrRun(
            TKey key,
            Func<UniTask> operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var task = WaitOrRunInternal(key, operation, out _);
            await task.AttachExternalCancellation(cancellationToken);
        }

        public void CancelAll()
        {
            foreach (var completionSource in _inflight.Values)
            {
                completionSource.TrySetCanceled();
            }
            _inflight.Clear();
        }

        private async UniTaskVoid RunOperation(TKey key, Func<UniTask> operation, UniTaskCompletionSource<bool> completionSource)
        {
            try
            {
                await operation();
                completionSource.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                completionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
            finally
            {
                _inflight.Remove(key);
            }
        }
    }
}
