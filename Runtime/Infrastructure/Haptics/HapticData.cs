using System;
using System.Collections.Generic;
using StickerFwk.Core.Haptics;
using UnityEngine;

namespace StickerFwk.Infrastructure.Haptics
{
    [Serializable]
    public class HapticData : IHapticData
    {
        [SerializeField] private string _name = string.Empty;
        [SerializeField] private float _durationSeconds = 0.1f;
        [SerializeField] private List<SerializableHapticCurvePoint> _intensityCurvePoints = new();
        [SerializeField] private List<SerializableHapticCurvePoint> _sharpnessCurvePoints = new();
        [SerializeField] private HapticPresetId _presetHint = HapticPresetId.None;

        private HapticCurvePoint[] _cachedIntensity;
        private HapticCurvePoint[] _cachedSharpness;

        public string Name => _name;
        public float DurationSeconds => _durationSeconds;
        public HapticPresetId PresetHint => _presetHint;

        public IReadOnlyList<HapticCurvePoint> IntensityCurvePoints
        {
            get
            {
                if (_cachedIntensity == null || _cachedIntensity.Length != (_intensityCurvePoints?.Count ?? 0))
                    _cachedIntensity = Convert(_intensityCurvePoints);
                return _cachedIntensity;
            }
        }

        public IReadOnlyList<HapticCurvePoint> SharpnessCurvePoints
        {
            get
            {
                if (_cachedSharpness == null || _cachedSharpness.Length != (_sharpnessCurvePoints?.Count ?? 0))
                    _cachedSharpness = Convert(_sharpnessCurvePoints);
                return _cachedSharpness;
            }
        }

        private static HapticCurvePoint[] Convert(List<SerializableHapticCurvePoint> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<HapticCurvePoint>();
            var result = new HapticCurvePoint[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = new HapticCurvePoint(source[i].TimeSeconds, source[i].Value);
            }
            return result;
        }
    }
}
