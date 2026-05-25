using System.Collections.Generic;
using StickerFwk.Core.Haptics;

namespace StickerFwk.Infrastructure.Haptics
{
    public interface IHapticData
    {
        string Name { get; }
        float DurationSeconds { get; }
        IReadOnlyList<HapticCurvePoint> IntensityCurvePoints { get; }
        IReadOnlyList<HapticCurvePoint> SharpnessCurvePoints { get; }
        HapticPresetId PresetHint { get; }
    }
}
