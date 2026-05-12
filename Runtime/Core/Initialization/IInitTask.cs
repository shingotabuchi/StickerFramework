using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Initialization
{
    /// <summary>
    /// A discrete step in the root initialization pipeline. Implementations are registered via
    /// <c>builder.AddInitTask&lt;T&gt;()</c> (or one of the framework's <c>Use*</c> helpers such as
    /// <c>UseTargetFrameRate</c> / <c>UseMasterDataInit</c>) and executed by
    /// <see cref="IRootInitService"/> in their declared <see cref="Phase"/>.
    /// </summary>
    public interface IInitTask
    {
        /// <summary>Phase this task belongs to. Determines when (and how) it runs.</summary>
        InitPhase Phase { get; }

        /// <summary>Performs the task. Honor <paramref name="ct"/> for cooperative cancellation.</summary>
        UniTask ExecuteAsync(CancellationToken ct);
    }
}
