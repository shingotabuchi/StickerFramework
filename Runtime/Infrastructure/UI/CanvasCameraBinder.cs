using System;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Infrastructure.UI
{
    // Binds a scene-authored Canvas to a camera registered with ICameraService.
    // Starts in ScreenSpaceOverlay so authored UI is visible immediately at boot,
    // then swaps to ScreenSpaceCamera once the target CameraId is registered. Re-binds
    // whenever the backing camera is re-registered (e.g. on CameraProfile changes).
    [RequireComponent(typeof(Canvas))]
    public sealed class CanvasCameraBinder : MonoBehaviour
    {
        [SerializeField] CameraId _cameraId = CameraId.UI;
        [SerializeField] int _planeDistance = 1;

        Canvas _canvas;
        ICameraService _cameraService;
        IDisposable _subscription;
        bool _injected;

        [Inject]
        public void Construct(
            ICameraService cameraService,
            ISubscriber<CameraRegisteredEvent> cameraRegisteredSubscriber)
        {
            _cameraService = cameraService;
            _subscription?.Dispose();
            _subscription = cameraRegisteredSubscriber?.Subscribe(OnCameraRegistered);
            _injected = true;

            if (_canvas != null)
            {
                TryBind();
            }
        }

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas.renderMode != RenderMode.ScreenSpaceCamera || _canvas.worldCamera == null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        void Start()
        {
            if (_injected)
            {
                TryBind();
            }
        }

        void OnDestroy()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        void OnCameraRegistered(CameraRegisteredEvent e)
        {
            if (e.CameraId != _cameraId)
            {
                return;
            }

            if (!e.IsRegistered)
            {
                if (_canvas != null)
                {
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    _canvas.worldCamera = null;
                }
                return;
            }

            TryBind();
        }

        void TryBind()
        {
            if (_cameraService == null || _canvas == null)
            {
                return;
            }

            if (!_cameraService.TryGetCamera(_cameraId, out var camera) || camera == null)
            {
                return;
            }

            if (_canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }
            if (_canvas.worldCamera != camera)
            {
                _canvas.worldCamera = camera;
            }
            if (_canvas.planeDistance != _planeDistance)
            {
                _canvas.planeDistance = _planeDistance;
            }
        }
    }
}
