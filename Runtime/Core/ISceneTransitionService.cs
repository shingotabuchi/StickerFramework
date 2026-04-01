using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core
{
    public interface ISceneTransitionService
    {
        UniTask TransitionToSceneAsync(
            string sceneName,
            string transitionViewTag = null,
            Func<CancellationToken, UniTask> beforeLoad = null,
            CancellationToken ct = default);
    }
}
