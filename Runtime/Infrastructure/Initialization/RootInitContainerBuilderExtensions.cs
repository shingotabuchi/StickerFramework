using StickerFwk.Core.Initialization;
using VContainer;

namespace StickerFwk.Infrastructure.Initialization
{
    /// <summary>
    /// VContainer integration for the root initialization sequence. Call <see cref="UseRootInit"/>
    /// from the root <c>LifetimeScope</c> to register both the settings asset and
    /// <see cref="RootInitService"/>.
    /// </summary>
    public static class RootInitContainerBuilderExtensions
    {
        /// <summary>
        /// Registers <see cref="RootInitSettings"/> (falling back to
        /// <see cref="RootInitSettings.Default"/>) and <see cref="RootInitService"/> as the
        /// <see cref="IRootInitService"/> implementation.
        /// </summary>
        /// <param name="builder">The VContainer builder for the root scope.</param>
        /// <param name="settings">Optional settings asset. Defaults to
        /// <see cref="RootInitSettings.Default"/> which leaves
        /// <see cref="UnityEngine.Application.targetFrameRate"/> at the platform default.</param>
        public static void UseRootInit(this IContainerBuilder builder, RootInitSettings settings = null)
        {
            builder.RegisterInstance(settings != null ? settings : RootInitSettings.Default);
            builder.Register<RootInitService>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}
