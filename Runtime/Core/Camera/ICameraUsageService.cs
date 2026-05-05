using System;

namespace StickerFwk.Core
{
    public interface ICameraUsageService
    {
        IDisposable Acquire(CameraId cameraId);
        bool IsActive(CameraId cameraId);
    }
}
