using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using UnityEngine.SceneManagement;

namespace StickerFwk.Infrastructure.SceneManagement
{
    public class SceneTransitionService : ISceneTransitionService
    {
        readonly IScreenTransitionService _screenTransitionService;
        readonly IInputLockService _inputLockService;
        readonly SceneReadyNotifier _sceneReadyNotifier;

        public SceneTransitionService(
            IScreenTransitionService screenTransitionService,
            IInputLockService inputLockService,
            SceneReadyNotifier sceneReadyNotifier)
        {
            _screenTransitionService = screenTransitionService;
            _inputLockService = inputLockService;
            _sceneReadyNotifier = sceneReadyNotifier;
        }

        public async UniTask TransitionToSceneAsync(
            string sceneName,
            string transitionViewTag = null,
            Func<CancellationToken, UniTask> beforeLoad = null,
            CancellationToken ct = default)
        {
            using var _ = _inputLockService.Lock();

            await _screenTransitionService.ExecuteAsync(async innerCt =>
            {
                if (beforeLoad != null)
                {
                    await beforeLoad(innerCt);
                }

                _sceneReadyNotifier.Reset();
                await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single)
                    .ToUniTask(cancellationToken: innerCt);
                await _sceneReadyNotifier.WaitForReady();
            },
            transitionViewTag,
            ct);
        }
    }
}
