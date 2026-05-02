#if STICKER_DEBUG
using StickerFwk.Core.Debug.Pages;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// VContainer integration for the debug menu. Call <see cref="UseDebugMenu"/> from the root
    /// <c>LifetimeScope</c> (typically inside <c>#if STICKER_DEBUG</c>) to install the overlay and
    /// the built-in pages.
    /// </summary>
    public static class DebugMenuContainerBuilderExtensions
    {
        /// <summary>
        /// Spawns the <see cref="DebugMenuService"/> overlay <c>GameObject</c> (kept alive via
        /// <c>DontDestroyOnLoad</c>), registers built-in pages such as <see cref="LogsDebugPage"/>,
        /// and force-resolves the service so the corner toggle button shows up at startup.
        /// </summary>
        /// <param name="builder">The VContainer builder for the host scope.</param>
        /// <param name="settings">Optional layout / sizing settings. Defaults to <see cref="DebugMenuSettings.Default"/>.</param>
        /// <remarks>
        /// Any feature can contribute additional pages by registering them as
        /// <c>IDebugPage</c>: <c>builder.Register&lt;IDebugPage, MyPage&gt;(Lifetime.Singleton)</c>.
        /// VContainer collects all such registrations and injects them into the overlay.
        /// </remarks>
        public static void UseDebugMenu(this IContainerBuilder builder, DebugMenuSettings settings = null)
        {
            builder.RegisterInstance(settings ?? DebugMenuSettings.Default);

            builder.RegisterComponentOnNewGameObject<DebugMenuService>(Lifetime.Singleton, "DebugMenuOverlay")
                .DontDestroyOnLoad()
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<LogsDebugPage>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            // Force the overlay GameObject to spawn at container build time so the corner toggle
            // button is visible immediately, without waiting for someone else to resolve it.
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<DebugMenuService>();
            });
        }
    }
}
#endif
