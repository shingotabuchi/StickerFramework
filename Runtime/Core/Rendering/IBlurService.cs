using System;
using StickerFwk.Core.Animation;
using UnityEngine.Rendering;

namespace StickerFwk.Core.Rendering
{
    public interface IBlurService
    {
        bool IsBlurred { get; }
        void Register(Volume volume);
        void Unregister(Volume volume);
        IDisposable Request(EaseType ease, float duration);
        IDisposable Request(EaseType onEase, float onDuration, EaseType offEase, float offDuration);
        void SetBlurDirty();
        void SetManualUpdate(bool enabled);
    }
}
