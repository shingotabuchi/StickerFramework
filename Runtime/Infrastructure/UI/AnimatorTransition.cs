using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    public class AnimatorTransition : ITransition
    {
        readonly Animator _animator;
        readonly string _showState;
        readonly string _hideState;

        public AnimatorTransition(Animator animator, string showState, string hideState)
        {
            _animator = animator;
            _showState = showState;
            _hideState = hideState;
        }

        public async UniTask Play(CanvasGroup canvasGroup, RectTransform rectTransform, bool isShow, float duration, CancellationToken ct)
        {
            if (_animator == null)
            {
                canvasGroup.alpha = isShow ? 1f : 0f;
                return;
            }

            var stateName = isShow ? _showState : _hideState;
            await _animator.PlayAsync(stateName, ct: ct);
        }
    }
}
