using System;

namespace StickerFwk.Core
{
    public interface ICameraModeService
    {
        CameraMode CurrentMode { get; }
        event Action<CameraMode> ModeChanged;
        void SetMode(CameraMode mode);
        bool ModeIncludes(CameraMode mode, CameraId id);
    }
}
