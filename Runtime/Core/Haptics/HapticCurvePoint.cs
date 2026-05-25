using UnityEngine;

namespace StickerFwk.Core.Haptics
{
    public readonly struct HapticCurvePoint
    {
        public HapticCurvePoint(float timeSeconds, float value)
        {
            TimeSeconds = timeSeconds;
            Value = Mathf.Clamp01(value);
        }

        public float TimeSeconds { get; }
        public float Value { get; }
    }
}
