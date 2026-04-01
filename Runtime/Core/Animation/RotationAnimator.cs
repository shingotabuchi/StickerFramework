using System.Diagnostics;
using UnityEngine;

namespace StickerFwk.Core
{
    [ExecuteAlways]
    public class RotationAnimator : MonoBehaviour
    {
        [SerializeField] private Vector3 _rotation;
        [SerializeField] private float _time;

        public Vector3 Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                ApplyRotation();
            }
        }

        public float Time
        {
            get => _time;
            set
            {
                _time = value;
                ApplyRotation();
            }
        }

        private void OnEnable()
        {
            ApplyRotation();
        }

        public void OnDidApplyAnimationProperties()
        {
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            var targetRotation = Quaternion.Euler(_rotation);
            transform.localRotation = Quaternion.SlerpUnclamped(Quaternion.identity, targetRotation, _time);
        }

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            ApplyRotation();
        }
    }
}