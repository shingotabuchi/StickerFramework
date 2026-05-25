using System;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core
{
    /// <summary>
    /// Adjusts a perspective camera's vertical field of view so a configurable
    /// box (defined in this transform's local space) just fits inside the view
    /// at the current screen aspect. The box is editable in the inspector and
    /// drawn with gizmos.
    ///
    /// Re-fits on enable when configured to do so, whenever the screen size
    /// changes (via <see cref="ScreenChangedEvent"/> at runtime / OnValidate in
    /// the editor), and on demand through the "Apply" context-menu button.
    /// </summary>
    [ExecuteAlways]
    public sealed class CameraBoxFovFitter : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("Box center in this transform's local space.")]
        [SerializeField] private Vector3 _boxCenter = Vector3.zero;
        [Tooltip("Box size (width, height, depth) in this transform's local space.")]
        [SerializeField] private Vector3 _boxSize = Vector3.one;
        [Tooltip("Extra margin around the box (1 = exact fit, 1.1 = 10% padding).")]
        [SerializeField] private float _paddingScale = 1f;
        [SerializeField] private float _minFov = 1f;
        [SerializeField] private float _maxFov = 179f;
        [Tooltip("Apply immediately when this component is enabled. Disable when another component coordinates camera-dependent applies.")]
        [SerializeField] private bool _applyOnEnable = true;
        [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.9f, 1f, 1f);

        // Reused corner buffer; avoids per-call allocation.
        private readonly Vector3[] _corners = new Vector3[8];

        private ISubscriber<ScreenChangedEvent> _subscriber;
        private IDisposable _subscription;

        public Camera Camera
        {
            get => _camera;
            set { _camera = value; Apply(); }
        }

        public Vector3 BoxCenter
        {
            get => _boxCenter;
            set { _boxCenter = value; Apply(); }
        }

        public Vector3 BoxSize
        {
            get => _boxSize;
            set { _boxSize = value; Apply(); }
        }

        public bool ApplyOnEnable
        {
            get => _applyOnEnable;
            set => _applyOnEnable = value;
        }

        /// <summary>
        /// Returns the vertical field of view required to fit the provided box
        /// size, or NaN if the current camera setup cannot produce a valid
        /// result. Does not modify the serialized box size or the camera's
        /// field of view.
        /// </summary>
        public float GetFovForBoxSize(Vector3 boxSize)
        {
            return TryGetFovForBoxSize(boxSize, out var fov) ? fov : float.NaN;
        }

        /// <summary>
        /// Calculates the vertical field of view required to fit the provided
        /// box size using this fitter's current camera, transform, box center,
        /// padding, and FOV clamp settings. Does not modify the serialized box
        /// size or the camera's field of view.
        /// </summary>
        public bool TryGetFovForBoxSize(Vector3 boxSize, out float fov)
        {
            return TryGetFovForBox(_boxCenter, boxSize, out fov);
        }

        /// <summary>
        /// Calculates the vertical field of view required to fit the provided
        /// box using this fitter's current camera, transform, padding, and FOV
        /// clamp settings. Does not modify the serialized box or the camera's
        /// field of view.
        /// </summary>
        public bool TryGetFovForBox(Vector3 boxCenter, Vector3 boxSize, out float fov)
        {
            fov = 0f;

            if (_camera == null || _camera.orthographic)
            {
                return false;
            }

            var aspect = _camera.aspect;
            if (aspect <= 0f)
            {
                return false;
            }

            var camTransform = _camera.transform;
            var camPos = camTransform.position;
            var right = camTransform.right;
            var up = camTransform.up;
            var forward = camTransform.forward;

            FillWorldCorners(_corners, boxCenter, boxSize);

            if (!TryGetMaxHalfFovTangent(_corners, camPos, right, up, forward, aspect, out var maxTan))
            {
                return false;
            }

            maxTan *= Mathf.Max(0.0001f, _paddingScale);

            fov = 2f * Mathf.Atan(maxTan) * Mathf.Rad2Deg;
            fov = Mathf.Clamp(fov, _minFov, _maxFov);
            return true;
        }

        [Inject]
        public void Construct(ISubscriber<ScreenChangedEvent> subscriber)
        {
            _subscription?.Dispose();
            _subscriber = subscriber;
            _subscription = _subscriber.Subscribe(_ => Apply());
        }

        private void OnEnable()
        {
            if (_subscriber != null)
            {
                _subscription?.Dispose();
                _subscription = _subscriber.Subscribe(_ => Apply());
            }

            if (_applyOnEnable)
            {
                Apply();
            }
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            if (TryGetFovForBox(_boxCenter, _boxSize, out var fov))
            {
                _camera.fieldOfView = fov;
            }
        }

        private static bool TryGetMaxHalfFovTangent(
            Vector3[] corners,
            Vector3 camPos,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            float aspect,
            out float maxTan)
        {
            // Find the largest vertical half-FOV tangent required by any corner.
            // Vertical:   |y| <= z * tan(vHalf)
            // Horizontal: |x| <= z * tan(vHalf) * aspect  (Unity FOV is vertical)
            maxTan = 0f;
            for (var i = 0; i < corners.Length; i++)
            {
                var v = corners[i] - camPos;
                var z = Vector3.Dot(v, forward);
                if (z <= 0.0001f)
                {
                    // Corner is at/behind the camera; cannot be framed by FOV alone.
                    continue;
                }

                var x = Mathf.Abs(Vector3.Dot(v, right));
                var y = Mathf.Abs(Vector3.Dot(v, up));

                var tanFromHeight = y / z;
                var tanFromWidth = x / (z * aspect);
                var tan = Mathf.Max(tanFromHeight, tanFromWidth);
                if (tan > maxTan)
                {
                    maxTan = tan;
                }
            }

            return maxTan > 0f;
        }

        private void FillWorldCorners(Vector3[] buffer, Vector3 boxCenter, Vector3 boxSize)
        {
            var half = boxSize * 0.5f;
            var index = 0;
            for (var sx = -1; sx <= 1; sx += 2)
            {
                for (var sy = -1; sy <= 1; sy += 2)
                {
                    for (var sz = -1; sz <= 1; sz += 2)
                    {
                        var local = boxCenter + new Vector3(sx * half.x, sy * half.y, sz * half.z);
                        buffer[index++] = transform.TransformPoint(local);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Transform reads are safe here, but defer to be consistent with
            // other camera helpers and to pick up aspect after layout settles.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Apply();
                }
            };
        }

        private void OnDrawGizmosSelected()
        {
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireCube(_boxCenter, _boxSize);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
