using System.Collections.Generic;

namespace StickerFwk.Infrastructure.Haptics
{
    public interface IHapticCueSheet
    {
        string Name { get; }
        IReadOnlyList<IHapticData> HapticDatas { get; }
    }
}
