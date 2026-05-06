using System;
using UnityEngine;

namespace StickerFwk.Core
{
    [Serializable]
    public class CameraDefinition
    {
        [SerializeField] CameraId _id = CameraId.World;
        [SerializeField] string _displayName;
        [SerializeField] LayerMask _cullingMask = ~0;
        [SerializeField] CameraClearFlags _clearFlags = CameraClearFlags.SolidColor;
        [SerializeField] Color _backgroundColor = Color.black;
        [SerializeField] float _nearClipPlane = 0.3f;
        [SerializeField] float _farClipPlane = 1000f;
        [SerializeField] bool _orthographic = true;
        [SerializeField] float _orthographicSize = 5f;
        [SerializeField] float _depth;
        [SerializeField] bool _postProcessingEnabled;
        [SerializeField] int _rendererIndex = -1;
        [SerializeField] LayerMask _volumeMask = ~0;

        public CameraId Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id.ToString() : _displayName;
        public LayerMask CullingMask => _cullingMask;
        public CameraClearFlags ClearFlags => _clearFlags;
        public Color BackgroundColor => _backgroundColor;
        public float NearClipPlane => _nearClipPlane;
        public float FarClipPlane => _farClipPlane;
        public bool Orthographic => _orthographic;
        public float OrthographicSize => _orthographicSize;
        public float Depth => _depth;
        public bool PostProcessingEnabled => _postProcessingEnabled;
        public int RendererIndex => _rendererIndex;
        public LayerMask VolumeMask => _volumeMask;
    }
}
