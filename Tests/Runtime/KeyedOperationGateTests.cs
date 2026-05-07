using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core;
using NUnitAssert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class KeyedOperationGateTests
    {
        [Test]
        public async Task ConcurrentWaitersReceiveOwnerFailureWithoutRetrying()
        {
            var gate = new KeyedOperationGate<string>();
            var started = new UniTaskCompletionSource();
            var release = new UniTaskCompletionSource();
            var runs = 0;

            async UniTask Operation()
            {
                runs++;
                started.TrySetResult();
                await release.Task;
                throw new InvalidOperationException("boom");
            }

            var first = gate.WaitOrRun("asset", Operation).AsTask();
            await started.Task;
            var second = gate.WaitOrRun("asset", Operation).AsTask();

            release.TrySetResult();

            NUnitAssert.ThrowsAsync<InvalidOperationException>(async () => await first);
            NUnitAssert.ThrowsAsync<InvalidOperationException>(async () => await second);
            NUnitAssert.That(runs, Is.EqualTo(1));
        }

        [Test]
        public void AlreadyCancelledTokenDoesNotStartOperation()
        {
            var gate = new KeyedOperationGate<string>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            NUnitAssert.CatchAsync<OperationCanceledException>(async () =>
                await gate.WaitOrRun("asset", () => UniTask.CompletedTask, cts.Token).AsTask());
        }
    }
}
