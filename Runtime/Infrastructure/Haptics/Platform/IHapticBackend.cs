using System;
using StickerFwk.Core.Haptics;

namespace StickerFwk.Infrastructure.Haptics.Platform
{
    internal interface IHapticBackend : IDisposable
    {
        bool IsSupported { get; }
        void PlayOneShot(in HapticPattern pattern, float intensityScale);
    }
}
