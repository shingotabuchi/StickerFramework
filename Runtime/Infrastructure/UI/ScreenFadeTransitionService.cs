using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StickerFwk.Infrastructure.UI
{
    public sealed class ScreenFadeTransitionService : IScreenTransitionService, IDisposable
    {
        public const string TransitionTag = "Fade";

        private const float FadeDurationSeconds = 0.35f;
        private const float MaxFadeStepSeconds = 1f / 30f;
        private const int OverlaySortingOrder = short.MaxValue;

        private static readonly IProgress<float> NoProgress = new NullProgress();

        private readonly SemaphoreSlim _transitionLock = new(1, 1);

        private GameObject _root;
        private CanvasGroup _canvasGroup;
        private bool _isActive;
        private bool _disposed;

        public bool IsActive => _isActive;

        public event Action TransitionCompleted;

        public UniTask ExecuteAsync(
            Func<CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return ExecuteAsync((_, actionCt) => action(actionCt), transitionViewTag, ct);
        }

        public async UniTask ExecuteAsync(
            Func<IProgress<float>, CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            ThrowIfDisposed();
            await _transitionLock.WaitAsync(ct);
            try
            {
                EnsureView();
                _isActive = true;
                _root.SetActive(true);
                await FadeAsync(0f, 1f, ct);

                NoProgress.Report(0f);
                await action(NoProgress, ct);
                NoProgress.Report(1f);
            }
            finally
            {
                if (_root != null)
                {
                    await FadeAsync(_canvasGroup.alpha, 0f, CancellationToken.None);
                    _root.SetActive(false);
                }

                _isActive = false;
                TransitionCompleted?.Invoke();
                _transitionLock.Release();
            }
        }

        private void EnsureView()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("[Screen Fade Transition]", typeof(RectTransform));
            Object.DontDestroyOnLoad(_root);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            _canvasGroup = _root.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _root.AddComponent<GraphicRaycaster>();

            var imageObject = new GameObject("FadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(_root.transform, false);
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                imageObject.layer = uiLayer;
            }

            var rectTransform = (RectTransform)imageObject.transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            var image = imageObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            _root.SetActive(false);
        }

        private async UniTask FadeAsync(float from, float to, CancellationToken ct)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = from;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            var elapsed = 0f;
            while (elapsed < FadeDurationSeconds)
            {
                ct.ThrowIfCancellationRequested();
                // Root scene fades must complete even when gameplay time is paused or scaled.
                elapsed += Mathf.Min(UnityEngine.Time.unscaledDeltaTime, MaxFadeStepSeconds);
                var t = Mathf.Clamp01(elapsed / FadeDurationSeconds);
                _canvasGroup.alpha = Mathf.Lerp(from, to, t);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _canvasGroup.alpha = to;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ScreenFadeTransitionService));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _canvasGroup = null;
            }

            _transitionLock.Dispose();
        }

        private sealed class NullProgress : IProgress<float>
        {
            public void Report(float value) { }
        }
    }
}
