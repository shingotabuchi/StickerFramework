using System;
using StickerFwk.Core.LocalDataSave;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class PassThroughLocalDataSaveProtector : ILocalDataSaveProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plain)
        {
            return plain.ToArray();
        }

        public byte[] Unprotect(ReadOnlySpan<byte> stored)
        {
            return stored.ToArray();
        }
    }
}
