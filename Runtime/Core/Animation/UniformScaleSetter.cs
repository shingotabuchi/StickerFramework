using System.Diagnostics;
using UnityEngine;

namespace StickerFwk.Core
{
    [ExecuteAlways]
    public class UniformScaleSetter : MonoBehaviour
    {
        [SerializeField] private float _scale = 1f;
        [SerializeField] private bool _applyX = true;
        [SerializeField] private bool _applyY = true;
        [SerializeField] private bool _applyZ = true;

        public void OnDidApplyAnimationProperties()
        {
            ApplyScale();
        }

        private void ApplyScale()
        {
            var currentScale = transform.localScale;
            var targetScale = GetTargetScale(currentScale);
            transform.localScale = targetScale;
        }

        private Vector3 GetTargetScale(Vector3 currentScale)
        {
            return new Vector3(
                ResolveAxisScale(_applyX, currentScale.x),
                ResolveAxisScale(_applyY, currentScale.y),
                ResolveAxisScale(_applyZ, currentScale.z)
            );
        }

        private float ResolveAxisScale(bool shouldApply, float currentAxisScale)
        {
            return shouldApply ? _scale : currentAxisScale;
        }

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            ApplyScale();
        }
    }
}