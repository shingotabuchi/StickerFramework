using System.Threading;
using StickerFwk.Core;
using NUnitAssert = NUnit.Framework.Assert;
using TestAttribute = NUnit.Framework.TestAttribute;

namespace StickerFwk.Tests.Runtime
{
    public class ScopeCancellationTests
    {
        [Test]
        public void DisposeCancelsScopeAndLinkedChildren()
        {
            var scope = new ScopeCancellation();
            var child = scope.CreateLinked();

            scope.Dispose();

            NUnitAssert.IsTrue(scope.IsCancellationRequested);
            NUnitAssert.IsTrue(scope.Token.IsCancellationRequested);
            NUnitAssert.IsTrue(child.IsCancellationRequested);
            NUnitAssert.IsTrue(child.Token.IsCancellationRequested);
        }

        [Test]
        public void DisposingChildDoesNotCancelScope()
        {
            using var scope = new ScopeCancellation();
            var child = scope.CreateLinked();

            child.Dispose();

            NUnitAssert.IsFalse(scope.IsCancellationRequested);
            NUnitAssert.IsTrue(child.IsCancellationRequested);
        }

        [Test]
        public void ExternalCancellationCancelsLinkedChildOnly()
        {
            using var externalCts = new CancellationTokenSource();
            using var scope = new ScopeCancellation();
            var child = scope.CreateLinked(externalCts.Token);

            externalCts.Cancel();

            NUnitAssert.IsFalse(scope.IsCancellationRequested);
            NUnitAssert.IsTrue(child.IsCancellationRequested);
        }
    }
}
