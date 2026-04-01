using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Playables;

namespace StickerFwk.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class WindowView : MonoBehaviour
    {
        [Header("Window Configuration")]
        [SerializeField] UILayer _layer = UILayer.Window;
        [SerializeField] bool _isBlocking = true;
        [SerializeField] TransitionType _showTransition = TransitionType.Fade;
        [SerializeField] TransitionType _hideTransition = TransitionType.Fade;
        [SerializeField] float _transitionDuration = 0.3f;

        [Header("Animator Transition")]
        [SerializeField] string _showAnimatorState = "Show";
        [SerializeField] string _hideAnimatorState = "Hide";

        [Header("Timeline Transition")]
        [SerializeField] PlayableDirector _showTimeline;
        [SerializeField] PlayableDirector _hideTimeline;

        CompositeDisposable _disposables = new CompositeDisposable();

        CanvasGroup _canvasGroup;

        public UILayer Layer => _layer;
        public bool IsBlocking => _isBlocking;
        public TransitionType ShowTransition => _showTransition;
        public TransitionType HideTransition => _hideTransition;
        public float TransitionDuration => _transitionDuration;
        public string ShowAnimatorState => _showAnimatorState;
        public string HideAnimatorState => _hideAnimatorState;
        public PlayableDirector ShowTimeline => _showTimeline;
        public PlayableDirector HideTimeline => _hideTimeline;

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
