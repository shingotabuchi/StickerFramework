using System;
using System.Collections.Generic;

namespace StickerFwk.Core
{
    // Snapshot of one camera's state used by CameraStackResolver. Built each frame from the
    // CameraProfileService registry + per-camera lease counts. Pure data — no Unity dependencies
    // so the resolver can be unit-tested in isolation.
    public readonly struct CameraSlot
    {
        public readonly CameraId Id;
        public readonly UnityEngine.Rendering.Universal.CameraRenderType RenderType;
        public readonly float Depth;
        public readonly CameraActivationPolicy ActivationPolicy;
        public readonly int LeaseCount;

        public CameraSlot(
            CameraId id,
            UnityEngine.Rendering.Universal.CameraRenderType renderType,
            float depth,
            CameraActivationPolicy activationPolicy,
            int leaseCount)
        {
            Id = id;
            RenderType = renderType;
            Depth = depth;
            ActivationPolicy = activationPolicy;
            LeaseCount = leaseCount;
        }
    }

    // Decides which cameras render and how the base camera's overlay stack is composed.
    //
    // Rules:
    //   1. A slot is "wanted" iff (a) the active mode includes its CameraId AND
    //      (b) its activation policy is AlwaysOn or its lease count is > 0.
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

        // outEnabled : final enabled set (winning base + wanted overlays).
        // outStack   : overlay ids sorted by depth ascending; they go into the winning base's stack.
        public static Result Resolve(
            IReadOnlyList<CameraSlot> slots,
            CameraMode mode,
            Func<CameraMode, CameraId, bool> modeIncludes,
            List<CameraId> outEnabled,
            List<CameraId> outStack)
        {
            outEnabled.Clear();
            outStack.Clear();

            var hasBase = false;
            CameraId winningBase = default;
            var winningBaseDepth = float.PositiveInfinity;

            // Pass 1: pick winning base.
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.RenderType != UnityEngine.Rendering.Universal.CameraRenderType.Base)
                {
                    continue;
                }
                if (!IsWanted(s, mode, modeIncludes))
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

            // Pass 2: collect overlays + emit enabled set.
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
                if (!IsWanted(s, mode, modeIncludes))
                {
                    continue;
                }
                outEnabled.Add(s.Id);
                outStack.Add(s.Id);
            }

            // Sort overlays by depth using the source slot list. Linear scan in Compare is OK
            // because overlay counts are tiny (single digits in practice).
            outStack.Sort((a, b) =>
            {
                var da = FindDepth(slots, a);
                var db = FindDepth(slots, b);
                return da.CompareTo(db);
            });

            return new Result(hasBase, winningBase);
        }

        static bool IsWanted(CameraSlot s, CameraMode mode, Func<CameraMode, CameraId, bool> modeIncludes)
        {
            if (!modeIncludes(mode, s.Id))
            {
                return false;
            }
            if (s.ActivationPolicy == CameraActivationPolicy.AlwaysOn)
            {
                return true;
            }
            return s.LeaseCount > 0;
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
