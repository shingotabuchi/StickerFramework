using System;
using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core.UI
{
    // TEMPORARY (Step 4 stop-gap): binds a scene-authored Canvas to a script-created camera
    // and holds a usage lease so the camera stays enabled. To be removed in Step 7 when the
    // gameplay UI moves to an Addressable framework window.
    [RequireComponent(typeof(Canvas))]
    public class CanvasCameraBinder : MonoBehaviour
    {
        [SerializeField] CameraId _cameraId = CameraId.UI;
        [SerializeField] float _planeDistance = 10f;

        Canvas _canvas;
        IDisposable _lease;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        [Inject]
        public void Construct(ICameraService cameraService, ICameraUsageService usageService)
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            // Acquire first so the target camera is guaranteed active before we bind to it.
            _lease = usageService.Acquire(_cameraId);

            if (cameraService.TryGetCamera(_cameraId, out var camera) && camera != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = camera;
                _canvas.planeDistance = _planeDistance;
            }
            else
            {
                Log.Warning($"[CanvasCameraBinder] Camera '{_cameraId}' not registered when binding canvas '{name}'.");
            }
        }

        void OnDestroy()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }
}
