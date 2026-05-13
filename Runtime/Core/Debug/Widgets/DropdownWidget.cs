#if STICKER_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class DropdownWidget : DebugWidget
    {
        public string Text;
        public Func<IReadOnlyList<string>> GetOptions;
        public Func<int> GetIndex;
        public Action<int> SetIndex;
        private bool _expanded;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var options = GetOptions != null ? GetOptions() : null;
            var current = GetIndex != null ? GetIndex() : -1;
            var label = options != null && current >= 0 && current < options.Count
                ? options[current]
                : "(none)";

            GUILayout.BeginHorizontal();
            OutlinedLabel(Text, ctx.Styles.Label, ctx, GUILayout.Width(ctx.Styles.LabelWidth));
            if (OutlinedButton(label + (_expanded ? "  ▲" : "  ▼"), ctx.Styles.Button, ctx, GUILayout.ExpandWidth(true), ctx.Styles.WidgetHeight))
            {
                _expanded = !_expanded;
            }
            GUILayout.EndHorizontal();

            if (_expanded && options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var marker = i == current ? "● " : "   ";
                    if (OutlinedButton(marker + options[i], ctx.Styles.Button, ctx, GUILayout.ExpandWidth(true), ctx.Styles.WidgetHeight))
                    {
                        SetIndex?.Invoke(i);
                        _expanded = false;
                    }
                }
            }
        }
    }
}
#endif
