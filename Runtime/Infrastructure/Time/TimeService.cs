using StickerFwk.Core;

namespace StickerFwk.Infrastructure.Time
{
    public class TimeService : ITimeService
    {
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public float Time => UnityEngine.Time.time;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;

        public float TimeScale
        {
            get => UnityEngine.Time.timeScale;
            set => UnityEngine.Time.timeScale = value;
        }

        public float LocalTimeScale { get; set; } = 1f;

        public int FrameCount => UnityEngine.Time.frameCount;
    }
}
