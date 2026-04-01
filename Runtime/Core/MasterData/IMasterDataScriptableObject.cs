using System;
using System.Collections.Generic;

namespace StickerFwk.Core.MasterData
{
    public interface IMasterDataScriptableObject
    {
        Type Type { get; }
        IReadOnlyList<IMasterData> Data { get; }
    }
}