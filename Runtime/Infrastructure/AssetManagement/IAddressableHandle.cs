using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Infrastructure.AssetManagement
{
    public interface IAddressableHandle
    {
        Object Object { get; }
        IReadOnlyList<Object> Objects { get; }
        bool Succeeded { get; }
        void Release();
    }
}