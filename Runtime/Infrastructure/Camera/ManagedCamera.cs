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
        bool _isRegistered;

        public CameraId CameraId => _cameraId;

        [Inject]
        public void Construct(ICameraService cameraService)
        {
            _cameraService = cameraService;
            TryRegister();
        }

        void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
        }

        void OnDisable()
        {
            if (_isRegistered && _cameraService != null)
            {
                _cameraService.Unregister(_cameraId);
                _isRegistered = false;
            }
        }

        void OnEnable()
        {
            TryRegister();
        }

        void TryRegister()
        {
            if (_cameraService == null || _camera == null || !isActiveAndEnabled)
            {
                return;
            }

            _cameraService.Register(_cameraId, _camera);
            _isRegistered = _cameraService.GetCamera(_cameraId) == _camera;
        }
    }
}
