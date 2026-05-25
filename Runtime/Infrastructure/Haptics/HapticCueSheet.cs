using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Infrastructure.Haptics
{
    [CreateAssetMenu(fileName = "HapticCueSheet", menuName = "HapticCueSheet", order = 0)]
    public class HapticCueSheet : ScriptableObject, IHapticCueSheet
    {
        [SerializeField] private List<HapticData> _hapticDatas = new();

        public string Name => name;
        public IReadOnlyList<IHapticData> HapticDatas => _hapticDatas;
    }
}
