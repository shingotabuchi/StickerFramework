#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class PageLinkWidget : DebugWidget
    {
        public string Text;
        public IDebugPage Target;
        public Func<IDebugPage> Factory;

        public override void Render(DebugMenuRenderContext ctx)
        {
            if (OutlinedButton(Text + "  ▶", ctx.Styles.Button, ctx, GUILayout.ExpandWidth(true), ctx.Styles.WidgetHeight))
            {
                var target = Target ?? Factory?.Invoke();
                if (target != null)
                {
                    ctx.Service.Push(target);
                }
            }
        }
    }
}
#endif
