using Cysharp.Threading.Tasks;
using System.Threading;

namespace StickerFwk.Core
{
    public class SceneReadyNotifier
    {
        private UniTaskCompletionSource _source = new UniTaskCompletionSource();

        public UniTask WaitForReady(CancellationToken cancellationToken = default) =>
            _source.Task.AttachExternalCancellation(cancellationToken);

        public void NotifyReady() => _source.TrySetResult();

        public void Reset() => _source = new UniTaskCompletionSource();
    }
}
