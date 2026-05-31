#if STICKER_DEBUG
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        private const int RaycastBlockerSortingOrder = short.MaxValue;
        private const float ScrollDragThreshold = 6f;

        private readonly DebugMenuStyles _styles = new DebugMenuStyles();
        private readonly Stack<IDebugPage> _stack = new Stack<IDebugPage>();
        private readonly Dictionary<IDebugPage, List<DebugWidget>> _widgetsByPage = new Dictionary<IDebugPage, List<DebugWidget>>();

        private GameObject _raycastBlockerCanvas;
        private RectTransform _raycastBlockerRect;
        private DebugMenuSettings _settings;
        private List<IDebugPage> _registeredPages;
        private RootDebugPage _rootPage;
        private bool _isOpen;
        private int _keepPanelRaycastBlockerFrame = -1;
        private Vector2 _scrollPosition;
        private bool _scrollDragCandidate;
        private bool _scrollDragActive;
        private int _scrollDragControlId;
        private Vector2 _scrollDragStartMouse;
        private Vector2 _scrollDragStartPosition;
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
            _registeredPages = pages != null ? new List<IDebugPage>(pages) : new List<IDebugPage>();
            _rootPage = new RootDebugPage(_registeredPages);
        }

        public void RegisterPage(IDebugPage page)
        {
            if (page == null || _registeredPages == null || _registeredPages.Contains(page))
            {
                return;
            }

            _registeredPages.Add(page);
            InvalidateRootPageCache();
        }

        public void UnregisterPage(IDebugPage page)
        {
            if (page == null || _registeredPages == null)
            {
                return;
            }

            if (!_registeredPages.Remove(page))
            {
                return;
            }

            _widgetsByPage.Remove(page);
            InvalidateRootPageCache();

            // If the user is currently viewing the unregistered page, pop back to root.
            if (_stack.Count > 0 && _stack.Peek() == page)
            {
                _stack.Pop();
                if (_stack.Count == 0)
                {
                    _stack.Push(_rootPage);
                }
            }
        }

        private void InvalidateRootPageCache()
        {
            if (_rootPage != null)
            {
                _widgetsByPage.Remove(_rootPage);
            }
        }

        private void Start()
        {
            _ctx = new DebugMenuRenderContext(_styles, this);
            EnsureRaycastBlocker();
            UpdateRaycastBlocker();

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

        private void LateUpdate()
        {
            UpdateRaycastBlocker();
        }

        public void Open()
        {
            if (_isOpen)
            {
                return;
            }
            _isOpen = true;
            ResetScroll();
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }
            _isOpen = false;
            _keepPanelRaycastBlockerFrame = Time.frameCount;
            ResetScrollDrag();
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
            ResetScroll();
        }

        public void Pop()
        {
            // Guard against popping the implicit root page.
            if (_stack.Count > 1)
            {
                _stack.Pop();
                ResetScroll();
            }
        }

        public void PopToRoot()
        {
            while (_stack.Count > 1)
            {
                _stack.Pop();
            }
            ResetScroll();
        }

        private void OnGUI()
        {
            _styles.EnsureInitialized(_settings);

            // Effective scale = uiScale × (screenHeight / referenceScreenHeight) so the menu
            // grows on larger displays and shrinks on smaller ones. ReferenceScreenHeight <= 0
            // disables screen-relative scaling.
            var scale = GetCurrentScale();
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
            if (!TryGetToggleButtonRect(scale, out var rect))
            {
                return;
            }

            // No outlined label here: the toggle sits on an opaque button background, so the
            // 8-direction outline (9 GUI.Label draws) is wasted. Render the text in the button itself.
            if (GUI.Button(rect, _settings.ButtonText, _styles.ToggleButton))
            {
                Open();
            }
        }

        private void DrawPanel(float scale)
        {
            if (!TryGetPanelRect(scale, out var rect))
            {
                return;
            }

            GUILayout.BeginArea(rect, _styles.Window);

            var current = _stack.Count > 0 ? _stack.Peek() : _rootPage;

            // Title bar: Back (disabled at root) | title | Close.
            var titleButtonHeight = Mathf.Min(Mathf.Max(28f, _settings.WidgetHeight), rect.height);
            var sideButtonMaxWidth = Mathf.Max(1f, Mathf.Min(Mathf.Max(32f, rect.width * 0.35f), rect.width * 0.5f));
            var backButtonWidth = Mathf.Min(Mathf.Max(80f, _settings.LabelWidth * 0.6f), sideButtonMaxWidth);
            var closeButtonWidth = Mathf.Min(Mathf.Max(40f, titleButtonHeight), sideButtonMaxWidth);
            var sideSlotWidth = Mathf.Max(backButtonWidth, closeButtonWidth);

            var titleRowRect = GUILayoutUtility.GetRect(
                0f,
                titleButtonHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(titleButtonHeight));
            var titleRect = new Rect(
                titleRowRect.xMin + sideSlotWidth,
                titleRowRect.yMin,
                Mathf.Max(0f, titleRowRect.width - sideSlotWidth * 2f),
                titleButtonHeight);
            var backRect = new Rect(titleRowRect.xMin, titleRowRect.yMin, backButtonWidth, titleButtonHeight);
            var closeRect = new Rect(titleRowRect.xMax - closeButtonWidth, titleRowRect.yMin, closeButtonWidth, titleButtonHeight);

            GUI.enabled = _stack.Count > 1;
            if (GUI.Button(backRect, GUIContent.none, _styles.SmallButton))
            {
                Pop();
            }
            _styles.DrawOutlinedLabel(backRect, "◀ Back", _styles.SmallButton);
            GUI.enabled = true;
            _styles.DrawOutlinedLabel(titleRect, current.Title, _styles.TitleLabel);

            if (GUI.Button(closeRect, GUIContent.none, _styles.SmallButton))
            {
                Close();
                GUILayout.EndArea();
                return;
            }
            _styles.DrawOutlinedLabel(closeRect, "✕", _styles.SmallButton);

            GUILayout.Space(6f);

            var scrollHeight = Mathf.Max(0f, rect.height - _styles.Window.padding.vertical - titleButtonHeight - 6f);
            var scrollRect = new Rect(
                titleRowRect.xMin,
                titleRowRect.yMax + 6f,
                titleRowRect.width,
                scrollHeight);
            var contentDragRect = scrollRect;
            contentDragRect.width = Mathf.Max(0f, contentDragRect.width - _styles.ScrollBarWidth);
            HandleScrollDrag(contentDragRect);

            var previousSkin = GUI.skin;
            if (_styles.ScrollViewSkin != null)
            {
                GUI.skin = _styles.ScrollViewSkin;
            }

            try
            {
                _scrollPosition = GUILayout.BeginScrollView(
                    _scrollPosition,
                    false,
                    true,
                    _styles.HorizontalScrollbar,
                    _styles.VerticalScrollbar,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(scrollHeight));
                var widgets = GetWidgets(current);
                for (var i = 0; i < widgets.Count; i++)
                {
                    widgets[i].Render(_ctx);
                }
                GUILayout.EndScrollView();
            }
            finally
            {
                GUI.skin = previousSkin;
            }

            GUILayout.EndArea();
        }

        private void HandleScrollDrag(Rect contentDragRect)
        {
            var evt = Event.current;
            if (evt == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(FocusType.Passive, contentDragRect);
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button != 0)
                    {
                        break;
                    }

                    if (!contentDragRect.Contains(evt.mousePosition))
                    {
                        ResetScrollDrag();
                        break;
                    }

                    _scrollDragCandidate = true;
                    _scrollDragActive = false;
                    _scrollDragControlId = controlId;
                    _scrollDragStartMouse = evt.mousePosition;
                    _scrollDragStartPosition = _scrollPosition;
                    break;

                case EventType.MouseDrag:
                    if (!_scrollDragCandidate)
                    {
                        break;
                    }

                    var delta = evt.mousePosition - _scrollDragStartMouse;
                    if (!_scrollDragActive)
                    {
                        if (delta.magnitude < ScrollDragThreshold)
                        {
                            break;
                        }

                        if (Mathf.Abs(delta.y) < Mathf.Abs(delta.x))
                        {
                            ResetScrollDrag();
                            break;
                        }

                        _scrollDragActive = true;
                        GUIUtility.hotControl = _scrollDragControlId;
                    }

                    if (GUIUtility.hotControl == _scrollDragControlId)
                    {
                        _scrollPosition.y = Mathf.Max(0f, _scrollDragStartPosition.y - delta.y);
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_scrollDragCandidate || _scrollDragActive)
                    {
                        if (GUIUtility.hotControl == _scrollDragControlId)
                        {
                            GUIUtility.hotControl = 0;
                            evt.Use();
                        }
                        ResetScrollDrag();
                    }
                    break;
            }
        }

        private void ResetScroll()
        {
            _scrollPosition = Vector2.zero;
            ResetScrollDrag();
        }

        private void ResetScrollDrag()
        {
            if (_scrollDragActive && GUIUtility.hotControl == _scrollDragControlId)
            {
                GUIUtility.hotControl = 0;
            }

            _scrollDragCandidate = false;
            _scrollDragActive = false;
            _scrollDragControlId = 0;
            _scrollDragStartMouse = Vector2.zero;
            _scrollDragStartPosition = Vector2.zero;
        }

        private static float GetResponsiveMargin(float requestedMargin, Rect safeArea)
        {
            var maxMargin = Mathf.Max(0f, (Mathf.Min(safeArea.width, safeArea.height) - 1f) * 0.5f);
            return Mathf.Clamp(requestedMargin, 0f, maxMargin);
        }

        private bool TryGetToggleButtonRect(float scale, out Rect rect)
        {
            var safeArea = GetScaledSafeArea(scale);
            var margin = GetResponsiveMargin(_settings.ButtonMargin, safeArea);
            var width = Mathf.Min(_settings.ButtonWidth, Mathf.Max(0f, safeArea.width - margin * 2f));
            var height = Mathf.Min(_settings.ButtonHeight, Mathf.Max(0f, safeArea.height - margin * 2f));
            if (width <= 0f || height <= 0f)
            {
                rect = default;
                return false;
            }

            rect = GetAnchoredRect(safeArea, margin, width, height, _settings.ButtonCorner);
            return true;
        }

        private bool TryGetPanelRect(float scale, out Rect rect)
        {
            var safeArea = GetScaledSafeArea(scale);
            var margin = GetResponsiveMargin(_settings.PanelMargin, safeArea);
            var availableWidth = Mathf.Max(0f, safeArea.width - margin * 2f);
            var availableHeight = Mathf.Max(0f, safeArea.height - margin * 2f);
            var width = Mathf.Min(_settings.PanelWidth, availableWidth);
            if (_settings.PanelFillScreenHeight)
            {
                availableHeight = Mathf.Min(availableHeight, _settings.PanelMaxHeight);
            }
            else
            {
                availableHeight = Mathf.Min(_settings.PanelMaxHeight, availableHeight);
            }

            var height = availableHeight;
            if (width <= 0f || height <= 0f)
            {
                rect = default;
                return false;
            }

            rect = GetAnchoredRect(safeArea, margin, width, height, _settings.PanelCorner);
            return true;
        }

        private static Rect GetAnchoredRect(Rect safeArea, float margin, float width, float height, DebugMenuButtonCorner corner)
        {
            float x, y;
            switch (corner)
            {
                case DebugMenuButtonCorner.TopLeft:
                    x = safeArea.xMin + margin;
                    y = safeArea.yMin + margin;
                    break;
                case DebugMenuButtonCorner.TopRight:
                    x = safeArea.xMax - width - margin;
                    y = safeArea.yMin + margin;
                    break;
                case DebugMenuButtonCorner.BottomLeft:
                    x = safeArea.xMin + margin;
                    y = safeArea.yMax - height - margin;
                    break;
                default: // BottomRight
                    x = safeArea.xMax - width - margin;
                    y = safeArea.yMax - height - margin;
                    break;
            }

            x = Mathf.Clamp(x, safeArea.xMin, Mathf.Max(safeArea.xMin, safeArea.xMax - width));
            y = Mathf.Clamp(y, safeArea.yMin, Mathf.Max(safeArea.yMin, safeArea.yMax - height));
            return new Rect(x, y, width, height);
        }

        private static Rect GetScaledSafeArea(float scale)
        {
            var safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
            }

            var safeX = safeArea.x / scale;
            var safeY = (Screen.height - safeArea.yMax) / scale;
            var safeW = safeArea.width / scale;
            var safeH = safeArea.height / scale;
            return new Rect(safeX, safeY, safeW, safeH);
        }

        private float GetCurrentScale()
        {
            var scale = _settings.UiScale;
            var refH = _settings.ReferenceScreenHeight;
            if (refH > 0f)
            {
                scale *= Screen.height / refH;
            }
            return Mathf.Max(scale, 0.0001f);
        }

        private void EnsureRaycastBlocker()
        {
            if (_raycastBlockerCanvas != null && _raycastBlockerRect != null)
            {
                return;
            }

            _raycastBlockerCanvas = new GameObject("DebugMenuRaycastBlocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _raycastBlockerCanvas.transform.SetParent(transform, false);

            var canvas = _raycastBlockerCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = RaycastBlockerSortingOrder;

            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(_raycastBlockerCanvas.transform, false);
            _raycastBlockerRect = blocker.GetComponent<RectTransform>();
            _raycastBlockerRect.anchorMin = new Vector2(0f, 1f);
            _raycastBlockerRect.anchorMax = new Vector2(0f, 1f);
            _raycastBlockerRect.pivot = new Vector2(0f, 1f);

            var image = blocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            _raycastBlockerCanvas.SetActive(false);
        }

        private void UpdateRaycastBlocker()
        {
            EnsureRaycastBlocker();

            var scale = GetCurrentScale();
            var shouldUsePanelRect = _isOpen || _keepPanelRaycastBlockerFrame == Time.frameCount;
            Rect guiRect;
            var hasRect = shouldUsePanelRect
                ? TryGetPanelRect(scale, out guiRect)
                : TryGetToggleButtonRect(scale, out guiRect);

            if (!hasRect)
            {
                _raycastBlockerCanvas.SetActive(false);
                return;
            }

            _raycastBlockerCanvas.SetActive(true);
            _raycastBlockerRect.anchoredPosition = new Vector2(guiRect.x * scale, -guiRect.y * scale);
            _raycastBlockerRect.sizeDelta = new Vector2(guiRect.width * scale, guiRect.height * scale);
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
