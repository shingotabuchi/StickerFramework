using System;
using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Infrastructure.UI
{
    public class UILayerManager : IDisposable
    {
        const string UiLayerName = "UI";

        readonly ICameraService _cameraService;
        readonly ISubscriber<CameraRegisteredEvent> _cameraRegisteredSubscriber;
        readonly UICanvasOptions _canvasOptions;
        readonly Dictionary<UILayer, Canvas> _layerCanvases = new Dictionary<UILayer, Canvas>();
        IDisposable _cameraSubscription;
        GameObject _root;
        bool _disposed;

        public UILayerManager(
            ICameraService cameraService,
            ISubscriber<CameraRegisteredEvent> cameraRegisteredSubscriber,
            UICanvasOptions canvasOptions = null)
        {
            _cameraService = cameraService;
            _cameraRegisteredSubscriber = cameraRegisteredSubscriber;
            _canvasOptions = canvasOptions ?? new UICanvasOptions();
        }

        public static CameraId LayerToCameraId(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.UI: return CameraId.UI;
                case UILayer.UIOverlay: return CameraId.UIOverlay;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "No camera mapping for layer");
            }
        }

        static bool TryCameraIdToLayer(CameraId cameraId, out UILayer layer)
        {
            if (cameraId == CameraId.UI) { layer = UILayer.UI; return true; }
            if (cameraId == CameraId.UIOverlay) { layer = UILayer.UIOverlay; return true; }
            layer = default;
            return false;
        }

        public void Initialize()
        {
            // Guards against DI ordering quirks where Start runs after Dispose: without this,
            // we'd create a new [UI Root] that nobody ever cleans up.
            if (_disposed)
            {
                return;
            }

            if (_root != null)
            {
                return;
            }

            _root = new GameObject("[UI Root]");
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }
            // Re-bind layer canvases whenever their backing camera is (re)registered. This keeps
            // scene transitions working when a scene unregisters UI cameras and the next scene
            // registers fresh ones — without this, cached canvases keep pointing to destroyed cameras.
            _cameraSubscription = _cameraRegisteredSubscriber?.Subscribe(OnCameraRegistered);
        }

        // Ensures the layer canvas exists and is bound to its target camera. Fails fast if the
        // target camera has not been registered — callers (UIService.Push) treat this as a
        // setup error and abort the push instead of stalling.
        public bool TryEnsureLayer(UILayer layer, out string error)
        {
            var cameraId = LayerToCameraId(layer);
            if (!_cameraService.TryGetCamera(cameraId, out var camera) || camera == null)
            {
                error = $"Camera '{cameraId}' for layer '{layer}' is not registered. " +
                        "Register a camera with this id via ICameraService and ensure a Base is active (use ICameraService.SetDefaultBase / PushBase) before pushing this window.";
                return false;
            }

            if (_layerCanvases.TryGetValue(layer, out var existing))
            {
                if (existing == null)
                {
                    _layerCanvases.Remove(layer);
                }
                else
                {
                    // Re-bind defensively in case the camera was replaced since last push.
                    if (existing.worldCamera != camera)
                    {
                        existing.worldCamera = camera;
                    }
                    error = null;
                    return true;
                }
            }

            var canvas = CreateLayerCanvas(layer, camera);
            _layerCanvases[layer] = canvas;
            error = null;
            return true;
        }

        void OnCameraRegistered(CameraRegisteredEvent e)
        {
            if (!e.IsRegistered)
            {
                return;
            }
            if (!TryCameraIdToLayer(e.CameraId, out var layer))
            {
                return;
            }
            if (!_layerCanvases.TryGetValue(layer, out var canvas) || canvas == null)
            {
                return;
            }
            if (!_cameraService.TryGetCamera(e.CameraId, out var camera) || camera == null)
            {
                return;
            }
            if (canvas.worldCamera != camera)
            {
                canvas.worldCamera = camera;
            }
        }

        Canvas CreateLayerCanvas(UILayer layer, UnityEngine.Camera camera)
        {
            var go = new GameObject($"UILayer_{layer}");
            go.transform.SetParent(_root.transform, false);
            var layerIndex = GetUnityLayerIndex(layer);
            if (layerIndex >= 0)
            {
                go.layer = layerIndex;
            }

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.sortingOrder = (int)layer;
            canvas.planeDistance = _canvasOptions.PlaneDistance;
            canvas.pixelPerfect = _canvasOptions.PixelPerfect;
            canvas.additionalShaderChannels = _canvasOptions.AdditionalShaderChannels;
            canvas.enabled = false;
            _canvasOptions.ConfigureCanvas?.Invoke(layer, canvas);

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = _canvasOptions.UiScaleMode;
            scaler.referenceResolution = _canvasOptions.ReferenceResolution;
            scaler.screenMatchMode = _canvasOptions.ScreenMatchMode;
            scaler.matchWidthOrHeight = _canvasOptions.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = _canvasOptions.ReferencePixelsPerUnit;
            scaler.scaleFactor = _canvasOptions.ScaleFactor;
            scaler.dynamicPixelsPerUnit = _canvasOptions.DynamicPixelsPerUnit;
            _canvasOptions.ConfigureScaler?.Invoke(layer, scaler);

            go.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        static int GetUnityLayerIndex(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.UI:
                case UILayer.UIOverlay:
                    return LayerMask.NameToLayer(UiLayerName);
                default:
                    return -1;
            }
        }

        public Transform GetLayerTransform(UILayer layer)
        {
            return _layerCanvases[layer].transform;
        }

        public void SetLayerCanvasEnabled(UILayer layer, bool enabled)
        {
            _layerCanvases[layer].enabled = enabled;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _cameraSubscription?.Dispose();
            _cameraSubscription = null;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            _layerCanvases.Clear();
        }
    }
}


