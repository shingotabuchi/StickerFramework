using System;
using System.Threading;

namespace StickerFwk.Core
{
    /// <summary>
    /// Disposable cancellation handle. Disposing the handle cancels its token.
    /// </summary>
    public interface ICancellationHandle : IDisposable
    {
        CancellationToken Token { get; }
        bool IsCancellationRequested { get; }
    }
}
