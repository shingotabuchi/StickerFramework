#if STICKER_DEBUG
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal abstract class DebugWidget
    {
        protected const float SliderBottomMargin = 8f;
        protected const float ToggleLabelGap = 12f;
        protected const float ToggleRightMargin = 16f;

        public abstract void Render(DebugMenuRenderContext ctx);

        protected static void OutlinedLabel(string text, GUIStyle style, DebugMenuRenderContext ctx, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text ?? string.Empty);
            var rect = GUILayoutUtility.GetRect(content, style, options);
            ctx.Styles.DrawOutlinedLabel(rect, content, style);
        }

        protected static bool OutlinedButton(string text, GUIStyle style, DebugMenuRenderContext ctx, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text ?? string.Empty);
            var rect = GUILayoutUtility.GetRect(content, style, options);
            var clicked = GUI.Button(rect, GUIContent.none, style);
            ctx.Styles.DrawOutlinedLabel(rect, content, style);
            return clicked;
        }

        protected static void DrawToggleBox(Rect rect, bool isOn, DebugMenuRenderContext ctx)
        {
            var border = Mathf.Clamp(rect.width * 0.08f, 2f, 6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, ctx.Styles.TextOutlineColor, 0f, 0f);

            var fillRect = new Rect(
                rect.xMin + border,
                rect.yMin + border,
                Mathf.Max(0f, rect.width - border * 2f),
                Mathf.Max(0f, rect.height - border * 2f));
            var fillColor = isOn ? ctx.Styles.TextColor : ctx.Styles.SeparatorColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, fillColor, 0f, 0f);

            if (!isOn)
            {
                return;
            }

            var checkStyle = new GUIStyle(ctx.Styles.Toggle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(rect.height * 0.72f),
                fontStyle = FontStyle.Bold
            };
            SetStyleTextColor(checkStyle, ctx.Styles.TextOutlineColor);
            GUI.Label(rect, "✓", checkStyle);
        }

        protected static void SetStyleTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        protected static float DrawCenteredSlider(Rect rect, float current, float min, float max, DebugMenuRenderContext ctx)
        {
            if (max <= min)
            {
                return min;
            }

            var controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            var next = Mathf.Clamp(current, min, max);
            var evt = Event.current;

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                next = SliderValueFromMouse(rect, evt.mousePosition.x, min, max, ctx.Styles.SliderHandleSize);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                next = SliderValueFromMouse(rect, evt.mousePosition.x, min, max, ctx.Styles.SliderHandleSize);
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                evt.Use();
            }

            var trackHeight = Mathf.Max(1f, ctx.Styles.SliderGaugeHeight);
            var trackRect = new Rect(rect.xMin, rect.center.y - trackHeight * 0.5f, rect.width, trackHeight);
            GUI.DrawTexture(trackRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, ctx.Styles.SliderGaugeColor, 0f, 0f);

            var thumbSize = ctx.Styles.SliderHandleSize;
            var minX = rect.xMin + thumbSize * 0.5f;
            var maxX = rect.xMax - thumbSize * 0.5f;
            if (maxX < minX)
            {
                minX = rect.xMin;
                maxX = rect.xMax;
            }

            var normalized = Mathf.InverseLerp(min, max, next);
            var thumbCenterX = Mathf.Lerp(minX, maxX, normalized);
            var thumbRect = new Rect(thumbCenterX - thumbSize * 0.5f, rect.center.y - thumbSize * 0.5f, thumbSize, thumbSize);
            GUI.DrawTexture(thumbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, ctx.Styles.SliderHandleColor, 0f, 0f);

            return next;
        }

        private static float SliderValueFromMouse(Rect rect, float mouseX, float min, float max, float thumbSize)
        {
            var minX = rect.xMin + thumbSize * 0.5f;
            var maxX = rect.xMax - thumbSize * 0.5f;
            if (maxX < minX)
            {
                minX = rect.xMin;
                maxX = rect.xMax;
            }

            return Mathf.Lerp(min, max, Mathf.InverseLerp(minX, maxX, mouseX));
        }
    }
}
#endif
