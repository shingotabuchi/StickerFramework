using System.Collections.Generic;

namespace StickerFwk.Core
{
    // Snapshot of one camera registered through the active profiles. Pure data so
    // CameraStackResolver can be unit-tested without Unity.
    public readonly struct CameraSlot
    {
        public readonly CameraId Id;
        public readonly float Depth;

        public CameraSlot(CameraId id, float depth)
        {
            Id = id;
            Depth = depth;
        }
    }

    // Decides which camera is the Base and how the Base's overlay stack is composed.
    //
    // Rules:
    //   1. Every slot supplied is "wanted" (slots come from active profiles only — there is no
    //      additional gate like mode or lease).
    //   2. The lowest-depth slot becomes the Base. All other slots become Overlays.
    //   3. Overlays are sorted by depth ascending and inserted into the Base's stack.
    //
    // The Base/Overlay role is therefore implicit in the depth ordering — there is no per-camera
    // "render type" authored in CameraSystemSettings.
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

            if (slots.Count == 0)
            {
                return new Result(false, default);
            }

            var winningIndex = 0;
            var winningDepth = slots[0].Depth;
            for (var i = 1; i < slots.Count; i++)
            {
                if (slots[i].Depth < winningDepth)
                {
                    winningDepth = slots[i].Depth;
                    winningIndex = i;
                }
            }

            var winningBase = slots[winningIndex].Id;
            outEnabled.Add(winningBase);

            for (var i = 0; i < slots.Count; i++)
            {
                if (i == winningIndex)
                {
                    continue;
                }

                var s = slots[i];
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

            return new Result(true, winningBase);
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
