using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core
{
    public interface ICameraService
    {
        // Registration
        void Register(CameraId id, Camera camera);
        void Unregister(CameraId id);
        Camera GetCamera(CameraId id);
        bool TryGetCamera(CameraId id, out Camera camera);
        Camera GetRequiredCamera(CameraId id);
        Camera GetCameraForRenderer(Renderer renderer);
        bool IsRegistered(CameraId id);
        IReadOnlyList<CameraId> GetRegisteredIds();

        // Base-swap stack
        CameraId ActiveBase { get; }
        event Action<ActiveBaseChangedEvent> ActiveBaseChanged;
        void SetDefaultBase(CameraId id);
        IDisposable PushBase(CameraId id);

        // Overlay leases (overlays default enabled)
        IDisposable DisableOverlay(CameraId id);
    }
}
