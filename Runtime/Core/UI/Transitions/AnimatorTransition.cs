using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class AnimatorTransition : ITransition
    {
        [SerializeField] string _showState = "Show";
        [SerializeField] string _hideState = "Hide";

        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            var animator = view.GetComponent<Animator>();
            if (animator == null)
            {
                var targets = view.GetComponent<AnimatorTransitionTargets>();
                animator = targets != null ? targets.Animator : null;
            }
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
