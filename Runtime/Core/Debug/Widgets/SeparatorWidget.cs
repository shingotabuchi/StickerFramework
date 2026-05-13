#if STICKER_DEBUG
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class SeparatorWidget : DebugWidget
    {
        public override void Render(DebugMenuRenderContext ctx)
        {
            GUILayout.Space(6f);
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, ctx.Styles.SeparatorColor, 0f, 0f);
            GUILayout.Space(6f);
        }
    }
}
#endif
