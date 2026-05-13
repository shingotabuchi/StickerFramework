#if STICKER_DEBUG
using System;

namespace StickerFwk.Core.Debug
{
    internal sealed class CustomWidget : DebugWidget
    {
        public Action<DebugMenuRenderContext> Render_;

        public override void Render(DebugMenuRenderContext ctx)
        {
            Render_?.Invoke(ctx);
        }
    }
}
#endif
