using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core
{
    public interface ICameraService
    {
        void Register(CameraId id, Camera camera);
        void Unregister(CameraId id);
        bool IsRegistered(CameraId id);
        Camera GetCamera(CameraId id);
        bool TryGetCamera(CameraId id, out Camera camera);
        Camera GetRequiredCamera(CameraId id);
        Camera GetCameraForRenderer(Renderer renderer);
        Camera GetCameraForGameObject(GameObject gameObject);
        IReadOnlyList<CameraId> GetRegisteredIds();
    }
}
