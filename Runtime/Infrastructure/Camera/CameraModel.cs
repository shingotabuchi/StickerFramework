using System.Collections.Generic;
using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraModel
    {
        readonly Dictionary<CameraId, UnityEngine.Camera> _cameras = new Dictionary<CameraId, UnityEngine.Camera>();

        public bool Register(CameraId id, UnityEngine.Camera camera)
        {
            return _cameras.TryAdd(id, camera);
        }

        public bool Unregister(CameraId id)
        {
            return _cameras.Remove(id);
        }

        public bool TryGet(CameraId id, out UnityEngine.Camera camera)
        {
            return _cameras.TryGetValue(id, out camera);
        }

        public bool IsRegistered(CameraId id)
        {
            return _cameras.ContainsKey(id);
        }

        public IReadOnlyList<CameraId> GetRegisteredIds()
        {
            var ids = new List<CameraId>(_cameras.Count);
            foreach (var kvp in _cameras)
            {
                ids.Add(kvp.Key);
            }
            return ids;
        }

        public IReadOnlyDictionary<CameraId, UnityEngine.Camera> GetAll()
        {
            return _cameras;
        }
    }
}
