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
    /// <b>Thread-safety:</b> All access to the in-flight dictionary is guarded by an internal
    /// lock, so the gate is safe to call from any thread. Continuations of the awaited task
    /// resume on whatever <see cref="System.Threading.SynchronizationContext"/> the caller was
    /// on — typical Unity + UniTask usage resumes on the main thread, but if you await operations
    /// that resume on a background thread, that's fine: the next gate call will lock as needed.
    /// </para>
    /// </remarks>
    public sealed class KeyedOperationGate<TKey>
    {
        private readonly Dictionary<TKey, UniTaskCompletionSource<bool>> _inflight = new();
        private readonly object _lock = new();

        private UniTask WaitOrRunInternal(TKey key, Func<UniTask> operation, out bool isOwner)
        {
            UniTaskCompletionSource<bool> completionSource;
            lock (_lock)
            {
                if (_inflight.TryGetValue(key, out var existing))
                {
                    isOwner = false;
                    return existing.Task;
                }

                completionSource = new UniTaskCompletionSource<bool>();
                _inflight[key] = completionSource;
                isOwner = true;
            }

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
            UniTaskCompletionSource<bool>[] sources;
            lock (_lock)
            {
                sources = new UniTaskCompletionSource<bool>[_inflight.Count];
                var i = 0;
                foreach (var cs in _inflight.Values)
                {
                    sources[i++] = cs;
                }
                _inflight.Clear();
            }

            foreach (var cs in sources)
            {
                cs.TrySetCanceled();
            }
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
                lock (_lock)
                {
                    // Only remove if we're still the owner — CancelAll may have replaced or cleared us.
                    if (_inflight.TryGetValue(key, out var current) && ReferenceEquals(current, completionSource))
                    {
                        _inflight.Remove(key);
                    }
                }
            }
        }
    }
}
