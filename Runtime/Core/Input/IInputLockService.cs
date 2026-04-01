using System;

namespace StickerFwk.Core
{
    public interface IInputLockService
    {
        bool IsLocked { get; }
        IDisposable Lock();
    }
}
