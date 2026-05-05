using System.Collections.Generic;
using StickerFwk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraFactory
    {
        readonly Transform _root;

        public CameraFactory(Transform root)
        {
            _root = root;
        }

        public Transform Root => _root;

        public UnityEngine.Camera Create(CameraDefinition definition)
        {
            var go = new GameObject(definition.DisplayName);
            go.transform.SetParent(_root, false);

            var camera = go.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = definition.ClearFlags;
            camera.backgroundColor = definition.BackgroundColor;
            camera.cullingMask = definition.CullingMask;
            camera.nearClipPlane = definition.NearClipPlane;
            camera.farClipPlane = definition.FarClipPlane;
            camera.orthographic = definition.Orthographic;
            camera.orthographicSize = definition.OrthographicSize;
            camera.depth = definition.Depth;

            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = definition.RenderType;
            urp.renderPostProcessing = definition.PostProcessingEnabled;
            urp.SetRenderer(definition.RendererIndex);
            urp.volumeLayerMask = definition.VolumeMask;

            return camera;
        }

        public void DestroyAll(IReadOnlyList<UnityEngine.Camera> cameras)
        {
            for (var i = 0; i < cameras.Count; i++)
            {
                if (cameras[i] != null)
                {
                    Object.Destroy(cameras[i].gameObject);
                }
            }
        }
    }
}
