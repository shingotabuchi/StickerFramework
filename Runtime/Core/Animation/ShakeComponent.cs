using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.InspectorTools;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core
{
    /// <summary>
    /// Applies a temporary local-position shake to its transform.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShakeComponent : MonoBehaviour
    {
        [SerializeField] private ShakeNoiseType _noiseType = ShakeNoiseType.Perlin;
        [SerializeField][Min(0f)] private float _duration = 0.35f;
        [SerializeField][Min(0f)] private float _amplitude = 0.15f;
        [SerializeField] private AnimationCurve _amplitudeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField][Min(0f)] private float _frequency = 24f;
        [SerializeField] private AnimationCurve _frequencyCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField] private ShakeTimeMode _timeMode = ShakeTimeMode.Scaled;
        [SerializeField] private int _seed = 1;

        private CancellationTokenSource _shakeCts;
        private Vector3 _baseLocalPosition;
        private ShakeNoiseGenerator _noiseGenerator;
        private ITimeService _timeService;
        private CancellationToken _destroyToken;

        public bool IsPlaying => _shakeCts != null;
        public ShakeTimeMode TimeMode
        {
            get => _timeMode;
            set => _timeMode = value;
        }

        private void Awake()
        {
            _destroyToken = this.GetCancellationTokenOnDestroy();
        }

        [Inject]
        public void Construct(ITimeService timeService)
        {
            _timeService = timeService;
        }

        [Button("Play Shake", playModeOnly: true)]
        public void PlayShake()
        {
            PlayShakeAsync().Forget();
        }

        public async UniTask PlayShakeAsync(CancellationToken cancellationToken = default)
        {
            StopShake();

            _baseLocalPosition = transform.localPosition;
            _noiseGenerator = new ShakeNoiseGenerator(_seed);
            var playCts = new CancellationTokenSource();
            _shakeCts = playCts;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                playCts.Token,
                cancellationToken,
                _destroyToken);

            try
            {
                await ShakeAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(_shakeCts, playCts))
                {
                    if (!_destroyToken.IsCancellationRequested)
                    {
                        RestoreBaseLocalPosition();
                    }

                    _shakeCts = null;
                }

                playCts.Dispose();
            }
        }

        public void StopShake()
        {
            if (_shakeCts == null)
            {
                return;
            }

            _shakeCts.Cancel();
            _shakeCts = null;
            RestoreBaseLocalPosition();
        }

        private async UniTask ShakeAsync(CancellationToken ct)
        {
            if (_duration <= 0f || _amplitude <= 0f)
            {
                return;
            }

            var elapsed = 0f;
            while (elapsed < _duration)
            {
                var normalizedTime = Mathf.Clamp01(elapsed / _duration);
                ApplyShake(normalizedTime, elapsed);
                elapsed += DeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private void ApplyShake(float normalizedTime, float elapsed)
        {
            var amplitude = _amplitude * EvaluateCurve(_amplitudeCurve, normalizedTime, 1f);
            var frequency = _frequency * EvaluateCurve(_frequencyCurve, normalizedTime, 1f);
            var sample = _noiseGenerator.Sample(_noiseType, elapsed * Mathf.Max(0f, frequency));

            transform.localPosition = _baseLocalPosition + new Vector3(sample.X, sample.Y, sample.Z) * amplitude;
        }

        private void RestoreBaseLocalPosition()
        {
            transform.localPosition = _baseLocalPosition;
        }

        private static float EvaluateCurve(AnimationCurve curve, float time, float fallback)
        {
            return curve == null || curve.length == 0 ? fallback : curve.Evaluate(time);
        }

        private float DeltaTime => _timeMode == ShakeTimeMode.Unscaled
            ? _timeService?.UnscaledDeltaTime ?? Time.unscaledDeltaTime
            : _timeService?.DeltaTime ?? Time.deltaTime;

        private void OnDisable()
        {
            StopShake();
        }

        private void OnDestroy()
        {
            StopShake();
        }
    }
}
