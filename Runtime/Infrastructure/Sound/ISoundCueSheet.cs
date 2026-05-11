using System.Collections.Generic;

namespace StickerFwk.Infrastructure.Sound
{
    public interface ISoundCueSheet
    {
        string Name { get; }
        IReadOnlyList<ISoundData> SoundDatas { get; }
    }
}
