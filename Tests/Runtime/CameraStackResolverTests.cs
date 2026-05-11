using System.Collections.Generic;
using NUnit.Framework;
using StickerFwk.Core;
using Assert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class CameraStackResolverTests
    {
        static readonly CameraId TestBaseA = new CameraId("TestBaseA");
        static readonly CameraId TestBaseB = new CameraId("TestBaseB");
        static readonly CameraId TestOverlay1 = new CameraId("TestOverlay1");
        static readonly CameraId TestOverlay2 = new CameraId("TestOverlay2");

        [Test]
        public void EmptySlots_HasNoActiveBaseOrEnabledIds()
        {
            var result = CameraStackResolver.Resolve(
                new List<CameraSlot>(),
                new List<CameraId>(),
                new HashSet<CameraId>());

            Assert.That(result.ActiveBase, Is.Null);
            Assert.That(result.EnabledIds, Is.Empty);
            Assert.That(result.CameraStackOrder, Is.Empty);
        }

        [Test]
        public void SingleBaseInStack_EnablesBaseOnly()
        {
            var result = CameraStackResolver.Resolve(
                new List<CameraSlot> { new CameraSlot(TestBaseA, true, 0f) },
                new List<CameraId> { TestBaseA },
                new HashSet<CameraId>());

            Assert.That(result.ActiveBase, Is.EqualTo(TestBaseA));
            Assert.That(result.EnabledIds, Is.EqualTo(new[] { TestBaseA }));
            Assert.That(result.CameraStackOrder, Is.Empty);
        }

        [Test]
        public void MultipleBasesInStack_EnablesOnlyTopBase()
        {
            var result = CameraStackResolver.Resolve(
                new List<CameraSlot>
                {
                    new CameraSlot(TestBaseA, true, 0f),
                    new CameraSlot(TestBaseB, true, 10f),
                },
                new List<CameraId> { TestBaseA, TestBaseB },
                new HashSet<CameraId>());

            Assert.That(result.ActiveBase, Is.EqualTo(TestBaseB));
            Assert.That(result.EnabledIds, Is.EqualTo(new[] { TestBaseB }));
            Assert.That(result.EnabledIds, Has.No.Member(TestBaseA));
        }

        [Test]
        public void ActiveBaseWithOverlays_SortsOverlaysByDepthAndEnablesAll()
        {
            var result = CameraStackResolver.Resolve(
                new List<CameraSlot>
                {
                    new CameraSlot(TestOverlay2, false, 30f),
                    new CameraSlot(TestBaseA, true, 0f),
                    new CameraSlot(TestOverlay1, false, 10f),
                },
                new List<CameraId> { TestBaseA },
                new HashSet<CameraId>());

            Assert.That(result.ActiveBase, Is.EqualTo(TestBaseA));
            Assert.That(result.EnabledIds, Is.EqualTo(new[] { TestBaseA, TestOverlay2, TestOverlay1 }));
            Assert.That(result.CameraStackOrder, Is.EqualTo(new[] { TestOverlay1, TestOverlay2 }));
        }

        [Test]
        public void DisabledOverlays_AreExcludedFromEnabledIdsAndStackOrder()
        {
            var result = CameraStackResolver.Resolve(
                new List<CameraSlot>
                {
                    new CameraSlot(TestBaseA, true, 0f),
                    new CameraSlot(TestOverlay1, false, 10f),
                    new CameraSlot(TestOverlay2, false, 20f),
                },
                new List<CameraId> { TestBaseA },
                new HashSet<CameraId> { TestOverlay1 });

            Assert.That(result.ActiveBase, Is.EqualTo(TestBaseA));
            Assert.That(result.EnabledIds, Is.EqualTo(new[] { TestBaseA, TestOverlay2 }));
            Assert.That(result.EnabledIds, Has.No.Member(TestOverlay1));
            Assert.That(result.CameraStackOrder, Is.EqualTo(new[] { TestOverlay2 }));
        }

        [Test]
        public void OverlayRegisteredWithNoActiveBase_IsNotStacked()
        {
            var result = CameraStackResolver.Resolve(
                new List<CameraSlot> { new CameraSlot(TestOverlay1, false, 10f) },
                new List<CameraId>(),
                new HashSet<CameraId>());

            Assert.That(result.ActiveBase, Is.Null);
            Assert.That(result.EnabledIds, Is.Empty);
            Assert.That(result.CameraStackOrder, Is.Empty);
        }
    }
}
