using System;
using System.Collections.Generic;

namespace StickerFwk.Core
{
    public readonly struct CameraSlot
    {
        public CameraSlot(CameraId id, bool isBase, float depth)
        {
            Id = id;
            IsBase = isBase;
            Depth = depth;
        }

        public CameraId Id { get; }
        public bool IsBase { get; }
        public float Depth { get; }
    }

    public static class CameraStackResolver
    {
        public readonly struct Result
        {
            public Result(
                IReadOnlyList<CameraId> enabledIds,
                IReadOnlyList<CameraId> cameraStackOrder,
                CameraId? activeBase)
            {
                EnabledIds = enabledIds;
                CameraStackOrder = cameraStackOrder;
                ActiveBase = activeBase;
            }

            public readonly IReadOnlyList<CameraId> EnabledIds;
            public readonly IReadOnlyList<CameraId> CameraStackOrder;
            public readonly CameraId? ActiveBase;
        }

        public static Result Resolve(
            IReadOnlyList<CameraSlot> slots,
            IReadOnlyList<CameraId> baseStack,
            ISet<CameraId> disabledOverlays)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            if (baseStack == null)
            {
                throw new ArgumentNullException(nameof(baseStack));
            }

            if (disabledOverlays == null)
            {
                throw new ArgumentNullException(nameof(disabledOverlays));
            }

            var activeBase = ResolveActiveBase(slots, baseStack);
            var enabledIds = new List<CameraId>();
            var cameraStackOrder = new List<CameraId>();

            if (!activeBase.HasValue)
            {
                return new Result(enabledIds, cameraStackOrder, null);
            }

            enabledIds.Add(activeBase.Value);

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsBase || disabledOverlays.Contains(slot.Id))
                {
                    continue;
                }

                enabledIds.Add(slot.Id);
                InsertOverlaySortedByDepth(cameraStackOrder, slot, slots);
            }

            return new Result(enabledIds, cameraStackOrder, activeBase);
        }

        static CameraId? ResolveActiveBase(IReadOnlyList<CameraSlot> slots, IReadOnlyList<CameraId> baseStack)
        {
            if (baseStack.Count == 0)
            {
                return null;
            }

            var top = baseStack[baseStack.Count - 1];
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsBase && slot.Id == top)
                {
                    return top;
                }
            }

            return null;
        }

        static void InsertOverlaySortedByDepth(
            List<CameraId> cameraStackOrder,
            CameraSlot slot,
            IReadOnlyList<CameraSlot> slots)
        {
            var insertAt = cameraStackOrder.Count;
            while (insertAt > 0 && FindDepth(slots, cameraStackOrder[insertAt - 1]) > slot.Depth)
            {
                insertAt--;
            }

            cameraStackOrder.Insert(insertAt, slot.Id);
        }

        static float FindDepth(IReadOnlyList<CameraSlot> slots, CameraId id)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Id == id)
                {
                    return slots[i].Depth;
                }
            }

            return 0f;
        }
    }
}
