#if STICKER_DEBUG
namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// Optional extension point for pages that need a direct action on the debug menu root.
    /// </summary>
    public interface IDebugRootPageContributor
    {
        void BuildRoot(IDebugPageBuilder builder);
    }
}
#endif
