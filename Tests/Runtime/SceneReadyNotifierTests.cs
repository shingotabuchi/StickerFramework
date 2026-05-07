using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core;
using NUnitAssert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class SceneReadyNotifierTests
    {
        [Test]
        public async Task NotifyReadyCompletesWaiters()
        {
            var notifier = new SceneReadyNotifier();
            var waitTask = notifier.WaitForReady().AsTask();

            notifier.NotifyReady();

            await waitTask;
        }

        [Test]
        public void WaitForReadyObservesCancellation()
        {
            var notifier = new SceneReadyNotifier();
            using var cts = new CancellationTokenSource();

            var waitTask = notifier.WaitForReady(cts.Token).AsTask();
            cts.Cancel();

            NUnitAssert.CatchAsync<OperationCanceledException>(async () => await waitTask);
        }
    }
}
