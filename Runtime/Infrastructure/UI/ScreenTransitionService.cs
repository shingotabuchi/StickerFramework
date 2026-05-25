using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace StickerFwk.Infrastructure.UI
{
    public class ScreenTransitionService : IScreenTransitionService
    {
        readonly IAssetRequester _assetRequester;
        readonly IObjectResolver _resolver;
        readonly string _keyPrefix;

        Transform _root;
        int _activeCount;

        public bool IsActive => _activeCount > 0;

        public event Action TransitionCompleted;

        [Inject]
        public ScreenTransitionService(IAssetRequester assetRequester, IObjectResolver resolver)
            : this(assetRequester, resolver, resolver?.ResolveOrDefault<WindowAssetKeyOptions>())
        {
        }

        public ScreenTransitionService(
            IAssetRequester assetRequester,
            IObjectResolver resolver,
            WindowAssetKeyOptions keyOptions)
        {
            _assetRequester = assetRequester ?? throw new ArgumentNullException(nameof(assetRequester));
            _resolver = resolver;
            _keyPrefix = keyOptions?.Prefix ?? string.Empty;
        }

        public async UniTask ExecuteAsync(
            Func<CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await ExecuteAsync((_, actionCt) => action(actionCt), transitionViewTag, ct);
        }

        public async UniTask ExecuteAsync(
            Func<IProgress<float>, CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var key = BuildKey(transitionViewTag);
            using var assetHandle = await _assetRequester.RequestAsset<GameObject>(key, ct);
            var prefab = assetHandle.Asset;
            if (prefab == null)
            {
                throw new InvalidOperationException($"Addressable screen transition asset '{key}' resolved to null.");
            }

            var instance = Object.Instantiate(prefab, GetOrCreateRoot(), false);
            var rig = instance.GetComponent<IScreenTransitionRig>() ?? instance.GetComponentInChildren<IScreenTransitionRig>(true);
            if (rig == null)
            {
                Object.Destroy(instance);
                throw new InvalidOperationException($"Prefab '{key}' must contain a component implementing {nameof(IScreenTransitionRig)}.");
            }

            _resolver?.InjectGameObject(instance);
            _activeCount++;

            try
            {
                await rig.Show(ct);
                var progress = new ScreenTransitionProgress(rig);
                progress.Report(0f);
                await action(progress, ct);
                progress.Report(1f);
            }
            finally
            {
                try
                {
                    await rig.Hide(CancellationToken.None);
                }
                finally
                {
                    _activeCount = Math.Max(0, _activeCount - 1);
                    DestroyInstance(instance);
                    TransitionCompleted?.Invoke();
                }
            }
        }

        static void DestroyInstance(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            Object.DestroyImmediate(instance);
        }

        string BuildKey(string tag)
        {
            return string.IsNullOrEmpty(tag)
                ? $"{_keyPrefix}Views/{nameof(ScreenTransitionView)}.prefab"
                : $"{_keyPrefix}Views/{nameof(ScreenTransitionView)}_{tag}.prefab";
        }

        Transform GetOrCreateRoot()
        {
            if (_root != null)
            {
                return _root;
            }

            var go = new GameObject("[ScreenTransitions]");
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(go);
            }
            _root = go.transform;
            return _root;
        }

        private sealed class ScreenTransitionProgress : IProgress<float>
        {
            private readonly IScreenTransitionRig _rig;

            public ScreenTransitionProgress(IScreenTransitionRig rig)
            {
                _rig = rig;
            }

            public void Report(float value)
            {
                _rig.SetProgress(value);
            }
        }
    }
}
