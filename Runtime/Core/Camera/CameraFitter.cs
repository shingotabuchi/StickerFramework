using System;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core
{
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] private Camera[] _cameras;
        [SerializeField] private float _width = 5f;
        [SerializeField] private float _height = 5f;
        [SerializeField] private float _safeHeightMultiplier = 1f;

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
        public void Construct(ISubscriber<ScreenChangedEvent> subscriber)
        {
            _subscription?.Dispose();
            _subscriber = subscriber;
            _subscription = _subscriber.Subscribe(_ => Apply());
        }

        private void Apply()
        {
            if (_cameras == null || _cameras.Length == 0)
            {
                return;
            }

            Log.Info($"CameraFitter: Apply - width: {_width}, height: {_height}");

            foreach (var cam in _cameras)
            {
                if (cam != null)
                {
                    cam.FitToArea(_width, _height, safeHeightMultiplier: _safeHeightMultiplier);
                }
            }
        }
    }
}