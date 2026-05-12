#if STICKER_DEBUG
using System.Text;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// Cached <c>GUIStyle</c>s for the debug overlay. Populated on first <c>OnGUI</c> from the
    /// active <see cref="DebugMenuSettings"/> so font sizes and label widths reflect user config.
    /// </summary>
    internal sealed class DebugMenuStyles
    {
        public GUIStyle Window;
        public GUIStyle TitleLabel;
        public GUIStyle Label;
        public GUIStyle SliderLabel;
        public GUIStyle Button;
        public GUIStyle ToggleButton;
        public GUIStyle SmallButton;
        public GUIStyle Toggle;
        public GUIStyle TextField;
        public GUIStyle HorizontalScrollbar;
        public GUIStyle VerticalScrollbar;
        public GUISkin ScrollViewSkin;

        public Color SeparatorColor = new Color(1f, 1f, 1f, 0.2f);
        public Color SliderGaugeColor = new Color(0f, 0f, 0f, 0.85f);
        public Color SliderHandleColor = new Color(1f, 1f, 1f, 0.65f);
        public Color TextColor = Color.white;
        public Color TextOutlineColor = Color.black;
        public float TextOutlineThickness = 1f;
        public float LabelWidth;
        public float SliderSize;
        public float SliderHandleSize;
        public float SliderGaugeHeight;
        public float SliderHeightValue;
        public float ToggleSize;
        public float WidgetHeightValue;
        public float ScrollBarWidth;
        public GUILayoutOption WidgetHeight;
        public GUILayoutOption SliderHeight;

        private bool _initialized;
        private Texture2D _panelBackgroundTexture;
        private Texture2D _buttonBackgroundTexture;
        private Texture2D _scrollbarTrackTexture;
        private Texture2D _scrollbarThumbTexture;
        private Color _cachedPanelBackgroundColor;
        private Color _cachedButtonColor;

        public void EnsureInitialized(DebugMenuSettings settings)
        {
            if (!_initialized)
            {
                _initialized = true;

                Window = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
                TitleLabel = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                Label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                SliderLabel = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = false,
                    alignment = TextAnchor.MiddleLeft
                };
                Button = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 6, 6)
                };
                ToggleButton = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 4, 4),
                    fontStyle = FontStyle.Bold
                };
                SmallButton = new GUIStyle(GUI.skin.button)
                {
                    padding = new RectOffset(8, 8, 4, 4)
                };
                Toggle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
                TextField = new GUIStyle(GUI.skin.textField);
                ScrollViewSkin = Object.Instantiate(GUI.skin);
                ScrollViewSkin.hideFlags = HideFlags.HideAndDontSave;
                HorizontalScrollbar = new GUIStyle(ScrollViewSkin.horizontalScrollbar);
                VerticalScrollbar = new GUIStyle(ScrollViewSkin.verticalScrollbar);
            }

            // Live-refresh layout and typography so Project Settings tweaks apply during Play.
            LabelWidth = settings.LabelWidth;
            SliderSize = settings.SliderSize;
            SliderHandleSize = Mathf.Max(1f, SliderSize * settings.SliderHandleSizeRatio);
            SliderGaugeHeight = Mathf.Max(1f, SliderSize * settings.SliderGaugeSizeRatio);
            SliderHeightValue = Mathf.Max(SliderSize, SliderHandleSize, SliderGaugeHeight);
            ToggleSize = settings.ToggleSize;
            WidgetHeightValue = settings.WidgetHeight;
            ScrollBarWidth = settings.ScrollBarWidth;
            WidgetHeight = GUILayout.Height(settings.WidgetHeight);
            SliderHeight = GUILayout.Height(SliderHeightValue);
            ApplyScrollbarWidth();
            TitleLabel.fontSize = settings.TitleFontSize;
            Label.fontSize = settings.FontSize;
            SliderLabel.fontSize = settings.FontSize;
            Button.fontSize = settings.FontSize;
            ToggleButton.fontSize = settings.ButtonFontSize;
            SmallButton.fontSize = settings.FontSize;
            Toggle.fontSize = settings.FontSize;
            TextField.fontSize = settings.FontSize;

            TextColor = settings.TextColor;
            TextOutlineColor = settings.TextOutlineColor;
            TextOutlineThickness = settings.TextOutlineThickness;
            ApplyTextColor(TitleLabel, TextColor);
            ApplyTextColor(Label, TextColor);
            ApplyTextColor(SliderLabel, TextColor);
            ApplyTextColor(Button, TextColor);
            ApplyTextColor(ToggleButton, TextColor);
            ApplyTextColor(SmallButton, TextColor);
            ApplyTextColor(Toggle, TextColor);
            ApplyTextColor(TextField, TextColor);
            SeparatorColor = settings.SeparatorColor;
            SliderGaugeColor = settings.SliderGaugeColor;
            SliderHandleColor = settings.SliderHandleColor;
            if (settings.ButtonColor != _cachedButtonColor || _buttonBackgroundTexture == null)
            {
                _cachedButtonColor = settings.ButtonColor;
                if (_buttonBackgroundTexture != null)
                {
                    Object.Destroy(_buttonBackgroundTexture);
                }
                _buttonBackgroundTexture = CreateSolidTexture(_cachedButtonColor);
                _buttonBackgroundTexture.name = "DebugMenuButtonBackground";
                SetBackground(ToggleButton, _buttonBackgroundTexture);
            }
            if (settings.PanelBackgroundColor != _cachedPanelBackgroundColor || _panelBackgroundTexture == null)
            {
                _cachedPanelBackgroundColor = settings.PanelBackgroundColor;
                if (_panelBackgroundTexture != null)
                {
                    Object.Destroy(_panelBackgroundTexture);
                }
                _panelBackgroundTexture = CreateSolidTexture(_cachedPanelBackgroundColor);
                if (Window != null)
                {
                    Window.normal.background = _panelBackgroundTexture;
                }
            }
        }

        private void ApplyScrollbarWidth()
        {
            if (ScrollViewSkin == null)
            {
                return;
            }

            if (_scrollbarTrackTexture == null)
            {
                _scrollbarTrackTexture = CreateSolidTexture(new Color(1f, 1f, 1f, 0.14f));
                _scrollbarTrackTexture.name = "DebugMenuScrollbarTrack";
            }

            if (_scrollbarThumbTexture == null)
            {
                _scrollbarThumbTexture = CreateSolidTexture(new Color(1f, 1f, 1f, 0.48f));
                _scrollbarThumbTexture.name = "DebugMenuScrollbarThumb";
            }

            ConfigureHorizontalScrollbar(HorizontalScrollbar, ScrollBarWidth, _scrollbarTrackTexture);
            ConfigureVerticalScrollbar(VerticalScrollbar, ScrollBarWidth, _scrollbarTrackTexture);

            ScrollViewSkin.horizontalScrollbar = HorizontalScrollbar;
            ScrollViewSkin.verticalScrollbar = VerticalScrollbar;
            ConfigureHorizontalScrollbar(ScrollViewSkin.horizontalScrollbarThumb, ScrollBarWidth, _scrollbarThumbTexture);
            ConfigureVerticalScrollbar(ScrollViewSkin.verticalScrollbarThumb, ScrollBarWidth, _scrollbarThumbTexture);
            ConfigureHorizontalScrollbarButton(ScrollViewSkin.horizontalScrollbarLeftButton);
            ConfigureHorizontalScrollbarButton(ScrollViewSkin.horizontalScrollbarRightButton);
            ConfigureVerticalScrollbarButton(ScrollViewSkin.verticalScrollbarUpButton);
            ConfigureVerticalScrollbarButton(ScrollViewSkin.verticalScrollbarDownButton);
        }

        private static void ConfigureHorizontalScrollbar(GUIStyle style, float width, Texture2D background)
        {
            if (style == null)
            {
                return;
            }

            style.fixedHeight = width;
            style.stretchHeight = false;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.overflow = new RectOffset(0, 0, 0, 0);
            SetBackground(style, background);
        }

        private static void ConfigureVerticalScrollbar(GUIStyle style, float width, Texture2D background)
        {
            if (style == null)
            {
                return;
            }

            style.fixedWidth = width;
            style.stretchWidth = false;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.overflow = new RectOffset(0, 0, 0, 0);
            SetBackground(style, background);
        }

        private static void ConfigureHorizontalScrollbarButton(GUIStyle style)
        {
            if (style == null)
            {
                return;
            }

            style.fixedWidth = 0f;
            style.stretchWidth = false;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.overflow = new RectOffset(0, 0, 0, 0);
            SetBackground(style, null);
        }

        private static void ConfigureVerticalScrollbarButton(GUIStyle style)
        {
            if (style == null)
            {
                return;
            }

            style.fixedHeight = 0f;
            style.stretchHeight = false;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.overflow = new RectOffset(0, 0, 0, 0);
            SetBackground(style, null);
        }

        private static void SetBackground(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        private static void ApplyTextColor(GUIStyle style, Color color)
        {
            if (style == null)
            {
                return;
            }

            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        public void DrawOutlinedLabel(Rect rect, string text, GUIStyle style)
        {
            DrawOutlinedLabel(rect, new GUIContent(text ?? string.Empty), style);
        }

        public void DrawOutlinedLabel(Rect rect, GUIContent content, GUIStyle style)
        {
            if (content == null || style == null)
            {
                return;
            }

            var drawStyle = new GUIStyle(style);
            var thickness = TextOutlineThickness;
            if (thickness > 0f)
            {
                var outlineStyle = new GUIStyle(drawStyle) { richText = false };
                ApplyTextColor(outlineStyle, TextOutlineColor);
                var outlineContent = content.image != null
                    ? content
                    : new GUIContent(StripRichText(content.text), content.tooltip);
                var wholePixels = Mathf.CeilToInt(thickness);
                for (var i = 1; i <= wholePixels; i++)
                {
                    var offset = Mathf.Min(i, thickness);
                    GUI.Label(new Rect(rect.x - offset, rect.y, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x + offset, rect.y, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x, rect.y - offset, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x, rect.y + offset, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x - offset, rect.y - offset, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x + offset, rect.y - offset, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x - offset, rect.y + offset, rect.width, rect.height), outlineContent, outlineStyle);
                    GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), outlineContent, outlineStyle);
                }
            }

            ApplyTextColor(drawStyle, TextColor);
            GUI.Label(rect, content, drawStyle);
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "DebugMenuPanelBackground",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.IndexOf('<') < 0)
            {
                return text;
            }
            var sb = new StringBuilder(text.Length);
            var inTag = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }
                if (c == '>')
                {
                    inTag = false;
                    continue;
                }
                if (!inTag)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
#endif
