namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Logical render layer for windows pushed through <c>IUIService</c>. Each layer is bound
    /// to its own <c>CameraId</c> and Canvas (created lazily by <c>UILayerManager</c>). The
    /// integer value is also the canvas <c>sortingOrder</c>.
    ///
    /// <para>Three layers, by design:</para>
    /// <list type="bullet">
    ///   <item><description><b>UI</b> — All in-game windows: HUDs, menus, popups, modals,
    ///   dialogs. They all share one stack; ordering between simultaneously open windows
    ///   comes from push order (later pushes draw above earlier ones) and child-sibling
    ///   order within a window prefab. Modal vs. non-modal is controlled per-window via
    ///   <see cref="WindowView.IsBlocking"/>, not via a separate layer.</description></item>
    ///   <item><description><b>UIOverlay</b> — UI that must render above the main UI camera
    ///   regardless of stack ordering (e.g. a global toast/notification surface, debug HUD).</description></item>
    ///   <item><description><b>Wipe</b> — Reserved for full-screen scene-transition wipes so
    ///   they always cover everything else.</description></item>
    /// </list>
    ///
    /// If you find yourself wanting "HUD vs. Window vs. Popup" as separate layers, prefer
    /// stack ordering inside <c>UI</c> plus <see cref="WindowView.IsBlocking"/>. Add a new
    /// enum entry only when you need a dedicated camera/canvas pair (different render
    /// settings, post-processing, or guaranteed top/bottom rendering).
    /// </summary>
    public enum UILayer
    {
        UI = 100,
        UIOverlay = 200,
        Wipe = 300,
    }
}
