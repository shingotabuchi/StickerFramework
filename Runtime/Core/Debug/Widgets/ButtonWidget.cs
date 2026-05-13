#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class ButtonWidget : DebugWidget
    {
        public string Text;
        public Action OnClick;

        public override void Render(DebugMenuRenderContext ctx)
        {
            if (OutlinedButton(Text, ctx.Styles.Button, ctx, GUILayout.ExpandWidth(true), ctx.Styles.WidgetHeight))
            {
                OnClick?.Invoke();
            }
        }
    }
}
#endif
