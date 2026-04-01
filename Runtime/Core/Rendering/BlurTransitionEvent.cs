using StickerFwk.Core.Animation;

namespace StickerFwk.Core.Rendering
{
    public readonly struct BlurTransitionEvent
    {
        public bool Enabled { get; }
        public EaseType Ease { get; }
        public float Duration { get; }

        public BlurTransitionEvent(bool enabled, EaseType ease, float duration)
        {
            Enabled = enabled;
            Ease = ease;
            Duration = duration;
        }

        public BlurTransitionEvent(bool enabled)
        {
            Enabled = enabled;
            Ease = EaseType.Linear;
            Duration = 0f;
        }
    }
}
