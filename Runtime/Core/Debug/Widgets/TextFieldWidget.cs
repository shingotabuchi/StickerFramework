#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class TextFieldWidget : DebugWidget
    {
        public string Text;
        public Func<string> Get;
        public Action<string> Set;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var current = Get() ?? string.Empty;
            GUILayout.BeginHorizontal();
            OutlinedLabel(Text, ctx.Styles.Label, ctx, GUILayout.Width(ctx.Styles.LabelWidth));
            var next = GUILayout.TextField(current, ctx.Styles.TextField, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (!string.Equals(next, current, StringComparison.Ordinal))
            {
                Set(next);
            }
        }
    }
}
#endif
