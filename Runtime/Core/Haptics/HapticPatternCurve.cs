using System;
using System.Collections.Generic;

namespace StickerFwk.Core.Haptics
{
    public readonly struct HapticPatternCurve
    {
        private readonly HapticCurvePoint[] _points;

        public HapticPatternCurve(IReadOnlyList<HapticCurvePoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("HapticPatternCurve requires at least one point.", nameof(points));

            _points = new HapticCurvePoint[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                _points[i] = points[i];
            }
        }

        public IReadOnlyList<HapticCurvePoint> Points => _points ?? Array.Empty<HapticCurvePoint>();

        public float Evaluate(float timeSeconds)
        {
            if (_points == null || _points.Length == 0) return 0f;
            if (_points.Length == 1) return _points[0].Value;
            if (timeSeconds <= _points[0].TimeSeconds) return _points[0].Value;

            var last = _points[_points.Length - 1];
            if (timeSeconds >= last.TimeSeconds) return last.Value;

            for (var i = 1; i < _points.Length; i++)
            {
                var hi = _points[i];
                if (timeSeconds > hi.TimeSeconds) continue;

                var lo = _points[i - 1];
                var span = hi.TimeSeconds - lo.TimeSeconds;
                if (span <= 0f) return hi.Value;

                var t = (timeSeconds - lo.TimeSeconds) / span;
                return lo.Value + (hi.Value - lo.Value) * t;
            }

            return last.Value;
        }
    }
}
