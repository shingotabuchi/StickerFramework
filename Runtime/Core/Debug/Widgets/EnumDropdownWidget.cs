#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class EnumDropdownWidget : DebugWidget
    {
        public string Text;
        public string[] Names;
        public Func<int> GetIndex;
        public Action<int> SetIndex;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var current = GetIndex();
            GUILayout.BeginHorizontal();
            OutlinedLabel(Text, ctx.Styles.Label, ctx, GUILayout.Width(ctx.Styles.LabelWidth));
            var next = GUILayout.SelectionGrid(current, Names, Mathf.Min(Names.Length, 3), ctx.Styles.Button);
            GUILayout.EndHorizontal();
            if (next != current)
            {
                SetIndex(next);
            }
        }
    }
}
#endif
