using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core
{
    [CreateAssetMenu(menuName = "StickerFwk/Camera/Camera System Settings", fileName = "CameraSystemSettings")]
    public class CameraSystemSettings : ScriptableObject
    {
        [SerializeField] List<CameraProfile> _profiles = new List<CameraProfile>();
        [SerializeField] string _cameraRootName = "[Cameras]";
        [SerializeField] string _audioListenerName = "[AudioListener]";

        public IReadOnlyList<CameraProfile> Profiles => _profiles;
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
    }
}
