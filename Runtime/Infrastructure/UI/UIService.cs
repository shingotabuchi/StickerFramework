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
        private readonly IObjectResolver _resolver;
        private readonly Dictionary<UILayer, Stack<WindowHandle>> _stacks;
        private readonly IPublisher<WindowClosedEvent> _windowClosedPublisher;
        private readonly IPublisher<WindowOpenedEvent> _windowOpenedPublisher;

        [Inject]
        public UIService(
            IAssetRequester assetRequester,
            IObjectResolver resolver,
            IPublisher<WindowOpenedEvent> windowOpenedPublisher,
            IPublisher<WindowClosedEvent> windowClosedPublisher)
        {
            _assetRequester = assetRequester;
            _resolver = resolver;
            _windowOpenedPublisher = windowOpenedPublisher;
            _windowClosedPublisher = windowClosedPublisher;
            _layerManager = new UILayerManager();
            _stacks = new Dictionary<UILayer, Stack<WindowHandle>>();

            foreach (var layer in AllLayers) _stacks[layer] = new Stack<WindowHandle>();
        }

        public void Dispose()
        {
            foreach (var stack in _stacks.Values)
                while (stack.Count > 0)
                {
                    var handle = stack.Pop();
                    handle.Dispose();
                }

            _stacks.Clear();
            _layerManager.Dispose();
        }

        public void Start()
        {
            _layerManager.Initialize();
        }

        static string BuildKey<T>(string tag) where T : WindowView
        {
            var name = typeof(T).Name;
            if (string.IsNullOrEmpty(tag))
            {
                return $"Views/{name}.prefab";
            }

            return $"Views/{name}_{tag}.prefab";
        }

        public async UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView
        {
            var key = BuildKey<T>(tag);
            var window = await PushInternal(key, options, ct);
            return window as T;
        }

        public async UniTask Pop(UILayer layer = UILayer.Window, CancellationToken ct = default)
        {
            var stack = _stacks[layer];
            if (stack.Count == 0)
            {
                Log.Warning("UIService", $"No windows to pop on layer {layer}");
                return;
            }

            var windowHandle = stack.Pop();
            windowHandle.View.OnBeforeHide();

            var hideTransType = windowHandle.View.HideTransition;
            var transDuration = windowHandle.View.TransitionDuration;
            var hideTransition = TransitionFactory.Create(hideTransType, windowHandle.View);
            await hideTransition.Play(windowHandle.View.CanvasGroup, windowHandle.View.RectTransform, false,
                transDuration, ct);

            windowHandle.View.OnHide();

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
        }

        public async UniTask Pop<T>(CancellationToken ct = default) where T : WindowView
        {
            foreach (var pair in _stacks)
            {
                foreach (var handle in pair.Value)
                {
                    if (handle.View is T)
                    {
                        await Pop(pair.Key, ct);
                        return;
                    }
                }
            }

            Log.Warning("UIService", $"No window of type {typeof(T).Name} found to pop");
        }

        public async UniTask<T> Replace<T>(UILayer layer, string tag = null, WindowOptions options = null,
            CancellationToken ct = default) where T : WindowView
        {
            var stack = _stacks[layer];
            if (stack.Count > 0)
            {
                var current = stack.Pop();
                current.View.OnBeforeHide();

                var hideTransition = TransitionFactory.Create(current.View.HideTransition, current.View);
                await hideTransition.Play(current.View.CanvasGroup, current.View.RectTransform, false,
                    current.View.TransitionDuration, ct);

                current.View.OnHide();

                var closedKey = current.Key;
                current.Dispose();
                _windowClosedPublisher.Publish(new WindowClosedEvent(closedKey, layer));
            }

            return await Push<T>(tag, options, ct);
        }

        public async UniTask PopAll(UILayer layer, CancellationToken ct = default)
        {
            var stack = _stacks[layer];
            while (stack.Count > 0) await Pop(layer, ct);
        }

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

        async UniTask<WindowView> PushInternal(string key, WindowOptions options, CancellationToken ct)
        {
            Log.Info("UIService", $"Pushing window with key '{key}'");
            var assetHandle = await _assetRequester.RequestAsset<GameObject>(key, ct);
            var prefab = assetHandle.Asset;

            var prefabWindow = prefab.GetComponent<WindowView>();
            if (prefabWindow == null)
            {
                assetHandle.Dispose();
                Log.Error("UIService", $"Prefab '{key}' does not have a WindowView component");
                return null;
            }

            var layer = prefabWindow.Layer;
            var isBlocking = options?.IsBlocking ?? prefabWindow.IsBlocking;
            var showTransType = options?.ShowTransition ?? prefabWindow.ShowTransition;
            var transDuration = options?.TransitionDuration ?? prefabWindow.TransitionDuration;

            var stack = _stacks[layer];
            if (stack.Count == 0)
            {
                _layerManager.SetLayerCanvasEnabled(layer, true);
            }

            if (stack.Count > 0 && isBlocking)
            {
                var previousTop = stack.Peek().View;
                previousTop.CanvasGroup.interactable = false;
            }

            var layerTransform = _layerManager.GetLayerTransform(layer);

            GameObject blocker = null;
            if (isBlocking)
            {
                blocker = InputBlocker.Create(layerTransform);
            }

            var instance = Object.Instantiate(prefab, layerTransform);
            var resolver = options?.Resolver ?? _resolver;
            Log.Info("UIService", $"Injecting '{key}' using resolver '{resolver.GetType().Name}'.");
            resolver.InjectGameObject(instance);

            var windowView = instance.GetComponent<WindowView>();
            await windowView.OnInitialize(ct);

            var windowHandle = new WindowHandle(key, windowView, blocker, layer, assetHandle);
            stack.Push(windowHandle);

            windowView.OnBeforeShow();

            var showTransition = TransitionFactory.Create(showTransType, windowView);
            Log.Info("UIService", $"Playing show transition for window '{key}' of type '{showTransType}' with duration {transDuration}s.");
            await showTransition.Play(windowView.CanvasGroup, windowView.RectTransform, true, transDuration, ct);

            windowView.OnShow();
            _windowOpenedPublisher.Publish(new WindowOpenedEvent(key, layer));
            Log.Info("UIService", $"Window with key '{key}' shown.");

            return windowView;
        }
    }
}
