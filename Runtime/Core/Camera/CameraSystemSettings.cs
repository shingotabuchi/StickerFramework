using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core
{
    [CreateAssetMenu(menuName = "StickerFwk/Camera/Camera System Settings", fileName = "CameraSystemSettings")]
    public class CameraSystemSettings : ScriptableObject
    {
        [SerializeField] List<CameraProfile> _profiles = new List<CameraProfile>();
        [SerializeField] List<CameraDefinition> _cameraDefinitions = new List<CameraDefinition>();
        [SerializeField] string _cameraRootName = "[Cameras]";
        [SerializeField] string _audioListenerName = "[AudioListener]";

        public IReadOnlyList<CameraProfile> Profiles => _profiles;
        public IReadOnlyList<CameraDefinition> CameraDefinitions => _cameraDefinitions;
        public string CameraRootName => _cameraRootName;
        public string AudioListenerName => _audioListenerName;

        public bool TryGetProfile(CameraProfileId id, out CameraProfile profile)
        {
            for (var i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i] != null && _profiles[i].ProfileId == id)
                {
                    profile = _profiles[i];
                    return true;
                }
            }

            profile = null;
            return false;
        }

        public bool TryGetDefinition(CameraId id, out CameraDefinition definition)
        {
            for (var i = 0; i < _cameraDefinitions.Count; i++)
            {
                if (_cameraDefinitions[i] != null && _cameraDefinitions[i].Id == id)
                {
                    definition = _cameraDefinitions[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            for (var i = 0; i < _cameraDefinitions.Count; i++)
            {
                if (_cameraDefinitions[i] == null)
                {
                    continue;
                }
                for (var j = i + 1; j < _cameraDefinitions.Count; j++)
                {
                    if (_cameraDefinitions[j] != null && _cameraDefinitions[j].Id == _cameraDefinitions[i].Id)
                    {
                        Debug.LogWarning(
                            $"[CameraSystemSettings] Duplicate CameraDefinition for CameraId '{_cameraDefinitions[i].Id}' at indices {i} and {j}.",
                            this);
                    }
                }
            }
        }
#endif
    }
}
