using System;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core
{
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] private CameraId[] _cameraIds;
        [SerializeField] private float _width = 5f;
        [SerializeField] private float _height = 5f;
        [SerializeField] private float _safeHeightMultiplier = 1f;

        private ICameraService _cameraService;
        private ISubscriber<ScreenChangedEvent> _subscriber;
        private IDisposable _subscription;

        private void OnEnable()
        {
            if (_subscriber == null)
            {
                return;
            }

            _subscription?.Dispose();
            _subscription = _subscriber.Subscribe(_ => Apply());
            Apply();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
        }

        [Inject]
        public void Construct(ICameraService cameraService, ISubscriber<ScreenChangedEvent> subscriber)
        {
            _cameraService = cameraService;
            _subscription?.Dispose();
            _subscriber = subscriber;
            _subscription = _subscriber.Subscribe(_ => Apply());
        }

        private void Apply()
        {
            if (_cameraIds == null || _cameraIds.Length == 0 || _cameraService == null)
            {
                return;
            }

            Log.Info($"CameraFitter: Apply - width: {_width}, height: {_height}");

            for (var i = 0; i < _cameraIds.Length; i++)
            {
                if (_cameraService.TryGetCamera(_cameraIds[i], out var cam) && cam != null)
                {
                    cam.FitToArea(_width, _height, safeHeightMultiplier: _safeHeightMultiplier);
                }
            }
        }
    }
}
