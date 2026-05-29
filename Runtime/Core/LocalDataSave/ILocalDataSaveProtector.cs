using System;

namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveProtector
    {
        byte[] Protect(ReadOnlySpan<byte> plain);

        byte[] Unprotect(ReadOnlySpan<byte> stored);
    }
}
