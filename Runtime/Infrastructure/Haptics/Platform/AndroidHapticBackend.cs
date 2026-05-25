using System;
using StickerFwk.Core.Haptics;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace StickerFwk.Infrastructure.Haptics.Platform
{
    internal sealed class AndroidHapticBackend : IHapticBackend
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const float SampleStepSeconds = 0.020f;
        private static readonly int s_sdkInt;

        private readonly AndroidJavaObject _vibrator;
        private readonly bool _hasVibrator;

        static AndroidHapticBackend()
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            s_sdkInt = version.GetStatic<int>("SDK_INT");
        }

        public AndroidHapticBackend()
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                _hasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            }
            catch (Exception)
            {
                _vibrator = null;
                _hasVibrator = false;
            }
        }

        public bool IsSupported => _hasVibrator;

        public void PlayOneShot(in HapticPattern pattern, float intensityScale)
        {
            if (!_hasVibrator) return;

            BuildWaveform(pattern, intensityScale, out var timings, out var amplitudes);
            if (timings == null || timings.Length == 0) return;

            try
            {
                if (s_sdkInt >= 26)
                {
                    using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform", timings, amplitudes, -1);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", timings, -1);
                }
            }
            catch (Exception)
            {
                // Swallow; haptic dispatch must never throw to the caller.
            }
        }

        public void Dispose()
        {
            try { _vibrator?.Call("cancel"); } catch (Exception) { /* ignore */ }
            _vibrator?.Dispose();
        }

        private static void BuildWaveform(in HapticPattern pattern, float intensityScale,
            out long[] timings, out int[] amplitudes)
        {
            var duration = pattern.DurationSeconds;
            if (duration <= 0f) { timings = null; amplitudes = null; return; }

            var stepCount = Mathf.Max(1, Mathf.CeilToInt(duration / SampleStepSeconds));
            timings = new long[stepCount];
            amplitudes = new int[stepCount];

            var stepMs = Mathf.RoundToInt(SampleStepSeconds * 1000f);
            var clampedScale = Mathf.Clamp01(intensityScale);

            for (var i = 0; i < stepCount; i++)
            {
                var t = i * SampleStepSeconds;
                var sample = pattern.IntensityCurve.Evaluate(t) * clampedScale;
                timings[i] = stepMs;
                amplitudes[i] = Mathf.Clamp(Mathf.RoundToInt(sample * 255f), 0, 255);
            }
        }
#else
        public bool IsSupported => false;
        public void PlayOneShot(in HapticPattern pattern, float intensityScale) { }
        public void Dispose() { }
#endif
    }
}
