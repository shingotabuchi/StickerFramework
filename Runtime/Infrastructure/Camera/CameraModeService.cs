using System;
using StickerFwk.Core;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraModeService : ICameraModeService
    {
        CameraMode _currentMode = CameraMode.Gameplay;

        public CameraMode CurrentMode => _currentMode;

        public event Action<CameraMode> ModeChanged;

        public void SetMode(CameraMode mode)
        {
            if (_currentMode == mode)
            {
                return;
            }

            _currentMode = mode;
            Log.Info($"[CameraModeService] Mode -> {mode}");
            ModeChanged?.Invoke(mode);
        }

        public bool ModeIncludes(CameraMode mode, CameraId id)
        {
            // Background is the always-available fallback base camera (declared by the Root
            // profile). It must be permitted in every mode so the screen never goes blank.
            if (id == CameraId.Background)
            {
                return true;
            }

            switch (mode)
            {
                case CameraMode.Gameplay:
                    return id == CameraId.World
                        || id == CameraId.UI
                        || id == CameraId.WorldOverlay;
                case CameraMode.GameplayModal:
                    return id == CameraId.World
                        || id == CameraId.UI
                        || id == CameraId.WorldOverlay
                        || id == CameraId.UIOverlay;
                case CameraMode.Transition:
                    return id == CameraId.World
                        || id == CameraId.UI
                        || id == CameraId.WorldOverlay
                        || id == CameraId.UIOverlay
                        || id == CameraId.Wipe;
                default:
                    return false;
            }
        }
    }
}
