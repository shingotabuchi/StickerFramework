using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class AnimatorTransition : ITransition
    {
        [SerializeField] Animator _animator;
        [SerializeField] string _showState = "Show";
        [SerializeField] string _hideState = "Hide";

        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            // UIService.PushLocked hides the freshly-instantiated CanvasGroup (alpha = 0) until
            // the show transition begins. Every shipped transition restores alpha at the start
            // of Play so the prefab actually becomes visible. Without this restore, an
            // animator-driven window stays at alpha 0 for the entire show animation.
            if (isShow)
            {
                view.CanvasGroup.alpha = 1f;
            }

            var animator = _animator != null ? _animator : view.GetComponent<Animator>();
            if (animator == null)
            {
                view.CanvasGroup.alpha = isShow ? 1f : 0f;
                return;
            }

            var stateName = isShow ? _showState : _hideState;
            await animator.PlayAsync(stateName, ct: ct);
        }
    }
}
