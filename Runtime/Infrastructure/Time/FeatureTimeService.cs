using System;
using StickerFwk.Core;

namespace StickerFwk.Infrastructure.Time
{
    public class FeatureTimeService : ITimeService
    {
        private readonly TimeService _rootTime;
        private float _localTimeScale = 1f;

        public event Action<float> LocalTimeScaleChanged;

        public FeatureTimeService(TimeService rootTime)
        {
            _rootTime = rootTime;
        }

        public float DeltaTime => _rootTime.DeltaTime * LocalTimeScale;
        public float UnscaledDeltaTime => _rootTime.UnscaledDeltaTime;
        public float FixedDeltaTime => _rootTime.FixedDeltaTime * LocalTimeScale;
        public float Time => _rootTime.Time;
        public float UnscaledTime => _rootTime.UnscaledTime;

        public float TimeScale
        {
            get => _rootTime.TimeScale;
            set => _rootTime.TimeScale = value;
        }

        public float LocalTimeScale
        {
            get => _localTimeScale;
            set
            {
                if (_localTimeScale == value)
                {
                    return;
                }

                _localTimeScale = value;
                LocalTimeScaleChanged?.Invoke(value);
            }
        }

        public int FrameCount => _rootTime.FrameCount;
    }
}
