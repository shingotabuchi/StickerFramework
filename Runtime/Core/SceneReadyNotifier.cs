using Cysharp.Threading.Tasks;

namespace StickerFwk.Core
{
    public class SceneReadyNotifier
    {
        private UniTaskCompletionSource _source = new UniTaskCompletionSource();

        public UniTask WaitForReady() => _source.Task;

        public void NotifyReady() => _source.TrySetResult();

        public void Reset() => _source = new UniTaskCompletionSource();
    }
}
