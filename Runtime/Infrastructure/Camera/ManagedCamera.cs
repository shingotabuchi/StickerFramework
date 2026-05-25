using System;
using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Infrastructure.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class ManagedCamera : MonoBehaviour
    {
        [SerializeField] CameraId _cameraId;

        ICameraService _cameraService;
        UnityEngine.Camera _camera;
        CameraId _effectiveId;
        bool _isRegistered;

        public CameraId CameraId => GetEffectiveId();

        [Inject]
        public void Construct(ICameraService cameraService)
        {
            _cameraService = cameraService;
            TryRegister();
        }

        void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            GetEffectiveId();
        }

        void OnDisable()
        {
            if (_isRegistered && _cameraService != null)
            {
                var id = GetEffectiveId();
                if (_cameraService.TryGetCamera(id, out var registeredCamera) && registeredCamera == _camera)
                {
                    _cameraService.Unregister(id);
                }
                _isRegistered = false;
            }
        }

        void OnEnable()
        {
            TryRegister();
        }

        CameraId GetEffectiveId()
        {
            if (_cameraId.IsValid)
            {
                return _cameraId;
            }

            if (!_effectiveId.IsValid)
            {
                _effectiveId = new CameraId($"anon:{Guid.NewGuid():N}");
            }

            return _effectiveId;
        }

        void TryRegister()
        {
            if (_camera == null)
            {
                _camera = GetComponent<UnityEngine.Camera>();
            }

            if (_cameraService == null || _camera == null || !isActiveAndEnabled)
            {
                return;
            }

            var id = GetEffectiveId();
            _cameraService.Register(id, _camera);
            _isRegistered = _cameraService.TryGetCamera(id, out var registeredCamera) && registeredCamera == _camera;
        }
    }
}
