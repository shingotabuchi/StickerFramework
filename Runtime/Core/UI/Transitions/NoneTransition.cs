using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class NoneTransition : ITransition
    {
        public UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            view.CanvasGroup.alpha = isShow ? 1f : 0f;
            return UniTask.CompletedTask;
        }
    }
}
