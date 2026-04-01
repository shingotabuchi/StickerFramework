using UnityEngine;

namespace StickerFwk.Core.Animation
{
    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;
        private const float ElasticPeriod = 0.3f;

        public static float Evaluate(EaseType type, float t)
        {
            switch (type)
            {
                case EaseType.InQuad:
                    return t * t;
                case EaseType.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case EaseType.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);

                case EaseType.InCubic:
                    return t * t * t;
                case EaseType.OutCubic:
                {
                    var inv = 1f - t;
                    return 1f - inv * inv * inv;
                }
                case EaseType.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - 4f * (1f - t) * (1f - t) * (1f - t);

                case EaseType.InQuart:
                    return t * t * t * t;
                case EaseType.OutQuart:
                {
                    var inv = 1f - t;
                    return 1f - inv * inv * inv * inv;
                }
                case EaseType.InOutQuart:
                {
                    var inv = 1f - t;
                    return t < 0.5f ? 8f * t * t * t * t : 1f - 8f * inv * inv * inv * inv;
                }

                case EaseType.InQuint:
                    return t * t * t * t * t;
                case EaseType.OutQuint:
                {
                    var inv = 1f - t;
                    return 1f - inv * inv * inv * inv * inv;
                }
                case EaseType.InOutQuint:
                {
                    var inv = 1f - t;
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - 16f * inv * inv * inv * inv * inv;
                }

                case EaseType.InExpo:
                    return t <= 0f ? 0f : Mathf.Pow(2f, 10f * (t - 1f));
                case EaseType.OutExpo:
                    return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case EaseType.InOutExpo:
                    if (t <= 0f) { return 0f; }
                    if (t >= 1f) { return 1f; }
                    return t < 0.5f
                        ? 0.5f * Mathf.Pow(2f, 20f * t - 10f)
                        : 1f - 0.5f * Mathf.Pow(2f, -20f * t + 10f);

                case EaseType.InCirc:
                    return 1f - Mathf.Sqrt(1f - t * t);
                case EaseType.OutCirc:
                {
                    var inv = t - 1f;
                    return Mathf.Sqrt(1f - inv * inv);
                }
                case EaseType.InOutCirc:
                {
                    var scaled = t * 2f;
                    if (scaled < 1f)
                    {
                        return -0.5f * (Mathf.Sqrt(1f - scaled * scaled) - 1f);
                    }
                    scaled -= 2f;
                    return 0.5f * (Mathf.Sqrt(1f - scaled * scaled) + 1f);
                }

                case EaseType.InBack:
                    return t * t * ((BackOvershoot + 1f) * t - BackOvershoot);
                case EaseType.OutBack:
                {
                    var inv = t - 1f;
                    return inv * inv * ((BackOvershoot + 1f) * inv + BackOvershoot) + 1f;
                }
                case EaseType.InOutBack:
                {
                    var s = BackOvershoot * 1.525f;
                    var scaled = t * 2f;
                    if (scaled < 1f)
                    {
                        return 0.5f * (scaled * scaled * ((s + 1f) * scaled - s));
                    }
                    scaled -= 2f;
                    return 0.5f * (scaled * scaled * ((s + 1f) * scaled + s) + 2f);
                }

                case EaseType.InElastic:
                    if (t <= 0f) { return 0f; }
                    if (t >= 1f) { return 1f; }
                    return -Mathf.Pow(2f, 10f * (t - 1f)) *
                           Mathf.Sin((t - 1f - ElasticPeriod / 4f) * (2f * Mathf.PI) / ElasticPeriod);
                case EaseType.OutElastic:
                    if (t <= 0f) { return 0f; }
                    if (t >= 1f) { return 1f; }
                    return Mathf.Pow(2f, -10f * t) *
                           Mathf.Sin((t - ElasticPeriod / 4f) * (2f * Mathf.PI) / ElasticPeriod) + 1f;
                case EaseType.InOutElastic:
                    if (t <= 0f) { return 0f; }
                    if (t >= 1f) { return 1f; }
                    if (t < 0.5f)
                    {
                        return -0.5f * Mathf.Pow(2f, 20f * t - 10f) *
                               Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI) / (ElasticPeriod * 1.5f));
                    }
                    return 0.5f * Mathf.Pow(2f, -20f * t + 10f) *
                           Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI) / (ElasticPeriod * 1.5f)) + 1f;

                case EaseType.Linear:
                default:
                    return t;
            }
        }
    }
}
