#if STICKER_DEBUG
namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// A page contributed to the debug menu. Pages are plain C# classes registered via VContainer
    /// (<c>builder.Register&lt;IDebugPage, MyPage&gt;(Lifetime.Singleton)</c>); the menu auto-discovers
    /// them via collection injection and lists them on its root page.
    /// </summary>
    /// <remarks>
    /// <see cref="Build"/> is invoked once, the first time the page is shown. The resulting widget
    /// list is cached for the lifetime of the menu — pages should bind state via getter/setter
    /// delegates rather than rebuilding their layout per frame.
    /// </remarks>
    public interface IDebugPage
    {
        /// <summary>Display name shown in the title bar and as the default <c>PageLink</c> label.</summary>
        string Title { get; }

        /// <summary>Stable id used for persistence (last-open page) and lookup. e.g. <c>"stickerfwk.logs"</c>.</summary>
        string Id { get; }

        /// <summary>Sort order on the root page; lower values appear first. Ties are broken by <see cref="Title"/>.</summary>
        int Order { get; }

        /// <summary>Declare the page's widget list. Called once on first display.</summary>
        void Build(IDebugPageBuilder builder);
    }
}
#endif
