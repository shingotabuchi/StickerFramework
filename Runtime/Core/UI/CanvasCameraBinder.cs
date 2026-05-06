using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core.UI
{
    // TEMPORARY (Step 4 stop-gap): binds a scene-authored Canvas to a script-created camera.
    // To be removed in Step 7 when the gameplay UI moves to an Addressable framework window.
    [RequireComponent(typeof(Canvas))]
    public class CanvasCameraBinder : MonoBehaviour
    {
        [SerializeField] CameraId _cameraId = CameraId.UI;
        [SerializeField] float _planeDistance = 10f;

        Canvas _canvas;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        [Inject]
        public void Construct(ICameraService cameraService)
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

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
    }
}
