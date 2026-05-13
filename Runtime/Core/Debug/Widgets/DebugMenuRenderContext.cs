#if STICKER_DEBUG
namespace StickerFwk.Core.Debug
{
    internal readonly struct DebugMenuRenderContext
    {
        public readonly DebugMenuStyles Styles;
        public readonly IDebugMenuService Service;

        public DebugMenuRenderContext(DebugMenuStyles styles, IDebugMenuService service)
        {
            Styles = styles;
            Service = service;
        }
    }
}
#endif
