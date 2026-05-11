using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Infrastructure.Sound
{
    [DisallowMultipleComponent]
    public sealed class SoundServiceInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private SoundServiceRoot _root;
        [SerializeField] private bool _createRootIfMissing = true;
        [SerializeField] private bool _resolveOnBuild = true;

        public void Install(IContainerBuilder builder)
        {
            RegisterRoot(builder);

            builder.Register<SoundService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            if (_resolveOnBuild)
            {
                builder.RegisterBuildCallback(container => container.Resolve<ISoundService>());
            }
        }

        private void RegisterRoot(IContainerBuilder builder)
        {
            if (_root == null)
            {
                _root = GetComponentInChildren<SoundServiceRoot>(true);
            }

            if (_root != null)
            {
                builder.RegisterInstance(_root);
                return;
            }

            if (_createRootIfMissing)
            {
                builder.RegisterComponentOnNewGameObject<SoundServiceRoot>(Lifetime.Singleton, "SoundServiceRoot")
                    .DontDestroyOnLoad()
                    .AsSelf();
                return;
            }

            builder.RegisterComponentInHierarchy<SoundServiceRoot>();
        }
    }
}
