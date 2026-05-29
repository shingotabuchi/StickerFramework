using StickerFwk.Core.Initialization;
using StickerFwk.Infrastructure.Initialization;
using VContainer;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    /// <summary>
    /// Reserved Load-phase hook for framework-level save setup. Game projects hydrate shards via
    /// <c>StickerLocalDataService</c> in their own init task.
    /// </summary>
    public sealed class LocalDataSaveInitTask : IInitTask
    {
        public InitPhase Phase => InitPhase.Load;

        public Cysharp.Threading.Tasks.UniTask ExecuteAsync(System.Threading.CancellationToken ct)
        {
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }
    }

    public static class LocalDataSaveInitExtensions
    {
        public static void UseLocalDataSaveInit(this IContainerBuilder builder)
        {
            builder.AddInitTask<LocalDataSaveInitTask>();
        }
    }
}
