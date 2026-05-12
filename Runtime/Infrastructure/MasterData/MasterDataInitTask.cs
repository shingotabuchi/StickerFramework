using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.Initialization;
using StickerFwk.Core.MasterData;
using StickerFwk.Infrastructure.Initialization;
using VContainer;

namespace StickerFwk.Infrastructure.MasterData
{
    /// <summary>
    /// Load-phase task that calls <see cref="IMasterDataRepository.LoadAsync"/> if the repository
    /// is not already loaded. Register via <see cref="UseMasterDataInit"/>.
    /// </summary>
    public sealed class MasterDataInitTask : IInitTask
    {
        private readonly IMasterDataRepository _repository;

        public InitPhase Phase => InitPhase.Load;

        public MasterDataInitTask(IMasterDataRepository repository)
        {
            _repository = repository;
        }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            return _repository.IsLoaded ? UniTask.CompletedTask : _repository.LoadAsync(ct);
        }
    }

    public static class MasterDataInitExtensions
    {
        /// <summary>
        /// Loads the registered <see cref="IMasterDataRepository"/> during the Load phase of root
        /// init. Requires an <see cref="IMasterDataRepository"/> registration on the same container.
        /// </summary>
        public static void UseMasterDataInit(this IContainerBuilder builder)
        {
            builder.AddInitTask<MasterDataInitTask>();
        }
    }
}
