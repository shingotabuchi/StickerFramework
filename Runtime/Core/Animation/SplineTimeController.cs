using System.Globalization;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StickerFwk.Core
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineAnimate))]
    [AddComponentMenu("Sticker Framework/Animation/Spline Time Controller")]
    public sealed class SplineTimeController : MonoBehaviour
    {
        [SerializeField] private SplineAnimate splineAnimate;
        [SerializeField][Min(0f)] private float normalizedTime;
        [SerializeField][Range(0f, 1f)] private float weight = 1f;
        [SerializeField] private bool pauseSplineAnimate = true;
        [SerializeField][HideInInspector] private Vector3 zeroWeightLocalPosition;
        [SerializeField][HideInInspector] private Quaternion zeroWeightLocalRotation = Quaternion.identity;
        [SerializeField][HideInInspector] private Vector3 zeroWeightLocalScale = Vector3.one;
        [SerializeField][HideInInspector] private bool hasZeroWeightPose;

        public SplineAnimate SplineAnimate
        {
            get => splineAnimate;
            set
            {
                splineAnimate = value;
                ApplyNormalizedTime();
            }
        }

        public float NormalizedTime
        {
            get => normalizedTime;
            set
            {
                normalizedTime = Mathf.Max(0f, value);
                ApplyNormalizedTime();
            }
        }

        public float Weight
        {
            get => weight;
            set
            {
                weight = Mathf.Clamp01(value);
                ApplyNormalizedTime();
            }
        }

        public bool PauseSplineAnimate
        {
            get => pauseSplineAnimate;
            set
            {
                pauseSplineAnimate = value;
                ApplyNormalizedTime();
            }
        }

        private void Reset()
        {
            EnsureSplineAnimate();
            CaptureZeroWeightPose();
            ApplyNormalizedTime();
        }

        private void OnEnable()
        {
            EnsureSplineAnimate();
            EnsureZeroWeightPose();
            ApplyNormalizedTime();
        }

        public void OnDidApplyAnimationProperties()
        {
            ApplyNormalizedTime();
        }

        public void ApplyNormalizedTime()
        {
            if (splineAnimate == null)
            {
                EnsureSplineAnimate();
            }

            if (splineAnimate == null)
            {
                return;
            }

            if (pauseSplineAnimate)
            {
                splineAnimate.Pause();
            }

            EnsureZeroWeightPose();
            splineAnimate.NormalizedTime = normalizedTime;

            if (Application.isPlaying)
            {
                ApplyRuntimeWeight();
            }
            else
            {
                ApplyEditorPreviewPose();
            }
        }

        [ContextMenu("Capture Zero Weight Pose")]
        public void CaptureZeroWeightPose()
        {
            zeroWeightLocalPosition = transform.localPosition;
            zeroWeightLocalRotation = transform.localRotation;
            zeroWeightLocalScale = transform.localScale;
            hasZeroWeightPose = true;
        }

        private void EnsureSplineAnimate()
        {
            if (splineAnimate == null)
            {
                splineAnimate = GetComponent<SplineAnimate>();
            }
        }

        private void EnsureZeroWeightPose()
        {
            if (!hasZeroWeightPose)
            {
                CaptureZeroWeightPose();
            }
        }

        private void ApplyRuntimeWeight()
        {
            if (Mathf.Approximately(weight, 1f))
            {
                return;
            }

            transform.localPosition = Vector3.LerpUnclamped(zeroWeightLocalPosition, transform.localPosition, weight);
            transform.localRotation = Quaternion.SlerpUnclamped(zeroWeightLocalRotation, transform.localRotation, weight);
        }

        private void ApplyEditorPreviewPose()
        {
            if (!TryEvaluateSplinePose(out var splineLocalPosition, out var splineLocalRotation))
            {
                return;
            }

            RegisterEditorPreviewTransformProperties();
            transform.localPosition = Vector3.LerpUnclamped(zeroWeightLocalPosition, splineLocalPosition, weight);
            transform.localRotation = Quaternion.SlerpUnclamped(zeroWeightLocalRotation, splineLocalRotation, weight);

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
#endif
        }

        private bool TryEvaluateSplinePose(out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;

            var container = splineAnimate.Container;
            if (container == null || container.Splines == null || container.Splines.Count == 0)
            {
                return false;
            }

            var path = new SplinePath<Spline>(container.Splines);
            var t = GetLoopInterpolation(path);
            if (!container.Evaluate(path, t, out var position, out var tangent, out var up))
            {
                return false;
            }

            var worldPosition = (Vector3)position;
            var worldRotation = EvaluateWorldRotation((Vector3)tangent, (Vector3)up);
            var parent = transform.parent;

            localPosition = parent == null ? worldPosition : parent.InverseTransformPoint(worldPosition);
            localRotation = parent == null ? worldRotation : Quaternion.Inverse(parent.rotation) * worldRotation;
            return true;
        }

        private float GetLoopInterpolation(SplinePath<Spline> path)
        {
            var pathLength = path.GetLength();
            var startOffsetT = pathLength <= 0f
                ? 0f
                : path.ConvertIndexUnit(splineAnimate.StartOffset * pathLength, PathIndexUnit.Distance, PathIndexUnit.Normalized);

            var normalizedTimeWithOffset = normalizedTime + startOffsetT;
            if (Mathf.Floor(normalizedTimeWithOffset) == normalizedTimeWithOffset)
            {
                return Mathf.Clamp01(normalizedTimeWithOffset);
            }

            return normalizedTimeWithOffset % 1f;
        }

        private Quaternion EvaluateWorldRotation(Vector3 tangent, Vector3 up)
        {
            if (splineAnimate.Alignment == SplineAnimate.AlignmentMode.None)
            {
                return transform.rotation;
            }

            var forward = Vector3.forward;
            var upward = Vector3.up;
            switch (splineAnimate.Alignment)
            {
                case SplineAnimate.AlignmentMode.SplineElement:
                    forward = tangent.sqrMagnitude <= Mathf.Epsilon ? transform.forward : tangent.normalized;
                    upward = up.sqrMagnitude <= Mathf.Epsilon ? transform.up : up.normalized;
                    break;
                case SplineAnimate.AlignmentMode.SplineObject:
                    var containerRotation = splineAnimate.Container.transform.rotation;
                    forward = containerRotation * Vector3.forward;
                    upward = containerRotation * Vector3.up;
                    break;
                case SplineAnimate.AlignmentMode.World:
                    break;
            }

            var axisRemapRotation = Quaternion.Inverse(Quaternion.LookRotation(
                GetAxisVector(splineAnimate.ObjectForwardAxis),
                GetAxisVector(splineAnimate.ObjectUpAxis)));

            return Quaternion.LookRotation(forward, upward) * axisRemapRotation;
        }

        private static Vector3 GetAxisVector(SplineComponent.AlignAxis axis)
        {
            return axis switch
            {
                SplineComponent.AlignAxis.XAxis => Vector3.right,
                SplineComponent.AlignAxis.YAxis => Vector3.up,
                SplineComponent.AlignAxis.ZAxis => Vector3.forward,
                SplineComponent.AlignAxis.NegativeXAxis => Vector3.left,
                SplineComponent.AlignAxis.NegativeYAxis => Vector3.down,
                SplineComponent.AlignAxis.NegativeZAxis => Vector3.back,
                _ => Vector3.forward
            };
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RegisterEditorPreviewTransformProperties()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || !AnimationMode.InAnimationMode())
            {
                return;
            }

            RegisterEditorPreviewTransformProperty("m_LocalPosition.x", transform.localPosition.x);
            RegisterEditorPreviewTransformProperty("m_LocalPosition.y", transform.localPosition.y);
            RegisterEditorPreviewTransformProperty("m_LocalPosition.z", transform.localPosition.z);
            RegisterEditorPreviewTransformProperty("m_LocalRotation.x", transform.localRotation.x);
            RegisterEditorPreviewTransformProperty("m_LocalRotation.y", transform.localRotation.y);
            RegisterEditorPreviewTransformProperty("m_LocalRotation.z", transform.localRotation.z);
            RegisterEditorPreviewTransformProperty("m_LocalRotation.w", transform.localRotation.w);
#endif
        }

#if UNITY_EDITOR
        private void RegisterEditorPreviewTransformProperty(string propertyPath, float value)
        {
            var modification = new PropertyModification
            {
                target = transform,
                propertyPath = propertyPath,
                value = value.ToString(CultureInfo.InvariantCulture)
            };

            if (AnimationUtility.PropertyModificationToEditorCurveBinding(
                    modification,
                    gameObject,
                    out var binding) == null)
            {
                return;
            }

            AnimationMode.AddPropertyModification(binding, modification, true);
        }
#endif

        private void OnValidate()
        {
            EnsureSplineAnimate();
            normalizedTime = Mathf.Max(0f, normalizedTime);
            weight = Mathf.Clamp01(weight);
            EnsureZeroWeightPose();
            ApplyNormalizedTime();
        }
    }
}
