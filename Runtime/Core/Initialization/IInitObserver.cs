using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Initialization
{
    /// <summary>
    /// Cross-cutting hook that wraps the entire root initialization pipeline. Use for concerns
    /// that must begin before any <see cref="IInitTask"/> runs and end after every task — for
    /// example, holding an input lock or marking a scene transition as in-progress for the
    /// duration of init.
    /// </summary>
    /// <remarks>
    /// Register via <c>builder.AddInitObserver&lt;T&gt;()</c>. Multiple observers run in
    /// registration order on the way in, and reverse order on the way out.
    /// </remarks>
    public interface IInitObserver
    {
        /// <summary>Invoked once before any <see cref="IInitTask"/> runs.</summary>
        UniTask OnStartingAsync(CancellationToken ct);

        /// <summary>
        /// Invoked once after all tasks complete (or fail/cancel). Runs in a <c>finally</c> block,
        /// so it executes even if a task throws. Only invoked if <see cref="OnStartingAsync"/>
        /// for the same observer completed successfully.
        /// </summary>
        UniTask OnCompletedAsync(CancellationToken ct);
    }
}
