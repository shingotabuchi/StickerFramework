using System;
using System.Collections.Generic;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Infrastructure.UI
{
    public class UILayerManager
    {
        readonly ICameraService _cameraService;
        readonly Dictionary<UILayer, Canvas> _layerCanvases = new Dictionary<UILayer, Canvas>();
        GameObject _root;

        public UILayerManager(ICameraService cameraService)
        {
            _cameraService = cameraService;
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

        public void Initialize()
        {
            _root = new GameObject("[UI Root]");
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        // Ensures the layer canvas exists and is bound to its target camera. Fails fast if the
        // camera profile has not registered the target camera — callers (UIService.Push) treat
        // this as a setup error and abort the push instead of stalling.
        public bool TryEnsureLayer(UILayer layer, out string error)
        {
            if (_layerCanvases.ContainsKey(layer))
            {
                error = null;
                return true;
            }

            var cameraId = LayerToCameraId(layer);
            if (!_cameraService.TryGetCamera(cameraId, out var camera) || camera == null)
            {
                error = $"Camera '{cameraId}' for layer '{layer}' is not registered. " +
                        "Apply a CameraProfile that includes it (e.g. via RootLifetimeScope) before pushing this window.";
                return false;
            }

            var canvas = CreateLayerCanvas(layer, camera);
            _layerCanvases[layer] = canvas;
            error = null;
            return true;
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
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _layerCanvases.Clear();
        }
    }
}


