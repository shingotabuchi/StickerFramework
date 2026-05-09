using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using StickerFwk.Core.InspectorTools;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class WindowView : MonoBehaviour
    {
        [Header("Window Configuration")]
        [SerializeField] UILayer _layer = UILayer.UI;
        [SerializeField] bool _isBlocking = true;
        [SerializeField] float _transitionDuration = 0.3f;

        [Header("Transitions")]
        [SerializeReference, SubclassSelector] ITransition _showTransition = new FadeTransition();
        [SerializeReference, SubclassSelector] ITransition _hideTransition = new FadeTransition();

        CompositeDisposable _disposables = new CompositeDisposable();

        CanvasGroup _canvasGroup;

        public UILayer Layer => _layer;
        public bool IsBlocking => _isBlocking;
        public ITransition ShowTransition => _showTransition;
        public ITransition HideTransition => _hideTransition;
        public float TransitionDuration => _transitionDuration;

        public CanvasGroup CanvasGroup => _canvasGroup;

        public RectTransform RectTransform => (RectTransform)transform;

        // Test-only seam. Lets unit tests configure the serialized layer field on a
        // GameObject built at runtime (no prefab inspector). DO NOT call from production code.
        internal void __SetLayerForTests(UILayer layer) => _layer = layer;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        // Safety net for cases where the GameObject is destroyed without going through the
        // managed UIService.Pop path — e.g. UIService.PushLocked's catch path destroys the
        // instance after a failed OnInitialize, or scene unload tears the window down. Without
        // this, OnDispose never runs, the presenter is never disposed via the framework, and
        // any asset handles loaded inside InitializeAsync stay live until scope teardown.
        // OnDispose / Presenter.Dispose are idempotent so a managed Pop followed by Unity's
        // OnDestroy is safe.
        protected virtual void OnDestroy()
        {
            OnDispose();
        }

        public virtual UniTask OnInitialize(CancellationToken ct) => UniTask.CompletedTask;

        public void OnBeforeShow()
        {
            OnBeforeShowInternal();
        }

        public void OnShow()
        {
            OnShowInternal();
        }

        public void OnBeforeHide()
        {
            OnBeforeHideInternal();
        }

        public void OnHide()
        {
            OnHideInternal();
        }

        public void OnDispose()
        {
            DisposeAll();
            OnDisposeInternal();
        }

        /// <summary>
        /// Tracks an <see cref="IDisposable"/> for the lifetime of this window. Items added here
        /// are disposed in <see cref="OnDispose"/> — i.e. when the window is popped and destroyed.
        /// They are NOT disposed in <see cref="OnHide"/>, so a subscription registered here will
        /// keep firing if the window is hidden and later shown again. For hide-scoped lifetimes,
        /// manage the disposable yourself in <see cref="OnHideInternal"/>.
        /// </summary>
        protected void AddDisposable(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            _disposables.Add(disposable);
        }

        protected virtual void OnBeforeShowInternal() { }

        protected virtual void OnShowInternal() { }

        protected virtual void OnBeforeHideInternal() { }

        protected virtual void OnHideInternal() { }

        protected virtual void OnDisposeInternal() { }

        void DisposeAll()
        {
            _disposables.Dispose();
            _disposables = new CompositeDisposable();
        }
    }
}
