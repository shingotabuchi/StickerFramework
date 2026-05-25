using UnityEngine;

namespace StickerFwk.Core
{
    /// <summary>
    /// Orients and scales a quad so it sits perpendicular to a camera at a fixed
    /// distance and fully fills (covers) the camera's view, acting as a camera
    /// background. Intended to be placed on a primitive Quad GameObject.
    ///
    /// Following the camera is handled by parenting (no per-frame Update). Call
    /// <see cref="Apply"/> after changing the camera's FOV/orthographic size at
    /// runtime if you need the fit recomputed.
    /// </summary>
    [ExecuteAlways]
    public sealed class CameraBackgroundQuad : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _distance = 10f;
        [Tooltip("Reparent the quad under the camera so it follows it automatically.")]
        [SerializeField] private bool _parentToCamera = true;
        [Tooltip("Match the camera's current aspect (exact fill). Disable to use a fixed content aspect.")]
        [SerializeField] private bool _useCameraAspect = true;
        [Tooltip("Content aspect ratio (width / height) used when 'Use Camera Aspect' is off. The quad is scaled to cover the view at this aspect.")]
        [SerializeField] private float _aspect = 16f / 9f;
        [Tooltip("Apply immediately when this component is enabled. Disable when another component coordinates camera-dependent applies.")]
        [SerializeField] private bool _applyOnEnable = true;

        public Camera Camera
        {
            get => _camera;
            set { _camera = value; Apply(); }
        }

        public float Distance
        {
            get => _distance;
            set { _distance = value; Apply(); }
        }

        public float Aspect
        {
            get => _aspect;
            set { _aspect = value; Apply(); }
        }

        public bool ApplyOnEnable
        {
            get => _applyOnEnable;
            set => _applyOnEnable = value;
        }

        private void OnEnable()
        {
            if (_applyOnEnable)
            {
                Apply();
            }
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            if (_camera == null)
            {
                return;
            }

            var camTransform = _camera.transform;

            if (_parentToCamera)
            {
                if (transform.parent != camTransform)
                {
                    transform.SetParent(camTransform, worldPositionStays: false);
                }

                transform.localPosition = new Vector3(0f, 0f, _distance);
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.position = camTransform.position + camTransform.forward * _distance;
                transform.rotation = camTransform.rotation;
            }

            float viewHeight;
            if (_camera.orthographic)
            {
                viewHeight = _camera.orthographicSize * 2f;
            }
            else
            {
                viewHeight = 2f * _distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }

            var viewWidth = viewHeight * _camera.aspect;

            var contentAspect = _useCameraAspect ? _camera.aspect : _aspect;
            if (contentAspect <= 0f)
            {
                contentAspect = _camera.aspect;
            }

            // Cover the view: start matching height, grow if the width falls short.
            var height = viewHeight;
            var width = height * contentAspect;
            if (width < viewWidth)
            {
                width = viewWidth;
                height = width / contentAspect;
            }

            transform.localScale = new Vector3(width, height, 1f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Reparenting/transform writes are illegal during OnValidate; defer one tick.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Apply();
                }
            };
        }
#endif
    }
}
