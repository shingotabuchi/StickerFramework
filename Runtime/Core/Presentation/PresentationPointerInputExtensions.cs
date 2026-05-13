using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Presentation
{
    /// <summary>
    /// Await helpers for pointer press/release transitions in presentation flows.
    /// </summary>
    public static class PresentationPointerInputExtensions
    {
        public static async UniTask WaitForFreshPressAsync(
            this PresentationPointerInput pointerInput,
            CancellationToken cancellationToken = default)
        {
            while (pointerInput.IsPressed())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            while (!pointerInput.WasPressedThisFrame())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public static async UniTask WaitForReleaseAsync(
            this PresentationPointerInput pointerInput,
            CancellationToken cancellationToken = default)
        {
            while (pointerInput.IsPressed())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
