using StickerFwk.Core.Haptics;

namespace StickerFwk.Infrastructure.Haptics
{
    public sealed class DefaultHapticProfile : HapticProfile
    {
        public DefaultHapticProfile()
            : base(new[]
            {
                CreateConstant(
                    HapticPresets.Selection,
                    0.05f,
                    0.6f,
                    0.4f,
                    HapticPresetId.Selection),
                CreateConstant(
                    HapticPresets.LightImpact,
                    0.05f,
                    0.5f,
                    0.5f,
                    HapticPresetId.LightImpact),
                CreateConstant(
                    HapticPresets.MediumImpact,
                    0.1f,
                    0.75f,
                    0.5f,
                    HapticPresetId.MediumImpact),
                CreateConstant(
                    HapticPresets.HeavyImpact,
                    0.1f,
                    1f,
                    0.5f,
                    HapticPresetId.HeavyImpact),
                CreateConstant(
                    HapticPresets.RigidImpact,
                    0.1f,
                    0.9f,
                    1f,
                    HapticPresetId.RigidImpact),
                CreateConstant(
                    HapticPresets.SoftImpact,
                    0.1f,
                    0.6f,
                    0.1f,
                    HapticPresetId.SoftImpact),
                CreatePattern(
                    HapticPresets.Success,
                    0.15f,
                    new[]
                    {
                        new HapticCurvePoint(0f, 0.5f),
                        new HapticCurvePoint(0.05f, 1f),
                        new HapticCurvePoint(0.15f, 0.7f),
                    },
                    new[]
                    {
                        new HapticCurvePoint(0f, 0.4f),
                        new HapticCurvePoint(0.15f, 0.6f),
                    },
                    HapticPresetId.Success),
                CreatePattern(
                    HapticPresets.Warning,
                    0.15f,
                    new[]
                    {
                        new HapticCurvePoint(0f, 0.8f),
                        new HapticCurvePoint(0.075f, 0.3f),
                        new HapticCurvePoint(0.15f, 0.8f),
                    },
                    new[]
                    {
                        new HapticCurvePoint(0f, 0.7f),
                        new HapticCurvePoint(0.15f, 0.7f),
                    },
                    HapticPresetId.Warning),
                CreatePattern(
                    HapticPresets.Error,
                    0.15f,
                    new[]
                    {
                        new HapticCurvePoint(0f, 1f),
                        new HapticCurvePoint(0.05f, 0.4f),
                        new HapticCurvePoint(0.1f, 1f),
                        new HapticCurvePoint(0.15f, 0.4f),
                    },
                    new[]
                    {
                        new HapticCurvePoint(0f, 0.9f),
                        new HapticCurvePoint(0.15f, 0.9f),
                    },
                    HapticPresetId.Error),
            })
        {
        }

        private static HapticPattern CreateConstant(
            string name,
            float durationSeconds,
            float intensity,
            float sharpness,
            HapticPresetId presetId)
        {
            return CreatePattern(
                name,
                durationSeconds,
                new[]
                {
                    new HapticCurvePoint(0f, intensity),
                    new HapticCurvePoint(durationSeconds, intensity),
                },
                new[]
                {
                    new HapticCurvePoint(0f, sharpness),
                    new HapticCurvePoint(durationSeconds, sharpness),
                },
                presetId);
        }

        private static HapticPattern CreatePattern(
            string name,
            float durationSeconds,
            HapticCurvePoint[] intensityPoints,
            HapticCurvePoint[] sharpnessPoints,
            HapticPresetId presetId)
        {
            return new HapticPattern(
                name,
                durationSeconds,
                new HapticPatternCurve(intensityPoints),
                new HapticPatternCurve(sharpnessPoints),
                presetId);
        }
    }
}
