#if STICKER_DEBUG
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// IMGUI overlay that renders the debug menu. Owns the navigation stack of pages, the cached
    /// widget lists, and the on-screen toggle button. Layout, sizing, font and button placement
    /// are all driven by <see cref="DebugMenuSettings"/> resolved from the container.
    /// </summary>
    /// <remarks>
    /// Spawned via <see cref="DebugMenuContainerBuilderExtensions.UseDebugMenu"/> on a dedicated
    /// <c>DontDestroyOnLoad</c> GameObject so it survives scene transitions. Rendered on top of
    /// everything else via <c>OnGUI</c>; intentionally does not piggy-back on the gameplay
    /// <c>UIService</c> so it never collides with window z-order or input locks.
    /// </remarks>
    public sealed class DebugMenuService : MonoBehaviour, IDebugMenuService
    {
        private const string LastPageIdKey = "StickerFwk.DebugMenu.LastPageId";

        private readonly DebugMenuStyles _styles = new DebugMenuStyles();
        private readonly Stack<IDebugPage> _stack = new Stack<IDebugPage>();
        private readonly Dictionary<IDebugPage, List<DebugWidget>> _widgetsByPage = new Dictionary<IDebugPage, List<DebugWidget>>();

        private DebugMenuSettings _settings;
        private IReadOnlyList<IDebugPage> _registeredPages;
        private RootDebugPage _rootPage;
        private bool _isOpen;
        private Vector2 _scrollPosition;
        private DebugMenuRenderContext _ctx;

        public bool IsOpen => _isOpen;

        /// <summary>
        /// Method-injected by VContainer with the active settings and all <see cref="IDebugPage"/>s
        /// registered in the container.
        /// </summary>
        [Inject]
        public void Construct(DebugMenuSettings settings, IReadOnlyList<IDebugPage> pages)
        {
            _settings = settings ?? DebugMenuSettings.Default;
            _registeredPages = pages ?? new List<IDebugPage>();
            _rootPage = new RootDebugPage(_registeredPages);
        }

        private void Start()
        {
            _ctx = new DebugMenuRenderContext(_styles, this);

            // Restore the last-open page (one level deep only — multi-level paths aren't persisted).
            var lastId = PlayerPrefs.GetString(LastPageIdKey, string.Empty);
            if (!string.IsNullOrEmpty(lastId))
            {
                var page = FindPage(lastId);
                if (page != null)
                {
                    _stack.Push(_rootPage);
                    _stack.Push(page);
                    return;
                }
            }
            _stack.Push(_rootPage);
        }

        public void Open()
        {
            if (_isOpen)
            {
                return;
            }
            _isOpen = true;
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }
            _isOpen = false;
            SaveLastPage();
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Push(IDebugPage page)
        {
            if (page == null)
            {
                return;
            }
            _stack.Push(page);
        }

        public void Pop()
        {
            // Guard against popping the implicit root page.
            if (_stack.Count > 1)
            {
                _stack.Pop();
            }
        }

        public void PopToRoot()
        {
            while (_stack.Count > 1)
            {
                _stack.Pop();
            }
        }

        private void OnGUI()
        {
            _styles.EnsureInitialized(_settings);

            // Effective scale = uiScale × (screenHeight / referenceScreenHeight) so the menu
            // grows on larger displays and shrinks on smaller ones. ReferenceScreenHeight <= 0
            // disables screen-relative scaling.
            var scale = _settings.UiScale;
            var refH = _settings.ReferenceScreenHeight;
            if (refH > 0f)
            {
                scale *= Screen.height / refH;
            }
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (!_isOpen)
            {
                DrawToggleButton(scale);
            }
            else
            {
                DrawPanel(scale);
            }

            GUI.matrix = previousMatrix;
        }

        private void DrawToggleButton(float scale)
        {
            var screenW = Screen.width / scale;
            var screenH = Screen.height / scale;
            var width = _settings.ButtonWidth;
            var height = _settings.ButtonHeight;
            var margin = _settings.ButtonMargin;

            float x, y;
            switch (_settings.ButtonCorner)
            {
                case DebugMenuButtonCorner.TopLeft:
                    x = margin;
                    y = margin;
                    break;
                case DebugMenuButtonCorner.TopRight:
                    x = screenW - width - margin;
                    y = margin;
                    break;
                case DebugMenuButtonCorner.BottomLeft:
                    x = margin;
                    y = screenH - height - margin;
                    break;
                default: // BottomRight
                    x = screenW - width - margin;
                    y = screenH - height - margin;
                    break;
            }

            if (GUI.Button(new Rect(x, y, width, height), _settings.ButtonText, _styles.ToggleButton))
            {
                Open();
            }
        }

        private void DrawPanel(float scale)
        {
            var screenH = Screen.height / scale;
            var margin = _settings.PanelMargin;
            float height;
            if (_settings.PanelFillScreenHeight)
            {
                height = Mathf.Min(screenH - margin * 2f, _settings.PanelMaxHeight);
            }
            else
            {
                height = _settings.PanelMaxHeight;
            }
            var rect = new Rect(margin, margin, _settings.PanelWidth, height);
            GUILayout.BeginArea(rect, _styles.Window);

            var current = _stack.Count > 0 ? _stack.Peek() : _rootPage;

            // Title bar: Back (disabled at root) | title | Close.
            GUILayout.BeginHorizontal();
            GUI.enabled = _stack.Count > 1;
            if (GUILayout.Button("◀ Back", _styles.SmallButton, GUILayout.Width(80f), GUILayout.Height(28f)))
            {
                Pop();
            }
            GUI.enabled = true;
            GUILayout.Label(current.Title, _styles.TitleLabel, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("✕", _styles.SmallButton, GUILayout.Width(40f), GUILayout.Height(28f)))
            {
                Close();
                // Bail out cleanly: the panel is gone, but we still need balanced GUILayout calls.
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            var widgets = GetWidgets(current);
            for (var i = 0; i < widgets.Count; i++)
            {
                widgets[i].Render(_ctx);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // Widget lists are built once per page on first display and cached for the lifetime of the menu.
        private List<DebugWidget> GetWidgets(IDebugPage page)
        {
            if (_widgetsByPage.TryGetValue(page, out var cached))
            {
                return cached;
            }
            var builder = new DebugPageBuilder();
            page.Build(builder);
            _widgetsByPage[page] = builder.Widgets;
            return builder.Widgets;
        }

        private void SaveLastPage()
        {
            if (_stack.Count > 1)
            {
                var top = _stack.Peek();
                PlayerPrefs.SetString(LastPageIdKey, top.Id ?? string.Empty);
            }
            else
            {
                PlayerPrefs.DeleteKey(LastPageIdKey);
            }
        }

        private IDebugPage FindPage(string id)
        {
            for (var i = 0; i < _registeredPages.Count; i++)
            {
                if (string.Equals(_registeredPages[i].Id, id, System.StringComparison.Ordinal))
                {
                    return _registeredPages[i];
                }
            }
            return null;
        }

        private void OnDestroy()
        {
            SaveLastPage();
        }
    }
}
#endif
