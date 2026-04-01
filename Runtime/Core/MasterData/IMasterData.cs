using System;

namespace StickerFwk.Core.MasterData
{
    public interface IMasterData
    {
        string Id { get; }
        Type Type { get; }
    }
}