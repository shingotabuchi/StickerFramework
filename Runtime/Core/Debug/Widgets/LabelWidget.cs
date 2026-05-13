#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class LabelWidget : DebugWidget
    {
        public string Text;
        public Func<string> DynamicText;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var text = DynamicText != null ? DynamicText() : Text;
            OutlinedLabel(text, ctx.Styles.Label, ctx, GUILayout.ExpandWidth(true));
        }
    }
}
#endif
