using StickerFwk.Core;

namespace StickerFwk.Infrastructure.Camera
{
    public readonly struct CameraRegisteredEvent
    {
        public CameraId CameraId { get; }
        public bool IsRegistered { get; }

        public CameraRegisteredEvent(CameraId cameraId, bool isRegistered)
        {
            CameraId = cameraId;
            IsRegistered = isRegistered;
        }
    }
}
