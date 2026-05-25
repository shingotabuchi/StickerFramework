namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Logical render layer for windows pushed through <c>IUIService</c>. Each layer is bound
    /// to its own <c>CameraId</c> and Canvas (created lazily by <c>UILayerManager</c>). The
    /// integer value is also the canvas <c>sortingOrder</c>.
    ///
    /// <para>Two layers, by design:</para>
    /// <list type="bullet">
    ///   <item><description><b>UI</b> — All in-game windows: HUDs, menus, popups, modals,
    ///   dialogs. They all share one stack; ordering between simultaneously open windows
    ///   comes from push order (later pushes draw above earlier ones) and child-sibling
    ///   order within a window prefab. Modal vs. non-modal is controlled per-window via
    ///   <see cref="WindowView.IsBlocking"/>, not via a separate layer.</description></item>
    ///   <item><description><b>UIOverlay</b> — UI that must render above the main UI camera
    ///   regardless of stack ordering (e.g. a global toast/notification surface, debug HUD).</description></item>
    /// </list>
    ///
    /// Full-screen screen transitions are self-contained prefab rigs and do not use an
    /// <c>IUIService</c> layer.
    /// </summary>
    public enum UILayer
    {
        UI = 100,
        UIOverlay = 200,
    }
}
