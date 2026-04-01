using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StickerFwk.Core
{
    public static class AnimatorExtensions
    {
        public static async UniTask PlayAsync(this Animator animator, string stateName, int layer = 0, float crossFadeDuration = 0f, CancellationToken ct = default)
        {
            var stateHash = Animator.StringToHash(stateName);
            if (crossFadeDuration > 0f)
            {
                animator.CrossFadeInFixedTime(stateName, crossFadeDuration, layer);
            }
            else
            {
                animator.Play(stateName, layer, 0f);
            }

            await animator.AwaitStateCompletionAsync(stateHash, layer, ct);
        }

        public static async UniTask PlayAsync(this Animator animator, int stateHash, int layer = 0, float crossFadeDuration = 0f, CancellationToken ct = default)
        {
            if (crossFadeDuration > 0f)
            {
                animator.CrossFadeInFixedTime(stateHash, crossFadeDuration, layer);
            }
            else
            {
                animator.Play(stateHash, layer, 0f);
            }

            await animator.AwaitStateCompletionAsync(stateHash, layer, ct);
        }

        public static async UniTask AwaitCurrentStateCompletionAsync(this Animator animator, int layer = 0, CancellationToken ct = default)
        {
            await UniTask.Yield(ct);
            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            var stateHash = stateInfo.fullPathHash != 0 ? stateInfo.fullPathHash : stateInfo.shortNameHash;
            await animator.AwaitStateCompletionAsync(stateHash, layer, ct);
        }

        public static async UniTask AwaitStateCompletionAsync(this Animator animator, int stateHash, int layer = 0, CancellationToken ct = default)
        {
            await UniTask.Yield(ct);

            var hasObservedTargetState = false;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
                var isInTransition = animator.IsInTransition(layer);
                var targetIsCurrent = IsMatchingState(stateInfo, stateHash);

                if (!hasObservedTargetState)
                {
                    if (targetIsCurrent)
                    {
                        hasObservedTargetState = true;
                    }
                    else if (isInTransition)
                    {
                        var nextStateInfo = animator.GetNextAnimatorStateInfo(layer);
                        hasObservedTargetState = IsMatchingState(nextStateInfo, stateHash);
                    }
                }
                if (hasObservedTargetState && targetIsCurrent && stateInfo.normalizedTime >= 1f && !isInTransition)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private static bool IsMatchingState(AnimatorStateInfo stateInfo, int stateHash)
        {
            return stateInfo.fullPathHash == stateHash || stateInfo.shortNameHash == stateHash;
        }
    }
}
