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
        readonly ICameraService _cameraService;
        readonly ISubscriber<CameraRegisteredEvent> _cameraRegisteredSubscriber;
        readonly Dictionary<UILayer, Canvas> _layerCanvases = new Dictionary<UILayer, Canvas>();
        IDisposable _cameraSubscription;
        GameObject _root;
        bool _disposed;

        public UILayerManager(
            ICameraService cameraService,
            ISubscriber<CameraRegisteredEvent> cameraRegisteredSubscriber)
        {
            _cameraService = cameraService;
            _cameraRegisteredSubscriber = cameraRegisteredSubscriber;
        }

        public static CameraId LayerToCameraId(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.UI: return CameraId.UI;
                case UILayer.UIOverlay: return CameraId.UIOverlay;
                case UILayer.Wipe: return CameraId.Wipe;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "No camera mapping for layer");
            }
        }

        static bool TryCameraIdToLayer(CameraId cameraId, out UILayer layer)
        {
            switch (cameraId)
            {
                case CameraId.UI: layer = UILayer.UI; return true;
                case CameraId.UIOverlay: layer = UILayer.UIOverlay; return true;
                case CameraId.Wipe: layer = UILayer.Wipe; return true;
                default: layer = default; return false;
            }
        }

        public void Initialize()
        {
            // Guards against DI ordering quirks where Start runs after Dispose: without this,
            // we'd create a new [UI Root] that nobody ever cleans up.
            if (_disposed)
            {
                return;
            }

            _root = new GameObject("[UI Root]");
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }
            // Re-bind layer canvases whenever their backing camera is (re)registered. This is what
            // keeps Game→Game scene transitions working: the previous Gameplay profile's UI/UIOverlay
            // cameras are destroyed during the transition, then the new Gameplay profile registers
            // fresh ones — without this, cached canvases keep pointing to the destroyed cameras.
            _cameraSubscription = _cameraRegisteredSubscriber?.Subscribe(OnCameraRegistered);
        }

        // Ensures the layer canvas exists and is bound to its target camera. Fails fast if the
        // camera profile has not registered the target camera — callers (UIService.Push) treat
        // this as a setup error and abort the push instead of stalling.
        public bool TryEnsureLayer(UILayer layer, out string error)
        {
            var cameraId = LayerToCameraId(layer);
            if (!_cameraService.TryGetCamera(cameraId, out var camera) || camera == null)
            {
                error = $"Camera '{cameraId}' for layer '{layer}' is not registered. " +
                        "Apply a CameraProfile that includes it (e.g. via RootLifetimeScope) before pushing this window.";
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
            var uiLayerIndex = LayerMask.NameToLayer("UI");
            if (uiLayerIndex >= 0)
            {
                go.layer = uiLayerIndex;
            }

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.sortingOrder = (int)layer;
            canvas.enabled = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            return canvas;
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



