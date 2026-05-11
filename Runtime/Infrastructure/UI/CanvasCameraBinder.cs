using System;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Infrastructure.UI
{
    /// <summary>
    /// Binds a scene-authored <see cref="Canvas"/> to a camera registered with
    /// <see cref="ICameraService"/>.
    /// </summary>
    /// <remarks>
    /// Starts in <see cref="RenderMode.ScreenSpaceOverlay"/> so the canvas is visible
    /// before any Base is pushed, then swaps to
    /// <see cref="RenderMode.ScreenSpaceCamera"/> when the configured
    /// <see cref="CameraId"/> is registered. Reverts to overlay if the camera
    /// unregisters, and re-binds when it is re-registered.
    /// Idempotent.
    /// <para>
    /// See <c>Runtime/Infrastructure/UI/README.md</c> §R11 for required scope wiring
    /// and layering rules.
    /// </para>
    /// </remarks>
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
