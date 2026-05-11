using System.Collections.Generic;
using StickerFwk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraModel
    {
        readonly Dictionary<CameraId, UnityEngine.Camera> _registered = new Dictionary<CameraId, UnityEngine.Camera>();
        readonly Dictionary<CameraId, CameraRenderType> _kinds = new Dictionary<CameraId, CameraRenderType>();
        readonly List<CameraId> _baseStack = new List<CameraId>();
        readonly List<long> _baseStackHandles = new List<long>();
        readonly Dictionary<CameraId, int> _overlayDisableRefs = new Dictionary<CameraId, int>();
        CameraId _pendingDefaultBase;
        long _nextBaseHandle = 1;

        public IReadOnlyDictionary<CameraId, UnityEngine.Camera> Registered => _registered;
        public IReadOnlyDictionary<CameraId, CameraRenderType> Kinds => _kinds;
        public IReadOnlyList<CameraId> BaseStack => _baseStack;
        public IReadOnlyDictionary<CameraId, int> OverlayDisableRefs => _overlayDisableRefs;
        public CameraId PendingDefaultBase => _pendingDefaultBase;

        public void Register(CameraId id, UnityEngine.Camera camera, CameraRenderType kind)
        {
            _registered[id] = camera;
            _kinds[id] = kind;
        }

        public bool Unregister(CameraId id)
        {
            var removed = _registered.Remove(id);
            _kinds.Remove(id);
            RemoveBase(id);
            return removed;
        }

        public bool TryGet(CameraId id, out UnityEngine.Camera camera)
        {
            return _registered.TryGetValue(id, out camera);
        }

        public bool IsRegistered(CameraId id)
        {
            return _registered.ContainsKey(id);
        }

        public bool TryGetKind(CameraId id, out CameraRenderType kind)
        {
            return _kinds.TryGetValue(id, out kind);
        }

        public IReadOnlyList<CameraId> GetRegisteredIds()
        {
            return new List<CameraId>(_registered.Keys);
        }

        public CameraId ActiveBase => _baseStack.Count == 0 ? default(CameraId) : _baseStack[_baseStack.Count - 1];

        public void SetDefaultBase(CameraId id)
        {
            if (_baseStack.Count == 0)
            {
                _baseStack.Add(id);
                _baseStackHandles.Add(0);
                return;
            }

            _baseStack[0] = id;
            _baseStackHandles[0] = 0;
        }

        public void SetPendingDefaultBase(CameraId id)
        {
            _pendingDefaultBase = id;
        }

        public void ClearPendingDefaultBase()
        {
            _pendingDefaultBase = default(CameraId);
        }

        public long PushBase(CameraId id)
        {
            var handle = _nextBaseHandle++;
            _baseStack.Add(id);
            _baseStackHandles.Add(handle);
            return handle;
        }

        public bool PopBase(long handle)
        {
            for (var i = _baseStackHandles.Count - 1; i >= 0; i--)
            {
                if (_baseStackHandles[i] != handle)
                {
                    continue;
                }

                _baseStack.RemoveAt(i);
                _baseStackHandles.RemoveAt(i);
                return true;
            }

            return false;
        }

        public bool RemoveBase(CameraId id)
        {
            var removed = false;
            for (var i = _baseStack.Count - 1; i >= 0; i--)
            {
                if (_baseStack[i] != id)
                {
                    continue;
                }

                _baseStack.RemoveAt(i);
                _baseStackHandles.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        public void DisableOverlay(CameraId id)
        {
            _overlayDisableRefs.TryGetValue(id, out var refs);
            _overlayDisableRefs[id] = refs + 1;
        }

        public void EnableOverlay(CameraId id)
        {
            if (!_overlayDisableRefs.TryGetValue(id, out var refs))
            {
                return;
            }

            refs--;
            if (refs <= 0)
            {
                _overlayDisableRefs.Remove(id);
            }
            else
            {
                _overlayDisableRefs[id] = refs;
            }
        }

        public int GetOverlayDisableRefCount(CameraId id)
        {
            return _overlayDisableRefs.TryGetValue(id, out var refs) ? refs : 0;
        }
    }
}
