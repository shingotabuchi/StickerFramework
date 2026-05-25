using System;
using StickerFwk.Core.Haptics;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace StickerFwk.Infrastructure.Haptics.Platform
{
    internal sealed class IOSHapticBackend : IHapticBackend
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void StickerFwk_Haptics_PlayImpact(int style, float intensity);

        [DllImport("__Internal")]
        private static extern void StickerFwk_Haptics_PlayPattern(
            float[] intensity, float[] sharpness, int count, float duration, float intensityScale);

        [DllImport("__Internal")]
        private static extern void StickerFwk_Haptics_StopEngine();
#endif

        private const int SampleCount = 16;
        private static readonly float[] s_intensityBuffer = new float[SampleCount];
        private static readonly float[] s_sharpnessBuffer = new float[SampleCount];

        public bool IsSupported =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public void PlayOneShot(in HapticPattern pattern, float intensityScale)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (pattern.PresetHint != HapticPresetId.None)
            {
                StickerFwk_Haptics_PlayImpact((int)pattern.PresetHint, intensityScale);
                return;
            }

            SampleCurve(pattern.IntensityCurve, pattern.DurationSeconds, s_intensityBuffer);
            SampleCurve(pattern.SharpnessCurve, pattern.DurationSeconds, s_sharpnessBuffer);
            StickerFwk_Haptics_PlayPattern(
                s_intensityBuffer, s_sharpnessBuffer, SampleCount, pattern.DurationSeconds, intensityScale);
#endif
        }

        public void Dispose()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { StickerFwk_Haptics_StopEngine(); }
            catch (Exception) { /* engine may already be torn down */ }
#endif
        }

        private static void SampleCurve(HapticPatternCurve curve, float duration, float[] buffer)
        {
            if (buffer.Length == 0) return;
            var step = duration / (buffer.Length - 1);
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = curve.Evaluate(i * step);
            }
        }
    }
}
