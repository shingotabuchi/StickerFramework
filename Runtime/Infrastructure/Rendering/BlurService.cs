using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.Animation;
using StickerFwk.Core.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class BlurService : IBlurService, IDisposable
    {
        int _requestCount;
        int _staticBackgroundRequestCount;
        CancellationTokenSource _cts;
        EaseType _lastEase;
        float _lastDuration;
        BlurVolume _blur;
        bool _disposed;

        public bool IsBlurred => _requestCount > 0;

        public void Register(Volume volume)
        {
            if (volume == null || volume.profile == null)
            {
                return;
            }

            if (volume.profile.TryGet(out BlurVolume blur))
            {
                _blur = blur;
            }
        }

        public void Unregister(Volume volume)
        {
            if (volume == null || volume.profile == null)
            {
                return;
            }

            if (volume.profile.TryGet(out BlurVolume blur) && _blur == blur)
            {
                _blur = null;
            }
        }

        public IDisposable Request(EaseType ease, float duration)
        {
            return Request(ease, duration, ease, duration);
        }

        public IDisposable Request(EaseType onEase, float onDuration, EaseType offEase, float offDuration)
        {
            _lastEase = onEase;
            _lastDuration = onDuration;
            _requestCount++;

            if (_requestCount == 1)
            {
                StartTransition(true, onEase, onDuration);
            }

            return new BlurHandle(this, offEase, offDuration);
        }

        public IDisposable RequestStaticBackground()
        {
            return RequestStaticBackground(EaseType.Linear, 0f);
        }

        public IDisposable RequestStaticBackground(EaseType ease, float duration)
        {
            return RequestStaticBackground(ease, duration, ease, duration);
        }

        public IDisposable RequestStaticBackground(EaseType onEase, float onDuration, EaseType offEase, float offDuration)
        {
            _staticBackgroundRequestCount++;

            if (_staticBackgroundRequestCount == 1)
            {
                SetManualUpdate(true);
            }

            SetBlurDirty();

            return new StaticBackgroundBlurHandle(this, Request(onEase, onDuration, offEase, offDuration));
        }

        void Release(EaseType ease, float duration)
        {
            if (_requestCount == 0)
            {
                return;
            }

            _requestCount--;

            if (_requestCount == 0)
            {
                StartTransition(false, ease, duration);
            }
        }

        void StartTransition(bool enabled, EaseType ease, float duration)
        {
            if (_disposed)
            {
                return;
            }

            _blur?.SetDirty();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            TransitionAsync(_blur, enabled, ease, duration, _cts.Token).Forget();
        }

        static async UniTask TransitionAsync(BlurVolume blur, bool enabled, EaseType ease, float duration, CancellationToken ct)
        {
            Log.Info($"Starting blur transition: enabled={enabled}, ease={ease}, duration={duration}");
            if (blur == null)
            {
                return;
            }

            var from = blur.intensity.value;
            var to = enabled ? 1f : 0f;

            if (duration <= 0f)
            {
                blur.intensity.Override(to);
                blur.enabled.Override(enabled);
                blur.SetDirty();
                return;
            }

            if (enabled)
            {
                blur.enabled.Override(true);
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Easing.Evaluate(ease, t);
                blur.intensity.Override(Mathf.Lerp(from, to, eased));
                blur.SetDirty();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            blur.intensity.Override(to);
            blur.SetDirty();

            if (!enabled)
            {
                blur.enabled.Override(false);
            }

            Log.Info($"Blur transition completed enabled={enabled}, ease={ease}, duration={duration}");
        }

        void SetBlurDirty()
        {
            _blur?.SetDirty();
        }

        void SetManualUpdate(bool enabled)
        {
            _blur?.manualUpdate.Override(enabled);
        }

        void ReleaseStaticBackground(IDisposable blurHandle)
        {
            if (_staticBackgroundRequestCount > 0)
            {
                _staticBackgroundRequestCount--;
            }

            if (_staticBackgroundRequestCount == 0)
            {
                SetManualUpdate(false);
            }

            blurHandle?.Dispose();
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        sealed class BlurHandle : IDisposable
        {
            BlurService _owner;
            readonly EaseType _ease;
            readonly float _duration;

            public BlurHandle(BlurService owner, EaseType ease, float duration)
            {
                _owner = owner;
                _ease = ease;
                _duration = duration;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.Release(_ease, _duration);
                _owner = null;
            }
        }

        sealed class StaticBackgroundBlurHandle : IDisposable
        {
            BlurService _owner;
            IDisposable _blurHandle;

            public StaticBackgroundBlurHandle(BlurService owner, IDisposable blurHandle)
            {
                _owner = owner;
                _blurHandle = blurHandle;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.ReleaseStaticBackground(_blurHandle);
                _owner = null;
                _blurHandle = null;
            }
        }
    }
}
