using System;
using System.Collections.Generic;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Infrastructure.UI
{
    public class UILayerManager
    {
        static readonly UILayer[] AllLayers = (UILayer[])Enum.GetValues(typeof(UILayer));

        readonly Dictionary<UILayer, Canvas> _layerCanvases = new Dictionary<UILayer, Canvas>();
        GameObject _root;

        public void Initialize()
        {
            _root = new GameObject("[UI Root]");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            foreach (var layer in AllLayers)
            {
                CreateLayerCanvas(layer);
            }
        }

        void CreateLayerCanvas(UILayer layer)
        {
            var go = new GameObject($"UILayer_{layer}");
            go.transform.SetParent(_root.transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (int)layer;
            canvas.enabled = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            _layerCanvases[layer] = canvas;
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
