using Cysharp.Threading.Tasks;
using System.Threading;

namespace StickerFwk.Core
{
    /// <summary>
    /// Transition→scene signal that the screen transition (wipe / fade) has
    /// fully revealed. Counterpart to <see cref="SceneReadyNotifier"/>, which
    /// flows the other direction (scene→transition). Destination-scene code
    /// awaits <see cref="WaitForRevealed"/> before starting post-transition
    /// visible animations so they don't race the reveal scrub.
    ///
    /// Default state is "already revealed" so consumers that await this when
    /// no transition is active (e.g., direct scene play in the Editor) pass
    /// through immediately. Transition services <see cref="Reset"/> the
    /// notifier just before concealing and call <see cref="NotifyRevealed"/>
    /// once the reveal scrub completes.
    /// </summary>
    public class SceneRevealedNotifier
    {
        private UniTaskCompletionSource _source;

        public SceneRevealedNotifier()
        {
            _source = new UniTaskCompletionSource();
            _source.TrySetResult();
        }

        public UniTask WaitForRevealed(CancellationToken cancellationToken = default) =>
            _source.Task.AttachExternalCancellation(cancellationToken);

        public void NotifyRevealed() => _source.TrySetResult();

        public void Reset() => _source = new UniTaskCompletionSource();
    }
}
