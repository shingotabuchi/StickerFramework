using UnityEngine;

namespace StickerFwk.Core
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CameraFovLerper : MonoBehaviour
    {
        [SerializeField] UnityEngine.Camera _camera;
        [SerializeField] float _startFov = 60f;
        [SerializeField] float _endFov = 45f;
        [SerializeField, Range(0f, 1f)] float _time;
        [SerializeField] AnimationCurve _curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float StartFov
        {
            get => _startFov;
            set => _startFov = Mathf.Max(0.01f, value);
        }

        public float EndFov
        {
            get => _endFov;
            set => _endFov = Mathf.Max(0.01f, value);
        }

        public float Time
        {
            get => _time;
            set => SetNormalizedTime(value);
        }

        void Reset()
        {
            if (_camera == null)
            {
                _camera = GetComponent<UnityEngine.Camera>();
            }

            if (_camera != null)
            {
                _startFov = _camera.fieldOfView;
            }
        }

        void Awake()
        {
            Apply(_time);
        }

        void OnEnable()
        {
            EnsureCamera();
            Apply(_time);
        }

        void OnValidate()
        {
            EnsureCamera();

            _startFov = Mathf.Max(0.01f, _startFov);
            _endFov = Mathf.Max(0.01f, _endFov);
            _time = Mathf.Clamp01(_time);

            if (_curve == null || _curve.length == 0)
            {
                _curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }

            Apply(_time);
        }

        public void OnDidApplyAnimationProperties()
        {
            Apply(_time);
        }

        public void SetNormalizedTime(float normalizedTime)
        {
            _time = Mathf.Clamp01(normalizedTime);
            Apply(_time);
        }

        public void SetFov(float fov)
        {
            if (_camera == null)
            {
                return;
            }

            _camera.fieldOfView = Mathf.Max(0.01f, fov);
        }

        public void SetFovs(float startFov, float endFov)
        {
            _startFov = Mathf.Max(0.01f, startFov);
            _endFov = Mathf.Max(0.01f, endFov);
            Apply(_time);
        }

        void Apply(float normalizedTime)
        {
            if (_camera == null)
            {
                return;
            }

            var t = Mathf.Clamp01(normalizedTime);
            var eased = _curve == null ? t : _curve.Evaluate(t);
            _camera.fieldOfView = Mathf.LerpUnclamped(_startFov, _endFov, eased);
        }

        void EnsureCamera()
        {
            if (_camera == null)
            {
                _camera = GetComponent<UnityEngine.Camera>();
            }
        }
    }
}
