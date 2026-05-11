using VContainer;
using VContainer.Unity;

namespace StickerFwk.Infrastructure.Initialization
{
    // Base LifetimeScope that auto-installs any IInstaller MonoBehaviours attached to the same
    // GameObject before delegating to the subclass. This lets drop-in installer
    // components contribute bindings without each scope having to remember to iterate
    // GetComponents<IInstaller>() manually.
    public abstract class StickerLifetimeScope : LifetimeScope
    {
        protected sealed override void Configure(IContainerBuilder builder)
        {
            foreach (var installer in GetComponents<IInstaller>())
            {
                installer.Install(builder);
            }

            ConfigureScope(builder);
        }

        protected abstract void ConfigureScope(IContainerBuilder builder);
    }
}
