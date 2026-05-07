using System.Collections.Generic;
using NUnit.Framework;
using StickerFwk.Core;
using Assert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class CameraStackResolverTests
    {
        [Test]
        public void EmptyInput_NoBase()
        {
            var slots = new List<CameraSlot>();
            var enabled = new List<CameraId>();
            var stack = new List<CameraId>();

            var result = CameraStackResolver.Resolve(slots, enabled, stack);

            Assert.That(result.HasBase, Is.False);
            Assert.That(enabled, Is.Empty);
            Assert.That(stack, Is.Empty);
        }

        [Test]
        public void SingleSlot_BecomesBase_NoOverlays()
        {
            var slots = new List<CameraSlot> { new CameraSlot(CameraId.World, -10f) };
            var enabled = new List<CameraId>();
            var stack = new List<CameraId>();

            var result = CameraStackResolver.Resolve(slots, enabled, stack);

            Assert.That(result.HasBase, Is.True);
            Assert.That(result.WinningBase, Is.EqualTo(CameraId.World));
            Assert.That(enabled, Is.EqualTo(new[] { CameraId.World }));
            Assert.That(stack, Is.Empty);
        }

        [Test]
        public void LowestDepth_WinsBase_OverlaysSortedAscending()
        {
            var slots = new List<CameraSlot>
            {
                new CameraSlot(CameraId.UI, 20f),
                new CameraSlot(CameraId.Wipe, 50f),
                new CameraSlot(CameraId.World, -10f),
                new CameraSlot(CameraId.UIOverlay, 40f),
                new CameraSlot(CameraId.WorldOverlay, 30f),
            };
            var enabled = new List<CameraId>();
            var stack = new List<CameraId>();

            var result = CameraStackResolver.Resolve(slots, enabled, stack);

            Assert.That(result.HasBase, Is.True);
            Assert.That(result.WinningBase, Is.EqualTo(CameraId.World));
            // Base first in enabled, then every overlay in any order.
            Assert.That(enabled[0], Is.EqualTo(CameraId.World));
            Assert.That(enabled, Has.Count.EqualTo(5));
            // Stack is overlays sorted by depth ascending.
            Assert.That(stack, Is.EqualTo(new[]
            {
                CameraId.UI,
                CameraId.WorldOverlay,
                CameraId.UIOverlay,
                CameraId.Wipe,
            }));
        }

        [Test]
        public void RootOnly_WipeAlone_BecomesBase()
        {
            var slots = new List<CameraSlot> { new CameraSlot(CameraId.Wipe, 50f) };
            var enabled = new List<CameraId>();
            var stack = new List<CameraId>();

            var result = CameraStackResolver.Resolve(slots, enabled, stack);

            Assert.That(result.WinningBase, Is.EqualTo(CameraId.Wipe));
            Assert.That(stack, Is.Empty);
        }

        [Test]
        public void RootPlusBackground_BackgroundWins_WipeOverlays()
        {
            var slots = new List<CameraSlot>
            {
                new CameraSlot(CameraId.Wipe, 50f),
                new CameraSlot(CameraId.Background, -5f),
            };
            var enabled = new List<CameraId>();
            var stack = new List<CameraId>();

            var result = CameraStackResolver.Resolve(slots, enabled, stack);

            Assert.That(result.WinningBase, Is.EqualTo(CameraId.Background));
            Assert.That(stack, Is.EqualTo(new[] { CameraId.Wipe }));
        }

        [Test]
        public void Resolve_ClearsOutputBuffers()
        {
            var slots = new List<CameraSlot> { new CameraSlot(CameraId.UI, 20f) };
            var enabled = new List<CameraId> { CameraId.Wipe, CameraId.Background };
            var stack = new List<CameraId> { CameraId.World };

            CameraStackResolver.Resolve(slots, enabled, stack);

            Assert.That(enabled, Is.EqualTo(new[] { CameraId.UI }));
            Assert.That(stack, Is.Empty);
        }
    }
}
