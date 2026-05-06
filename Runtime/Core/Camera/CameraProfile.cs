using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core
{
    [CreateAssetMenu(menuName = "StickerFwk/Camera/Camera Profile", fileName = "CameraProfile")]
    public class CameraProfile : ScriptableObject
    {
        [SerializeField] CameraProfileId _profileId = CameraProfileId.Gameplay;
        [SerializeField] List<CameraId> _cameras = new List<CameraId>();

        public CameraProfileId ProfileId => _profileId;
        public IReadOnlyList<CameraId> Cameras => _cameras;
    }
}
