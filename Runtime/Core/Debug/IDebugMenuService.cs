#if STICKER_DEBUG
namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// Programmatic control over the debug menu. Inject this when code outside the menu itself needs
    /// to open/close it or push specific pages (e.g. a dev-only hotkey or a far-corner gesture).
    /// </summary>
    public interface IDebugMenuService
    {
        /// <summary>True while the menu panel is visible.</summary>
        bool IsOpen { get; }

        /// <summary>Show the panel. No-op if already open.</summary>
        void Open();

        /// <summary>Hide the panel and persist the current page. No-op if already closed.</summary>
        void Close();

        /// <summary>Toggles between <see cref="Open"/> and <see cref="Close"/>.</summary>
        void Toggle();

        /// <summary>Push a page onto the navigation stack so it becomes the visible page.</summary>
        void Push(IDebugPage page);

        /// <summary>Pop one level. No-op when already at the root page.</summary>
        void Pop();

        /// <summary>Pop all the way back to the root page.</summary>
        void PopToRoot();
    }
}
#endif
