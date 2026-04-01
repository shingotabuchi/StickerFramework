using UnityEngine;

namespace StickerFwk.Core
{
    public static class SmoothMath
    {
        /// <summary>
        /// Returns the frame-rate-independent exponential-decay lerp factor.
        /// Usage: Mathf.Lerp(current, target, SmoothMath.ExpFactor(speed))
        /// </summary>
        public static float ExpFactor(float speed)
        {
            return 1f - Mathf.Exp(-speed * Time.deltaTime);
        }

        /// <summary>
        /// Smoothly moves a float toward a target using exponential decay.
        /// </summary>
        public static float ExpDecay(float current, float target, float speed)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        /// <summary>
        /// Smoothly moves a Vector3 toward a target using exponential decay.
        /// </summary>
        public static Vector3 ExpDecay(Vector3 current, Vector3 target, float speed)
        {
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }
    }
}
