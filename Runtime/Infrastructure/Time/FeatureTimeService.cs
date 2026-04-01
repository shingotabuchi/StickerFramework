using StickerFwk.Core;

namespace StickerFwk.Infrastructure.Time
{
    public class FeatureTimeService : ITimeService
    {
        private readonly TimeService _rootTime;

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

        public float LocalTimeScale { get; set; } = 1f;
        public int FrameCount => _rootTime.FrameCount;
    }
}
