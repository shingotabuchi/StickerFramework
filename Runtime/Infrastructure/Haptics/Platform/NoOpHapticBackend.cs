using StickerFwk.Core.Haptics;

namespace StickerFwk.Infrastructure.Haptics.Platform
{
    internal sealed class NoOpHapticBackend : IHapticBackend
    {
        public bool IsSupported => false;

        public void PlayOneShot(in HapticPattern pattern, float intensityScale)
        {
        }

        public void Dispose()
        {
        }
    }
}
