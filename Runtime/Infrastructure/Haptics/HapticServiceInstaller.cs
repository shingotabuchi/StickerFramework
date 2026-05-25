using Cysharp.Threading.Tasks;
using StickerFwk.Core.Haptics;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Infrastructure.Haptics
{
    [DisallowMultipleComponent]
    public sealed class HapticServiceInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private HapticServiceRoot _root;
        [SerializeField] private bool _createRootIfMissing = true;
        [SerializeField] private bool _resolveOnBuild = true;
        [SerializeField] private string _defaultPresetsAddressableKey = "stickerfwk/haptics/default";
        [SerializeField] private bool _loadDefaultPresetsOnBuild = true;

        public void Install(IContainerBuilder builder)
        {
            RegisterRoot(builder);

            builder.Register<HapticService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            if (!_resolveOnBuild) return;

            var loadDefaults = _loadDefaultPresetsOnBuild;
            var defaultsKey = _defaultPresetsAddressableKey;

            builder.RegisterBuildCallback(container =>
            {
                var service = container.Resolve<IHapticService>();
                if (!loadDefaults || string.IsNullOrWhiteSpace(defaultsKey)) return;

                // Fire-and-forget; cancellation is bounded by the container lifetime (service disposes
                // its asset handles), matching the SoundService eager-load behavior. No IScopeCancellation
                // is wired in v1 — see plan.md / research.md R10.
                service.LoadCueSheetAsync(defaultsKey).Forget();
            });
        }

        private void RegisterRoot(IContainerBuilder builder)
        {
            if (_root == null)
            {
                _root = GetComponentInChildren<HapticServiceRoot>(true);
            }

            if (_root != null)
            {
                builder.RegisterInstance(_root);
                return;
            }

            if (_createRootIfMissing)
            {
                builder.RegisterComponentOnNewGameObject<HapticServiceRoot>(Lifetime.Singleton, "HapticServiceRoot")
                    .DontDestroyOnLoad()
                    .AsSelf();
                return;
            }

            builder.RegisterComponentInHierarchy<HapticServiceRoot>();
        }
    }
}
