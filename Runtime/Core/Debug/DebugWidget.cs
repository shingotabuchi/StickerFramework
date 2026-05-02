#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal abstract class DebugWidget
    {
        public abstract void Render(DebugMenuRenderContext ctx);
    }

    internal sealed class LabelWidget : DebugWidget
    {
        public string Text;
        public Func<string> DynamicText;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var text = DynamicText != null ? DynamicText() : Text;
            GUILayout.Label(text ?? string.Empty, ctx.Styles.Label);
        }
    }

    internal sealed class ButtonWidget : DebugWidget
    {
        public string Text;
        public Action OnClick;

        public override void Render(DebugMenuRenderContext ctx)
        {
            if (GUILayout.Button(Text, ctx.Styles.Button, ctx.Styles.WidgetHeight))
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
            var next = GUILayout.Toggle(current, " " + Text, ctx.Styles.Toggle);
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
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text, ctx.Styles.Label, GUILayout.Width(ctx.Styles.LabelWidth));
            var next = GUILayout.HorizontalSlider(current, Min, Max, GUILayout.ExpandWidth(true));
            GUILayout.Label(next.ToString("0.###"), ctx.Styles.Label, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
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
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text, ctx.Styles.Label, GUILayout.Width(ctx.Styles.LabelWidth));
            var next = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, Min, Max, GUILayout.ExpandWidth(true)));
            GUILayout.Label(next.ToString(), ctx.Styles.Label, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
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
            GUILayout.Label(Text, ctx.Styles.Label, GUILayout.Width(ctx.Styles.LabelWidth));
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
            GUILayout.Label(Text, ctx.Styles.Label, GUILayout.Width(ctx.Styles.LabelWidth));
            var next = GUILayout.SelectionGrid(current, Names, Mathf.Min(Names.Length, 3), ctx.Styles.Button);
            GUILayout.EndHorizontal();
            if (next != current)
            {
                SetIndex(next);
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
            if (GUILayout.Button(Text + "  ▶", ctx.Styles.Button, ctx.Styles.WidgetHeight))
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
