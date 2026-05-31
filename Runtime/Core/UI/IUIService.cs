using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.UI
{
    public interface IUIService
    {
        UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default) where T : WindowView;
        UniTask<T> Push<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>;
        // Convenience overloads of Push that suppress input for the duration of the push
        // (asset load + show transition), without the caller having to build a WindowOptions
        // just to set LockInputDuringPush. Any options passed are honored; the lock flag is
        // forced on. Requires an IInputLockService to be registered, otherwise the push runs
        // unlocked with a warning (see UIService).
        UniTask<T> PushLocked<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default) where T : WindowView;
        UniTask<T> PushLocked<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>;
        UniTask<WindowPushHandle<T>> PushWithHandle<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView;
        UniTask<WindowPushHandle<T>> PushWithHandle<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>;
        UniTask<WindowPushHandle<T>> PushBelow<T>(WindowView coveringView, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView;
        UniTask<T> PushPrepared<T>(Func<T, CancellationToken, UniTask> prepareAsync, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView;
        // Returns true if a window was popped, false if the target stack/window was not found.
        UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default);
        UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView;
        UniTask<bool> Pop(WindowView view, CancellationToken ct = default);
        // When immediate is true, the hide transition is skipped and the window is torn down
        // synchronously. Use this for "leaving the scene" cases (e.g. scope dispose during
        // scene unload) where playing the transition after a scene wipe is visible.
        UniTask<bool> Pop(WindowView view, bool immediate, CancellationToken ct = default);
        UniTask<T> Replace<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default) where T : WindowView;
        UniTask<T> Replace<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>;
        // Returns the number of windows popped from the layer.
        // When immediate is true, hide transitions are skipped and all windows are torn down
        // synchronously in one frame. Use this for "leaving the scene" cases where the
        // accumulated transition time would otherwise be visible to the user.
        UniTask<int> PopAll(UILayer layer, bool immediate = false, CancellationToken ct = default);
        UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView;
        // Releases the asset reference taken by a prior Preload<T>(tag). Safe to call when the
        // asset is not currently loaded (no-op). Mirrors Preload<T> so consumers do not need to
        // reach into IAssetRequester with a hand-built key to drop a preload that was never
        // pushed.
        void Unload<T>(string tag = null) where T : WindowView;

        // Threading contract for the synchronous query methods below
        // (IsOpen<T>, GetWindow<T>, GetStackCount):
        //
        //   * Main-thread-only. They take no locks and iterate the underlying
        //     stacks directly. Calling them from a background thread is not
        //     supported and may observe torn state.
        //
        //   * They return a snapshot of state taken between awaits. If a
        //     Push/Pop/Replace is in flight on the same logical operation
        //     (e.g. paused awaiting a show/hide transition), the window is
        //     considered "open" once it has been placed on the stack and
        //     "closed" once it has been removed from the stack — even if its
        //     transition has not yet completed. Callers that need
        //     transition-complete semantics must await the originating
        //     Push/Pop/Replace task.
        //
        //   * Mutating methods (Push/Pop/Replace/PopAll) serialize via
        //     per-layer locks; these queries intentionally do not, so they
        //     remain cheap and usable from synchronous contexts (e.g. Update).
        bool IsOpen<T>() where T : WindowView;
        T GetWindow<T>() where T : WindowView;
        int GetStackCount(UILayer layer);
    }
}
