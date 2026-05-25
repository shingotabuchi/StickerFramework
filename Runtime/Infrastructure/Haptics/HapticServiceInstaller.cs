using StickerFwk.Core.AssetManagement;
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

        public void Install(IContainerBuilder builder)
        {
            RegisterRoot(builder);

            builder.Register<DefaultHapticProfile>(Lifetime.Singleton);
            builder.Register(container => new HapticService(
                    container.Resolve<IAssetRequester>(),
                    container.Resolve<HapticServiceRoot>(),
                    container.Resolve<DefaultHapticProfile>()),
                    Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            if (!_resolveOnBuild) return;

            builder.RegisterBuildCallback(container => container.Resolve<IHapticService>());
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
