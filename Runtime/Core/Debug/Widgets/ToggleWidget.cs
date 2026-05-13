#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class ToggleWidget : DebugWidget
    {
        public string Text;
        public Func<bool> Get;
        public Action<bool> Set;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var current = Get();
            var rowHeight = Mathf.Max(ctx.Styles.WidgetHeightValue, ctx.Styles.ToggleSize);
            var rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            var toggleSize = Mathf.Min(ctx.Styles.ToggleSize, rowRect.height, rowRect.width);
            var toggleRect = new Rect(
                rowRect.xMax - ToggleRightMargin - toggleSize,
                rowRect.center.y - toggleSize * 0.5f,
                toggleSize,
                toggleSize);
            var labelRect = new Rect(
                rowRect.xMin,
                rowRect.yMin,
                Mathf.Max(0f, toggleRect.xMin - rowRect.xMin - ToggleLabelGap),
                rowRect.height);
            var next = GUI.Button(rowRect, GUIContent.none, GUIStyle.none) ? !current : current;
            ctx.Styles.DrawOutlinedLabel(labelRect, Text, ctx.Styles.Toggle);
            DrawToggleBox(toggleRect, next, ctx);
            if (next != current)
            {
                Set(next);
            }
        }
    }
}
#endif
