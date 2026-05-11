#if STICKER_DEBUG
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// ScriptableObject configuring the debug menu overlay (toggle button placement, panel size,
    /// font sizes, scaling). Create one via <c>Assets &gt; Create &gt; Sticker &gt; Framework &gt;
    /// Debug Menu Settings</c>, or edit the project-level asset from Project Settings.
    /// </summary>
    /// <remarks>
    /// If no asset is assigned and no project-level asset exists,
    /// <see cref="DebugMenuContainerBuilderExtensions.UseDebugMenu"/> falls back to
    /// <see cref="Default"/>, an in-memory instance with sensible defaults.
    /// </remarks>
    [CreateAssetMenu(menuName = "Sticker/Framework/Debug Menu Settings", fileName = "DebugMenuSettings")]
    public sealed class DebugMenuSettings : ScriptableObject
    {
        public const string ResourcesPath = "Sticker/DebugMenuSettings";
        public const string ProjectSettingsAssetPath = "Assets/Resources/Sticker/DebugMenuSettings.asset";
        public const string LegacyProjectSettingsAssetPath = "Assets/Settings/DebugMenuSettings.asset";

        [Header("Toggle Button")]
        [SerializeField] private DebugMenuButtonCorner _buttonCorner = DebugMenuButtonCorner.BottomLeft;
        [SerializeField, Min(16f)] private float _buttonSize = 80f;
        [SerializeField, Min(0.1f)] private float _buttonAspect = 3f;
        [SerializeField, Range(8, 64)] private int _buttonFontSize = 50;
        [SerializeField] private string _buttonText = "Debug";
        [SerializeField, Min(0f)] private float _buttonMargin = 12f;
        [SerializeField] private Color _buttonColor = new Color(0f, 0f, 0f, 0.85f);

        [Header("Panel")]
        [SerializeField] private DebugMenuButtonCorner _panelCorner = DebugMenuButtonCorner.BottomLeft;
        [SerializeField, Min(160f)] private float _panelWidth = 880f;
        [SerializeField, Min(0f)] private float _panelMargin = 12f;
        [SerializeField] private bool _panelFillScreenHeight = true;
        [SerializeField, Min(120f)] private float _panelMaxHeight = 720f;

        [Header("Typography")]
        [SerializeField, Range(0.5f, 3f)] private float _uiScale = 1f;
        [Tooltip("Reference screen height the menu is designed for. Effective scale = (Screen.height / ReferenceScreenHeight) × UiScale. Set to 0 to disable screen scaling.")]
        [SerializeField, Min(0f)] private float _referenceScreenHeight = 1080f;
        [SerializeField, Range(8, 100)] private int _fontSize = 40;
        [SerializeField, Range(8, 100)] private int _titleFontSize = 48;

        [Header("Layout")]
        [SerializeField, Min(16f)] private float _widgetHeight = 80f;
        [SerializeField, Min(8f)] private float _toggleSize = 52f;
        [SerializeField, Min(8f)] private float _sliderSize = 52f;
        [SerializeField, Range(0.1f, 3f)] private float _sliderHandleSizeRatio = 1.26f;
        [SerializeField, Range(0.05f, 2f)] private float _sliderGaugeSizeRatio = 0.76f;
        [SerializeField, Min(8f)] private float _scrollBarWidth = 60f;
        [SerializeField, Min(40f)] private float _labelWidth = 300f;

        [Header("Colors")]
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _textOutlineColor = Color.black;
        [SerializeField, Min(0f)] private float _textOutlineThickness = 1f;
        [SerializeField] private Color _panelBackgroundColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _separatorColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Color _sliderGaugeColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _sliderHandleColor = new Color(1f, 1f, 1f, 0.65f);

        public DebugMenuButtonCorner ButtonCorner => _buttonCorner;
        /// <summary>Button height in pixels (before <see cref="UiScale"/>).</summary>
        public float ButtonSize => _buttonSize;

        /// <summary>Button width-to-height ratio. 1 = square, 2.4 ≈ pill.</summary>
        public float ButtonAspect => _buttonAspect;

        /// <summary>Button width derived from <see cref="ButtonSize"/> × <see cref="ButtonAspect"/>.</summary>
        public float ButtonWidth => _buttonSize * _buttonAspect;

        /// <summary>Button height (alias for <see cref="ButtonSize"/>).</summary>
        public float ButtonHeight => _buttonSize;

        /// <summary>Font size used for the floating toggle button label.</summary>
        public int ButtonFontSize => _buttonFontSize;
        public string ButtonText => string.IsNullOrEmpty(_buttonText) ? "Debug" : _buttonText;
        public float ButtonMargin => _buttonMargin;

        /// <summary>Background color used by the floating button that opens the debug menu.</summary>
        public Color ButtonColor => _buttonColor;

        public DebugMenuButtonCorner PanelCorner => _panelCorner;
        public float PanelWidth => _panelWidth;
        public float PanelMargin => _panelMargin;
        public bool PanelFillScreenHeight => _panelFillScreenHeight;
        public float PanelMaxHeight => _panelMaxHeight;
        public float UiScale => _uiScale <= 0f ? 1f : _uiScale;

        /// <summary>Design-target screen height. 0 disables screen-relative scaling.</summary>
        public float ReferenceScreenHeight => _referenceScreenHeight;
        public int FontSize => _fontSize > 0 ? _fontSize : 16;
        public int TitleFontSize => _titleFontSize > 0 ? _titleFontSize : 24;
        public float WidgetHeight => _widgetHeight;
        public float ToggleSize => Mathf.Max(8f, _toggleSize);
        public float SliderSize => _sliderSize;
        public float SliderHandleSizeRatio => _sliderHandleSizeRatio <= 0f ? 1f : _sliderHandleSizeRatio;
        public float SliderGaugeSizeRatio => _sliderGaugeSizeRatio <= 0f ? 0.35f : _sliderGaugeSizeRatio;
        public float ScrollBarWidth => Mathf.Max(8f, _scrollBarWidth);
        public float LabelWidth => _labelWidth;

        /// <summary>Color used for debug menu text.</summary>
        public Color TextColor => _textColor;

        /// <summary>Color used for the debug menu text outline.</summary>
        public Color TextOutlineColor => _textOutlineColor;

        /// <summary>Outline thickness in IMGUI pixels. 0 disables text outlines.</summary>
        public float TextOutlineThickness => Mathf.Max(0f, _textOutlineThickness);

        /// <summary>Background color used to fill the panel area. Alpha controls translucency.</summary>
        public Color PanelBackgroundColor => _panelBackgroundColor;

        /// <summary>Color of the thin divider drawn by <c>Separator()</c> widgets.</summary>
        public Color SeparatorColor => _separatorColor;

        /// <summary>Color of the slider gauge track.</summary>
        public Color SliderGaugeColor => _sliderGaugeColor;

        /// <summary>Color of the slider handle.</summary>
        public Color SliderHandleColor => _sliderHandleColor;

        private static DebugMenuSettings _default;

        /// <summary>In-memory instance with built-in defaults; used when no asset is assigned.</summary>
        public static DebugMenuSettings Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<DebugMenuSettings>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    _default.name = "DebugMenuSettings (Default)";
                }
                return _default;
            }
        }

        /// <summary>
        /// Loads the project-level settings asset used by the Project Settings provider, or returns
        /// <see cref="Default"/> when the asset has not been created yet.
        /// </summary>
        public static DebugMenuSettings LoadProjectSettingsOrDefault()
        {
            var settings = Resources.Load<DebugMenuSettings>(ResourcesPath);
            if (settings != null)
            {
                return settings;
            }

#if UNITY_EDITOR
            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<DebugMenuSettings>(ProjectSettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }

            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<DebugMenuSettings>(LegacyProjectSettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }
#endif
            return Default;
        }
    }
}
#endif
