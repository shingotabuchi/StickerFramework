using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StickerFwk.Core
{
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
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var task = WaitOrRunInternal(key, operation, out var isOwner);
                try
                {
                    await task;
                    return;
                }
                catch (Exception)
                {
                    if (!isOwner)
                    {
                        await UniTask.Yield();
                        continue;
                    }

                    throw;
                }
            }
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
