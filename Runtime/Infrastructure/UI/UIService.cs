using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace StickerFwk.Infrastructure.UI
{
    public class UIService : IUIService, IStartable, IDisposable
    {
        private static readonly UILayer[] AllLayers = (UILayer[])Enum.GetValues(typeof(UILayer));

        private readonly IAssetRequester _assetRequester;
        private readonly UILayerManager _layerManager;
        private readonly WindowAssetResolver _windowAssetResolver;
        private readonly WindowLifecycleRunner _windowLifecycleRunner = new();
        private readonly IObjectResolver _resolver;
        private readonly Dictionary<UILayer, Stack<WindowHandle>> _stacks;
        private readonly IPublisher<WindowClosedEvent> _windowClosedPublisher;
        private readonly IPublisher<WindowOpenedEvent> _windowOpenedPublisher;

        // Concurrency model:
        //   - Each UILayer has a SemaphoreSlim that serializes Push/Pop/Replace on that layer.
        //     Different layers can run concurrently.
        //   - A global semaphore serializes cross-layer scans (Pop<T>(), Pop(WindowView)).
        //     Lock ordering is global -> layer (never the reverse) so nested acquisition is
        //     deadlock-free.
        //   - Asset loads happen outside any lock so they can run in parallel.
        //   - _disposeCts is cancelled on Dispose so queued waiters and in-flight awaits
        //     observe cancellation and unwind cleanly.
        private readonly Dictionary<UILayer, SemaphoreSlim> _layerLocks;
        private readonly SemaphoreSlim _globalLock = new(1, 1);
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly string _keyPrefix;
        private bool _disposed;

        [Inject]
        public UIService(
            IAssetRequester assetRequester,
            IObjectResolver resolver,
            ICameraService cameraService,
            ISubscriber<CameraRegisteredEvent> cameraRegisteredSubscriber,
            IPublisher<WindowOpenedEvent> windowOpenedPublisher,
            IPublisher<WindowClosedEvent> windowClosedPublisher)
            : this(
                assetRequester,
                resolver,
                cameraService,
                cameraRegisteredSubscriber,
                windowOpenedPublisher,
                windowClosedPublisher,
                resolver?.ResolveOrDefault<WindowAssetKeyOptions>())
        {
        }

        public UIService(
            IAssetRequester assetRequester,
            IObjectResolver resolver,
            ICameraService cameraService,
            ISubscriber<CameraRegisteredEvent> cameraRegisteredSubscriber,
            IPublisher<WindowOpenedEvent> windowOpenedPublisher,
            IPublisher<WindowClosedEvent> windowClosedPublisher,
            WindowAssetKeyOptions keyOptions)
        {
            _assetRequester = assetRequester;
            _resolver = resolver;
            _windowOpenedPublisher = windowOpenedPublisher;
            _windowClosedPublisher = windowClosedPublisher;
            _windowAssetResolver = new WindowAssetResolver(assetRequester);
            _layerManager = new UILayerManager(cameraService, cameraRegisteredSubscriber);
            _stacks = new Dictionary<UILayer, Stack<WindowHandle>>();
            _layerLocks = new Dictionary<UILayer, SemaphoreSlim>();
            _keyPrefix = keyOptions?.Prefix ?? string.Empty;

            foreach (var layer in AllLayers)
            {
                _stacks[layer] = new Stack<WindowHandle>();
                _layerLocks[layer] = new SemaphoreSlim(1, 1);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Cancel any queued or in-flight ops; their linked tokens fire and they unwind.
            try { _disposeCts.Cancel(); } catch { /* ignored */ }

            foreach (var stack in _stacks.Values)
                while (stack.Count > 0)
                {
                    var handle = stack.Pop();
                    handle.Dispose();
                }

            _stacks.Clear();
            _layerManager.Dispose();
            _disposeCts.Dispose();
        }

        public void Start()
        {
            _layerManager.Initialize();
        }

        string BuildKey<T>(string tag) where T : WindowView
        {
            var name = typeof(T).Name;
            if (string.IsNullOrEmpty(tag))
            {
                return $"{_keyPrefix}Views/{name}.prefab";
            }

            return $"{_keyPrefix}Views/{name}_{tag}.prefab";
        }

        public UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView
        {
            return PushInternal<T>(tag, options, ct, configure: null);
        }

        public UniTask<T> Push<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>
        {
            return PushInternal<T>(tag, options, ct, view => view.SetArgs(args));
        }

        public async UniTask<WindowPushHandle<T>> PushWithHandle<T>(string tag = null, WindowOptions options = null,
            CancellationToken ct = default) where T : WindowView
        {
            var view = await Push<T>(tag, options, ct);
            return new WindowPushHandle<T>(view, this);
        }

        public async UniTask<WindowPushHandle<T>> PushWithHandle<T, TArgs>(TArgs args, string tag = null,
            WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>
        {
            var view = await Push<T, TArgs>(args, tag, options, ct);
            return new WindowPushHandle<T>(view, this);
        }

        public async UniTask<WindowPushHandle<T>> PushBelow<T>(WindowView coveringView, string tag = null,
            WindowOptions options = null, CancellationToken ct = default) where T : WindowView
        {
            if (coveringView == null)
            {
                throw new ArgumentNullException(nameof(coveringView));
            }

            var view = await PushInternal<T>(tag, options, ct, configure: null, prepareBeforeShow: null,
                afterInitializeBeforeShow: pushedView => PositionBelow(pushedView, coveringView));
            return new WindowPushHandle<T>(view, this);
        }

        public UniTask<T> PushPrepared<T>(Func<T, CancellationToken, UniTask> prepareAsync, string tag = null,
            WindowOptions options = null, CancellationToken ct = default) where T : WindowView
        {
            if (prepareAsync == null)
            {
                throw new ArgumentNullException(nameof(prepareAsync));
            }

            return PushInternal<T>(tag, options, ct, configure: null, prepareBeforeShow: prepareAsync);
        }

        async UniTask<T> PushInternal<T>(string tag, WindowOptions options, CancellationToken ct, Action<T> configure,
            Func<T, CancellationToken, UniTask> prepareBeforeShow = null, Action<T> afterInitializeBeforeShow = null)
            where T : WindowView
        {
            ThrowIfDisposed();
            var key = BuildKey<T>(tag);

            using var linkedCts = LinkToken(ct);
            var linkedCt = linkedCts.Token;

            // Load asset OUTSIDE any lock so concurrent pushes can load in parallel.
            var windowAsset = await _windowAssetResolver.Load<T>(key, linkedCt);
            UILayer layer;
            SemaphoreSlim layerLock;
            try
            {
                layer = windowAsset.PrefabWindow.Layer;
                layerLock = _layerLocks[layer];
                await layerLock.WaitAsync(linkedCt);
            }
            catch
            {
                windowAsset.Dispose();
                throw;
            }

            try
            {
                // PushLocked takes ownership of windowAsset and disposes it on any failure.
                return await PushLocked<T>(key, windowAsset, options, layer, linkedCt, configure,
                    prepareBeforeShow, afterInitializeBeforeShow);
            }
            finally
            {
                layerLock.Release();
            }
        }

        public async UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            using var linkedCts = LinkToken(ct);
            var linkedCt = linkedCts.Token;

            var layerLock = _layerLocks[layer];
            await layerLock.WaitAsync(linkedCt);
            try
            {
                return await PopLocked(layer, linkedCt);
            }
            finally
            {
                layerLock.Release();
            }
        }

        public async UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView
        {
            ThrowIfDisposed();
            using var linkedCts = LinkToken(ct);
            var linkedCt = linkedCts.Token;

            // Cross-layer scan: take global lock first, then nest layer lock (global -> layer
            // ordering keeps things deadlock-free since other ops never go layer -> global).
            await _globalLock.WaitAsync(linkedCt);
            try
            {
                if (!TryFindLayerOf<T>(out var layer, out var view))
                {
                    Log.Warning("UIService", $"No window of type {typeof(T).Name} found to pop");
                    return false;
                }

                var layerLock = _layerLocks[layer];
                await layerLock.WaitAsync(linkedCt);
                try
                {
                    return await PopViewLocked(view, layer, immediate: false, linkedCt);
                }
                finally
                {
                    layerLock.Release();
                }
            }
            finally
            {
                _globalLock.Release();
            }
        }

        public UniTask<bool> Pop(WindowView view, CancellationToken ct = default)
        {
            return Pop(view, immediate: false, ct);
        }

        public async UniTask<bool> Pop(WindowView view, bool immediate, CancellationToken ct = default)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            ThrowIfDisposed();
            using var linkedCts = LinkToken(ct);
            var linkedCt = linkedCts.Token;

            await _globalLock.WaitAsync(linkedCt);
            try
            {
                if (!TryFindLayerOf(view, out var layer))
                {
                    Log.Warning("UIService", "No matching window instance found to pop");
                    return false;
                }

                var layerLock = _layerLocks[layer];
                await layerLock.WaitAsync(linkedCt);
                try
                {
                    return await PopViewLocked(view, layer, immediate, linkedCt);
                }
                finally
                {
                    layerLock.Release();
                }
            }
            finally
            {
                _globalLock.Release();
            }
        }

        public UniTask<T> Replace<T>(string tag = null, WindowOptions options = null,
            CancellationToken ct = default) where T : WindowView
        {
            return ReplaceInternal<T>(tag, options, ct, configure: null);
        }

        public UniTask<T> Replace<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>
        {
            return ReplaceInternal<T>(tag, options, ct, view => view.SetArgs(args));
        }

        async UniTask<T> ReplaceInternal<T>(string tag, WindowOptions options, CancellationToken ct, Action<T> configure)
            where T : WindowView
        {
            ThrowIfDisposed();
            var key = BuildKey<T>(tag);

            using var linkedCts = LinkToken(ct);
            var linkedCt = linkedCts.Token;

            var windowAsset = await _windowAssetResolver.Load<T>(key, linkedCt);
            UILayer layer;
            SemaphoreSlim layerLock;
            try
            {
                // Layer is sourced from the prefab so pop and push are guaranteed symmetric.
                layer = windowAsset.PrefabWindow.Layer;
                layerLock = _layerLocks[layer];
                await layerLock.WaitAsync(linkedCt);
            }
            catch
            {
                windowAsset.Dispose();
                throw;
            }

            try
            {
                var stack = _stacks[layer];
                if (stack.Count > 0)
                {
                    try
                    {
                        await PopLocked(layer, linkedCt);
                    }
                    catch
                    {
                        // PushLocked never took ownership; dispose the asset ourselves.
                        windowAsset.Dispose();
                        throw;
                    }
                }

                // PushLocked takes ownership of windowAsset and disposes it on any failure.
                return await PushLocked<T>(key, windowAsset, options, layer, linkedCt, configure);
            }
            finally
            {
                layerLock.Release();
            }
        }

        public async UniTask<int> PopAll(UILayer layer, bool immediate = false, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            using var linkedCts = LinkToken(ct);
            var linkedCt = linkedCts.Token;

            // Acquire the layer lock once and drain via the lock-free core to avoid
            // re-entrant waits (SemaphoreSlim is not reentrant).
            var layerLock = _layerLocks[layer];
            await layerLock.WaitAsync(linkedCt);
            try
            {
                var stack = _stacks[layer];
                var popped = 0;
                while (stack.Count > 0)
                {
                    if (immediate)
                    {
                        if (PopImmediateLocked(layer))
                        {
                            popped++;
                        }
                    }
                    else if (await PopLocked(layer, linkedCt))
                    {
                        popped++;
                    }
                }

                return popped;
            }
            finally
            {
                layerLock.Release();
            }
        }

        // Synchronous queries: main-thread-only, lock-free by design. See the
        // threading contract on IUIService for the full rules. Summary:
        //   * No locks are taken; these iterate _stacks directly.
        //   * They reflect committed stack state — a window is visible to
        //     IsOpen/GetWindow/GetStackCount the moment it is pushed onto the
        //     stack (before its show transition completes) and disappears
        //     the moment it is popped (before its hide transition completes).
        //   * Calling these from a background thread is not supported.
        public bool IsOpen<T>() where T : WindowView
        {
            foreach (var stack in _stacks.Values)
                foreach (var handle in stack)
                    if (handle.View is T)
                        return true;

            return false;
        }

        public T GetWindow<T>() where T : WindowView
        {
            foreach (var stack in _stacks.Values)
                foreach (var handle in stack)
                    if (handle.View is T view)
                        return view;

            return null;
        }

        public int GetStackCount(UILayer layer)
        {
            return _stacks[layer].Count;
        }

        public async UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView
        {
            var key = BuildKey<T>(tag);
            if (_assetRequester.IsLoaded(key))
            {
                return;
            }

            Log.Info("UIService", $"Preloading window asset '{key}'");
            await _assetRequester.Preload<GameObject>(new[] { key }, ct);
        }

        public void Unload<T>(string tag = null) where T : WindowView
        {
            ThrowIfDisposed();
            var key = BuildKey<T>(tag);
            if (!_assetRequester.IsLoaded(key))
            {
                return;
            }

            Log.Info("UIService", $"Unloading window asset '{key}'");
            _assetRequester.Release(key);
        }

        // ---------------------------------------------------------------------
        // Lock-free cores. Callers MUST hold the corresponding layer lock.
        // ---------------------------------------------------------------------

        async UniTask<T> PushLocked<T>(string key, WindowAsset<T> windowAsset, WindowOptions options, UILayer layer,
            CancellationToken ct, Action<T> configure = null,
            Func<T, CancellationToken, UniTask> prepareBeforeShow = null,
            Action<T> afterInitializeBeforeShow = null) where T : WindowView
        {
            Log.Info("UIService", $"Pushing window with key '{key}'");
            var prefabWindow = windowAsset.PrefabWindow;

            // IsBlocking and TransitionDuration are plain serialized value-type fields, so the
            // prefab and instance always agree on them — reading from the prefab here lets us
            // decide whether to spawn the input blocker before the instance exists. The
            // [SerializeReference] transition graphs (ShowTransition / HideTransition) are read
            // from the instance below, after instantiation, because object references inside
            // those graphs are only remapped on the cloned instance.
            var isBlocking = options?.IsBlocking ?? prefabWindow.IsBlocking;
            var transDuration = options?.TransitionDuration ?? prefabWindow.TransitionDuration;

            var stack = _stacks[layer];
            var enabledLayer = false;
            var disabledPreviousTop = false;
            GameObject blocker = null;
            GameObject instance = null;
            WindowHandle windowHandle = null;

            try
            {
                // Layer canvases are created on demand and bound to the target camera. If the camera
                // profile is not yet applied, fail fast: the caller should ensure the profile is ready
                // (e.g. via RootLifetimeScope.Apply) before any UI push.
                if (!_layerManager.TryEnsureLayer(layer, out var ensureError))
                {
                    throw new InvalidOperationException($"Cannot push '{key}': {ensureError}");
                }

                if (stack.Count == 0)
                {
                    _layerManager.SetLayerCanvasEnabled(layer, true);
                    enabledLayer = true;
                }

                if (stack.Count > 0 && isBlocking)
                {
                    var previousTop = stack.Peek().View;
                    previousTop.CanvasGroup.interactable = false;
                    disabledPreviousTop = true;
                }

                var layerTransform = _layerManager.GetLayerTransform(layer);

                if (isBlocking)
                {
                    blocker = InputBlocker.Create(layerTransform);
                }

                instance = Object.Instantiate(windowAsset.Prefab, layerTransform);

                // Hide the freshly-instantiated window until the show transition begins.
                // OnInitialize may await async work (e.g. Addressables loads); without this,
                // the unconfigured prefab would render at full opacity for the duration of
                // OnInitialize and then "blink" when the show transition resets alpha to 0.
                // Every shipped transition (Fade/Slide/Scale/None) re-establishes alpha at
                // the start of Play, so this initial hide is safely overwritten when Show
                // runs. Authors of custom transitions must do the same.
                //
                // Also disable raycasts so the (invisible) prefab can't catch clicks during
                // the OnInitialize await — the InputBlocker shields lower views, but without
                // this the new window's own controls would still hit-test.
                var preShowCanvasGroup = instance.GetComponent<CanvasGroup>();
                var originalAlpha = 1f;
                var originalBlocksRaycasts = false;
                var originalInteractable = false;
                if (preShowCanvasGroup != null)
                {
                    // Snapshot the prefab-authored raycast/interactable values so we can restore
                    // them after OnInitialize. Forcing them back unconditionally would overwrite
                    // deliberately-authored CanvasGroup values on the prefab.
                    originalAlpha = preShowCanvasGroup.alpha;
                    originalBlocksRaycasts = preShowCanvasGroup.blocksRaycasts;
                    originalInteractable = preShowCanvasGroup.interactable;
                    preShowCanvasGroup.alpha = 0f;
                    preShowCanvasGroup.blocksRaycasts = false;
                    preShowCanvasGroup.interactable = false;
                }

                var inject = options?.Inject;
                if (inject != null)
                {
                    Log.Info("UIService", $"Injecting '{key}' using caller-supplied Inject delegate.");
                    inject(instance);
                }
                else
                {
                    Log.Info("UIService", $"Injecting '{key}' using resolver '{_resolver.GetType().Name}'.");
                    _resolver.InjectGameObject(instance);
                }

                var windowView = instance.GetComponent<T>();
                if (windowView == null)
                {
                    throw new InvalidOperationException(
                        $"Instantiated window '{key}' is missing expected component {typeof(T).Name}.");
                }

                configure?.Invoke(windowView);
                await windowView.OnInitialize(ct);
                afterInitializeBeforeShow?.Invoke(windowView);
                if (prepareBeforeShow != null)
                {
                    await prepareBeforeShow(windowView, ct);
                }

                // Read transitions from the INSTANCE, not the prefab — SerializeReference
                // strategy data lives inline and any UnityEngine.Object refs inside it are
                // remapped only on the cloned instance.
                var showTrans = options?.ShowTransition ?? windowView.ShowTransition;
                var hideTrans = options?.HideTransition ?? windowView.HideTransition;

                windowHandle = new WindowHandle(key, windowView, blocker, layer, windowAsset.AssetHandle, hideTrans, transDuration);
                stack.Push(windowHandle);

                // Restore raycast/interactable to the prefab-authored values now that init is
                // done. Restore alpha too so transitions that animate something other than the
                // root CanvasGroup (e.g. Timeline-driven sprite wipes) are visible by default.
                // Alpha-owning transitions such as Fade/Scale immediately set their own start
                // value at the beginning of Play.
                if (preShowCanvasGroup != null)
                {
                    preShowCanvasGroup.alpha = originalAlpha;
                    preShowCanvasGroup.blocksRaycasts = originalBlocksRaycasts;
                    preShowCanvasGroup.interactable = originalInteractable;
                }

                Log.Info("UIService",
                    $"Playing show transition for window '{key}' of type '{showTrans?.GetType().Name ?? "null"}' with duration {transDuration}s.");
                await _windowLifecycleRunner.Show(windowView, showTrans, transDuration, ct);

                _windowOpenedPublisher.Publish(new WindowOpenedEvent(key, layer));
                Log.Info("UIService", $"Window with key '{key}' shown.");

                return windowView;
            }
            catch
            {
                if (windowHandle != null)
                {
                    // If Dispose ran while we were awaiting (e.g. show transition observed
                    // the cancellation), it has already drained the stack and disposed this
                    // handle along with the layer manager. Touching either again would
                    // double-dispose the asset handle and KeyNotFoundException on
                    // _layerCanvases. Just unwind.
                    if (!_disposed)
                    {
                        if (stack.Count > 0 && ReferenceEquals(stack.Peek(), windowHandle))
                        {
                            stack.Pop();
                        }

                        windowHandle.Dispose();
                    }
                }
                else
                {
                    if (blocker != null)
                    {
                        Object.Destroy(blocker);
                    }

                    if (instance != null)
                    {
                        Object.Destroy(instance);
                    }

                    // No WindowHandle yet -> the asset's lifetime hasn't been transferred.
                    windowAsset.Dispose();
                }

                if (!_disposed)
                {
                    if (disabledPreviousTop && stack.Count > 0)
                    {
                        stack.Peek().View.CanvasGroup.interactable = true;
                    }

                    if (enabledLayer && stack.Count == 0)
                    {
                        _layerManager.SetLayerCanvasEnabled(layer, false);
                    }
                }

                throw;
            }
        }

        async UniTask<bool> PopLocked(UILayer layer, CancellationToken ct)
        {
            var stack = _stacks[layer];
            if (stack.Count == 0)
            {
                Log.Warning("UIService", $"No windows to pop on layer {layer}");
                return false;
            }

            var windowHandle = stack.Peek();
            await _windowLifecycleRunner.Hide(windowHandle.View, windowHandle.HideTransition, windowHandle.TransitionDuration, ct);
            if (_disposed)
            {
                return false;
            }

            if (stack.Count == 0 || !ReferenceEquals(stack.Peek(), windowHandle))
            {
                Log.Warning("UIService", $"Window '{windowHandle.Key}' was removed while its hide transition was running.");
                return false;
            }

            stack.Pop();
            var key = windowHandle.Key;
            var windowLayer = windowHandle.Layer;
            windowHandle.Dispose();

            if (stack.Count > 0)
            {
                stack.Peek().View.CanvasGroup.interactable = true;
            }
            else
            {
                _layerManager.SetLayerCanvasEnabled(windowLayer, false);
            }

            _windowClosedPublisher.Publish(new WindowClosedEvent(key, windowLayer));
            return true;
        }

        // Synchronous teardown that skips the hide transition. Used by PopAll(immediate: true)
        // to wipe a layer in one frame (e.g. on scene change) instead of paying the
        // accumulated transition cost for every entry in the stack.
        bool PopImmediateLocked(UILayer layer)
        {
            var stack = _stacks[layer];
            if (stack.Count == 0)
            {
                return false;
            }

            var windowHandle = stack.Pop();
            _windowLifecycleRunner.HideWithoutTransition(windowHandle.View);

            var key = windowHandle.Key;
            var windowLayer = windowHandle.Layer;
            windowHandle.Dispose();

            if (stack.Count > 0)
            {
                stack.Peek().View.CanvasGroup.interactable = true;
            }
            else
            {
                _layerManager.SetLayerCanvasEnabled(windowLayer, false);
            }

            _windowClosedPublisher.Publish(new WindowClosedEvent(key, windowLayer));
            return true;
        }

        async UniTask<bool> PopViewLocked(WindowView view, UILayer layer, bool immediate, CancellationToken ct)
        {
            var stack = _stacks[layer];

            // Re-locate under the layer lock: between releasing the global lock and acquiring
            // the layer lock another op may have removed/popped this window.
            WindowHandle target = null;
            foreach (var handle in stack)
            {
                if (ReferenceEquals(handle.View, view))
                {
                    target = handle;
                    break;
                }
            }

            if (target == null)
            {
                Log.Warning("UIService", "Window no longer in stack at pop time");
                return false;
            }

            if (ReferenceEquals(stack.Peek(), target))
            {
                return immediate ? PopImmediateLocked(layer) : await PopLocked(layer, ct);
            }


            // Buried in the stack: remove without playing a hide transition (it isn't
            // visible) but still fire lifecycle hooks and publish the closed event so
            // bookkeeping stays consistent. The buried path is naturally "immediate"
            // regardless of the caller's flag — no transition is ever played here.
            var temp = new List<WindowHandle>(stack.Count);
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                if (ReferenceEquals(top, target))
                {
                    break;
                }

                temp.Add(top);
            }

            _windowLifecycleRunner.HideWithoutTransition(target.View);
            var closedKey = target.Key;
            target.Dispose();

            for (var i = temp.Count - 1; i >= 0; i--)
            {
                stack.Push(temp[i]);
            }

            _windowClosedPublisher.Publish(new WindowClosedEvent(closedKey, layer));
            return true;
        }

        static void PositionBelow(WindowView view, WindowView coveringView)
        {
            if (view == null || coveringView == null)
            {
                Debug.LogWarning("[UIService] PushBelow skipped sibling reorder because one of the views was destroyed.");
                return;
            }

            var viewParent = view.transform.parent;
            var coveringParent = coveringView.transform.parent;
            if (viewParent != null && ReferenceEquals(viewParent, coveringParent))
            {
                view.transform.SetSiblingIndex(coveringView.transform.GetSiblingIndex());
                return;
            }

            Debug.LogWarning("[UIService] PushBelow skipped sibling reorder because the pushed and covering windows do not share a parent.");
        }

        bool TryFindLayerOf<T>(out UILayer layer, out WindowView view) where T : WindowView
        {
            foreach (var pair in _stacks)
            {
                foreach (var handle in pair.Value)
                {
                    if (handle.View is T match)
                    {
                        layer = pair.Key;
                        view = match;
                        return true;
                    }
                }
            }

            layer = default;
            view = null;
            return false;
        }

        bool TryFindLayerOf(WindowView view, out UILayer layer)
        {
            foreach (var pair in _stacks)
            {
                foreach (var handle in pair.Value)
                {
                    if (ReferenceEquals(handle.View, view))
                    {
                        layer = pair.Key;
                        return true;
                    }
                }
            }

            layer = default;
            return false;
        }

        CancellationTokenSource LinkToken(CancellationToken ct)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UIService));
            }
        }
    }
}
