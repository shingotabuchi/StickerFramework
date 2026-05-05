using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core
{
    [CreateAssetMenu(menuName = "StickerFwk/Camera/Camera Profile", fileName = "CameraProfile")]
    public class CameraProfile : ScriptableObject
    {
        [SerializeField] CameraProfileId _profileId = CameraProfileId.Gameplay;
        [SerializeField] CameraId _baseCamera = CameraId.World;
        [SerializeField] List<CameraDefinition> _cameras = new List<CameraDefinition>();
        [SerializeField] List<CameraId> _defaultStackOrder = new List<CameraId>();

        public CameraProfileId ProfileId => _profileId;
        public CameraId BaseCamera => _baseCamera;
        public IReadOnlyList<CameraDefinition> Cameras => _cameras;
        public IReadOnlyList<CameraId> DefaultStackOrder => _defaultStackOrder;

        public bool TryGetDefinition(CameraId id, out CameraDefinition definition)
        {
            for (var i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i] != null && _cameras[i].Id == id)
                {
                    definition = _cameras[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
