#if STICKER_DEBUG
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
        public GUIStyle Button;
        public GUIStyle ToggleButton;
        public GUIStyle SmallButton;
        public GUIStyle Toggle;
        public GUIStyle TextField;

        public Color SeparatorColor = new Color(1f, 1f, 1f, 0.2f);
        public float LabelWidth;
        public GUILayoutOption WidgetHeight;

        private bool _initialized;

        public void EnsureInitialized(DebugMenuSettings settings)
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            LabelWidth = settings.LabelWidth;
            WidgetHeight = GUILayout.Height(settings.WidgetHeight);

            Window = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
            TitleLabel = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = settings.TitleFontSize,
                alignment = TextAnchor.MiddleCenter
            };
            Label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = settings.FontSize };
            Button = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
                fontSize = settings.FontSize
            };
            ToggleButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4),
                fontSize = settings.ButtonFontSize,
                fontStyle = FontStyle.Bold
            };
            SmallButton = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(8, 8, 4, 4),
                fontSize = settings.FontSize
            };
            Toggle = new GUIStyle(GUI.skin.toggle)
            {
                padding = new RectOffset(20, 4, 2, 2),
                fontSize = settings.FontSize
            };
            TextField = new GUIStyle(GUI.skin.textField) { fontSize = settings.FontSize };
        }
    }
}
#endif
