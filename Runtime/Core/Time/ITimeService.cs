namespace StickerFwk.Core
{
    public interface ITimeService
    {
        float DeltaTime { get; }
        float UnscaledDeltaTime { get; }
        float FixedDeltaTime { get; }
        float Time { get; }
        float UnscaledTime { get; }
        float TimeScale { get; set; }
        float LocalTimeScale { get; set; }
        int FrameCount { get; }
    }
}
