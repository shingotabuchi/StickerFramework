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
        bool TryGetCamera(CameraId id, out Camera camera);
        Camera GetRequiredCamera(CameraId id);
        bool IsRegistered(CameraId id);
        IReadOnlyCollection<CameraId> GetRegisteredIds();

        // Base-swap stack
        CameraId ActiveBase { get; }
        event Action<ActiveBaseChangedEvent> ActiveBaseChanged;
        void SetDefaultBase(CameraId id);
        IDisposable PushBase(CameraId id);

        // Overlay leases (overlays default enabled)
        IDisposable DisableOverlay(CameraId id);
    }
}
