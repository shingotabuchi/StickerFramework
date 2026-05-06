using System.Collections.Generic;

namespace StickerFwk.Core
{
    // Snapshot of one camera registered through the active profiles. Pure data so
    // CameraStackResolver can be unit-tested without Unity.
    public readonly struct CameraSlot
    {
        public readonly CameraId Id;
        public readonly UnityEngine.Rendering.Universal.CameraRenderType RenderType;
        public readonly float Depth;

        public CameraSlot(
            CameraId id,
            UnityEngine.Rendering.Universal.CameraRenderType renderType,
            float depth)
        {
            Id = id;
            RenderType = renderType;
            Depth = depth;
        }
    }

    // Decides which cameras render and how the base camera's overlay stack is composed.
    //
    // Rules:
    //   1. Every slot supplied is "wanted" (slots come from active profiles only — there is no
    //      additional gate like mode or lease).
    //   2. Among wanted Base slots, the lowest-depth one wins. Other Base slots are forced off.
    //      Only one base camera may render at a time.
    //   3. The winner's overlay stack = all wanted Overlay slots, sorted by depth ascending.
    //
    // Results are written into caller-provided buffers (avoid per-frame allocations).
    public static class CameraStackResolver
    {
        public readonly struct Result
        {
            public readonly bool HasBase;
            public readonly CameraId WinningBase;

            public Result(bool hasBase, CameraId winningBase)
            {
                HasBase = hasBase;
                WinningBase = winningBase;
            }
        }

        // outEnabled : final enabled set (winning base + all overlays).
        // outStack   : overlay ids sorted by depth ascending; they go into the winning base's stack.
        public static Result Resolve(
            IReadOnlyList<CameraSlot> slots,
            List<CameraId> outEnabled,
            List<CameraId> outStack)
        {
            outEnabled.Clear();
            outStack.Clear();

            var hasBase = false;
            CameraId winningBase = default;
            var winningBaseDepth = float.PositiveInfinity;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.RenderType != UnityEngine.Rendering.Universal.CameraRenderType.Base)
                {
                    continue;
                }
                if (!hasBase || s.Depth < winningBaseDepth)
                {
                    hasBase = true;
                    winningBase = s.Id;
                    winningBaseDepth = s.Depth;
                }
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.RenderType == UnityEngine.Rendering.Universal.CameraRenderType.Base)
                {
                    if (hasBase && s.Id.Equals(winningBase))
                    {
                        outEnabled.Add(s.Id);
                    }
                    continue;
                }
                outEnabled.Add(s.Id);

                // Insertion-sort overlays by depth ascending. Overlay counts are tiny in practice,
                // so this avoids both the closure allocation of List.Sort(Comparison) and the need
                // for a parallel depth buffer.
                var insertAt = outStack.Count;
                while (insertAt > 0 && FindDepth(slots, outStack[insertAt - 1]) > s.Depth)
                {
                    insertAt--;
                }
                outStack.Insert(insertAt, s.Id);
            }

            return new Result(hasBase, winningBase);
        }

        static float FindDepth(IReadOnlyList<CameraSlot> slots, CameraId id)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Id.Equals(id))
                {
                    return slots[i].Depth;
                }
            }
            return 0f;
        }
    }
}
