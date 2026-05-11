using System.Threading;

namespace StickerFwk.Core
{
    /// <summary>
    /// Cancellation token for the current DI scope. The token is cancelled when
    /// the owning LifetimeScope is destroyed or the scope container is disposed.
    /// </summary>
    public interface IScopeCancellation : ICancellationHandle
    {
        /// <summary>
        /// Creates a child handle linked to this scope. Disposing the child cancels
        /// only that child operation; disposing the scope cancels all linked children.
        /// </summary>
        ICancellationHandle CreateLinked(CancellationToken cancellationToken = default);
    }
}
