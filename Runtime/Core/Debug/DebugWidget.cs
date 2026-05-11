#if STICKER_DEBUG
using System;
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

    internal sealed class SliderWidget : DebugWidget
    {
        public string Text;
        public Func<float> Get;
        public Action<float> Set;
        public float Min;
        public float Max;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var current = Get();
            GUILayout.BeginVertical();
            OutlinedLabel($"{Text}: {current:0.###}", ctx.Styles.SliderLabel, ctx, GUILayout.ExpandWidth(true));
            var sliderRect = GUILayoutUtility.GetRect(0f, ctx.Styles.SliderHeightValue, GUILayout.ExpandWidth(true), ctx.Styles.SliderHeight);
            var next = DrawCenteredSlider(sliderRect, current, Min, Max, ctx);
            GUILayout.EndVertical();
            GUILayout.Space(SliderBottomMargin);
            if (!Mathf.Approximately(next, current))
            {
                Set(next);
            }
        }
    }

    internal sealed class IntSliderWidget : DebugWidget
    {
        public string Text;
        public Func<int> Get;
        public Action<int> Set;
        public int Min;
        public int Max;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var current = Get();
            GUILayout.BeginVertical();
            OutlinedLabel($"{Text}: {current}", ctx.Styles.SliderLabel, ctx, GUILayout.ExpandWidth(true));
            var sliderRect = GUILayoutUtility.GetRect(0f, ctx.Styles.SliderHeightValue, GUILayout.ExpandWidth(true), ctx.Styles.SliderHeight);
            var next = Mathf.RoundToInt(DrawCenteredSlider(sliderRect, current, Min, Max, ctx));
            GUILayout.EndVertical();
            GUILayout.Space(SliderBottomMargin);
            if (next != current)
            {
                Set(next);
            }
        }
    }

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

    internal sealed class DropdownWidget : DebugWidget
    {
        public string Text;
        public Func<System.Collections.Generic.IReadOnlyList<string>> GetOptions;
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

    internal sealed class CustomWidget : DebugWidget
    {
        public Action<DebugMenuRenderContext> Render_;

        public override void Render(DebugMenuRenderContext ctx)
        {
            Render_?.Invoke(ctx);
        }
    }

    internal readonly struct DebugMenuRenderContext
    {
        public readonly DebugMenuStyles Styles;
        public readonly IDebugMenuService Service;

        public DebugMenuRenderContext(DebugMenuStyles styles, IDebugMenuService service)
        {
            Styles = styles;
            Service = service;
        }
    }
}
#endif
