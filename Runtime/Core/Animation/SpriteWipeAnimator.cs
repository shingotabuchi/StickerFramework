using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Core
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class SpriteWipeAnimator : MonoBehaviour
    {
        private static readonly int WipeTimeId = Shader.PropertyToID("_WipeTime");

        [SerializeField] [Range(0f, 1f)] private float _wipeTime;

        private Graphic _graphic;

        public float WipeTime
        {
            get => _wipeTime;
            set
            {
                _wipeTime = value;
                ApplyWipeTime();
            }
        }

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
        }

        private void OnEnable()
        {
            if (_graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }

            ApplyWipeTime();
        }

        public void OnDidApplyAnimationProperties()
        {
            ApplyWipeTime();
        }

        private void ApplyWipeTime()
        {
            if (_graphic == null)
            {
                return;
            }

            _graphic.material.SetFloat(WipeTimeId, _wipeTime);
        }

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            if (_graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }

            ApplyWipeTime();
        }
    }
}
