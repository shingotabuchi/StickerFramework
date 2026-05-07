#if STICKER_DEBUG
using System;
using VContainer.Unity;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// Registers a single <see cref="IDebugPage"/> with the global <see cref="IDebugMenuService"/>
    /// while a child <c>LifetimeScope</c> is alive. The page is added on initialization and removed
    /// on disposal, so a feature's debug page only appears in the menu while that feature's scope
    /// exists (e.g. only while the gameplay scene is loaded).
    /// </summary>
    /// <remarks>
    /// Use <see cref="DebugMenuContainerBuilderExtensions.AddScopedDebugPage{TPage}"/> from a child
    /// <c>LifetimeScope</c>'s <c>ConfigureScope</c> to wire this up declaratively. The generic
    /// parameter ensures the registrar resolves only its own page — not all <see cref="IDebugPage"/>
    /// registrations cascading from the parent scope.
    /// </remarks>
    public sealed class ScopedDebugPageRegistrar<TPage> : IInitializable, IDisposable
        where TPage : class, IDebugPage
    {
        private readonly IDebugMenuService _menu;
        private readonly TPage _page;
        private bool _registered;

        public ScopedDebugPageRegistrar(IDebugMenuService menu, TPage page)
        {
            _menu = menu;
            _page = page;
        }

        public void Initialize()
        {
            if (_registered || _menu == null || _page == null)
            {
                return;
            }

            _registered = true;
            _menu.RegisterPage(_page);
        }

        public void Dispose()
        {
            if (!_registered || _menu == null || _page == null)
            {
                return;
            }

            _registered = false;
            _menu.UnregisterPage(_page);
        }
    }
}
#endif
