#if STICKER_DEBUG
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// ScriptableObject configuring the debug menu overlay (toggle button placement, panel size,
    /// font sizes, scaling). Create one via <c>Assets &gt; Create &gt; Sticker &gt; Framework &gt;
    /// Debug Menu Settings</c>, then reference it from your root <c>LifetimeScope</c>.
    /// </summary>
    /// <remarks>
    /// If no asset is assigned, <see cref="DebugMenuContainerBuilderExtensions.UseDebugMenu"/>
    /// falls back to <see cref="Default"/>, an in-memory instance with sensible defaults.
    /// </remarks>
    [CreateAssetMenu(menuName = "Sticker/Framework/Debug Menu Settings", fileName = "DebugMenuSettings")]
    public sealed class DebugMenuSettings : ScriptableObject
    {
        [Header("Toggle Button")]
        [SerializeField] private DebugMenuButtonCorner _buttonCorner = DebugMenuButtonCorner.TopRight;
        [SerializeField, Min(16f)] private float _buttonSize = 36f;
        [SerializeField, Min(0.1f)] private float _buttonAspect = 4.5f;
        [SerializeField, Range(8, 64)] private int _buttonFontSize = 20;
        [SerializeField] private string _buttonText = "Debug";
        [SerializeField, Min(0f)] private float _buttonMargin = 12f;

        [Header("Panel")]
        [SerializeField, Min(160f)] private float _panelWidth = 380f;
        [SerializeField, Min(0f)] private float _panelMargin = 12f;
        [SerializeField] private bool _panelFillScreenHeight = true;
        [SerializeField, Min(120f)] private float _panelMaxHeight = 720f;

        [Header("Typography")]
        [SerializeField, Range(0.5f, 3f)] private float _uiScale = 1f;
        [Tooltip("Reference screen height the menu is designed for. Effective scale = (Screen.height / ReferenceScreenHeight) × UiScale. Set to 0 to disable screen scaling.")]
        [SerializeField, Min(0f)] private float _referenceScreenHeight = 1080f;
        [SerializeField, Range(8, 40)] private int _fontSize = 16;
        [SerializeField, Range(8, 48)] private int _titleFontSize = 24;

        [Header("Layout")]
        [SerializeField, Min(16f)] private float _widgetHeight = 40f;
        [SerializeField, Min(40f)] private float _labelWidth = 140f;

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
        public float LabelWidth => _labelWidth;

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
    }
}
#endif
