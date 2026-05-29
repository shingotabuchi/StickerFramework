using System;
using UnityEngine.Playables;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    /// Playable behaviour for a single time-scale clip. Carries the speed multiplier this clip
    /// applies to the whole timeline while it is active.
    /// </summary>
    [Serializable]
    public class TimeScaleClipBehaviour : PlayableBehaviour
    {
        public float Speed = 1f;
    }
}
