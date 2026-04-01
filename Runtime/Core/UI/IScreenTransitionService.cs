using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Covers the screen with a transition overlay, executes an action while covered,
    /// then reveals. Use the tag parameter for different wipe styles (e.g. "wipe", "fade").
    /// No tag uses the default ScreenTransitionView prefab.
    /// </summary>
    public interface IScreenTransitionService
    {
        UniTask ExecuteAsync(
            Func<CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default);
    }
}
