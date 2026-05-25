using System;

namespace StickerFwk.Core.Haptics
{
    public readonly struct HapticPattern
    {
        public HapticPattern(
            string name,
            float durationSeconds,
            HapticPatternCurve intensityCurve,
            HapticPatternCurve sharpnessCurve,
            HapticPresetId presetHint = HapticPresetId.None)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("HapticPattern Name must be non-empty.", nameof(name));

            var clampedDuration = durationSeconds;
            if (clampedDuration <= 0f)
            {
                Log.Warning("HapticService",
                    $"Pattern '{name}' DurationSeconds must be > 0 (got {durationSeconds:F3}). Clamping to 0.05.");
                clampedDuration = 0.05f;
            }
            else if (clampedDuration > 5f)
            {
                Log.Warning("HapticService",
                    $"Pattern '{name}' DurationSeconds must be <= 5 (got {durationSeconds:F3}). Clamping to 5.0.");
                clampedDuration = 5f;
            }

            Name = name;
            DurationSeconds = clampedDuration;
            IntensityCurve = intensityCurve;
            SharpnessCurve = sharpnessCurve;
            PresetHint = presetHint;
        }

        public string Name { get; }
        public float DurationSeconds { get; }
        public HapticPatternCurve IntensityCurve { get; }
        public HapticPatternCurve SharpnessCurve { get; }
        public HapticPresetId PresetHint { get; }
    }
}
