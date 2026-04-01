using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.Initialization;
using StickerFwk.Core.MasterData;
using VContainer.Unity;

namespace StickerFwk.Infrastructure.Initialization
{
    public class RootInitService : IRootInitService, IAsyncStartable
    {
        private readonly IMasterDataRepository _masterDataRepository;
        private readonly UniTaskCompletionSource _completionSource = new UniTaskCompletionSource();

        public UniTask Initialization => _completionSource.Task;

        public RootInitService(IMasterDataRepository masterDataRepository)
        {
            _masterDataRepository = masterDataRepository;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            UnityEngine.Application.targetFrameRate = 60;

            Log.Info("RootInitService", "Root initialization started.");

            if (!_masterDataRepository.IsLoaded)
            {
                await _masterDataRepository.LoadAsync(cancellation);
                Log.Info("RootInitService", "Master data loaded.");
            }

            _completionSource.TrySetResult();
            Log.Info("RootInitService", "Root initialization complete.");
        }
    }
}
