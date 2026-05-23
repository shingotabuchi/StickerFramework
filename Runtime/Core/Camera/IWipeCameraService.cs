using System;
using UnityEngine;

namespace StickerFwk.Core
{
    public interface IWipeCameraService
    {
        IWipeCameraLease Acquire();
    }

    public interface IWipeCameraLease : IDisposable
    {
        Camera Camera { get; }
    }
}
