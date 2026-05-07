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

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
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
