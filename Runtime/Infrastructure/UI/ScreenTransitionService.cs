using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;

namespace StickerFwk.Infrastructure.UI
{
    public class ScreenTransitionService : IScreenTransitionService
    {
        readonly IUIService _uiService;

        public ScreenTransitionService(IUIService uiService)
        {
            _uiService = uiService;
        }

        public async UniTask ExecuteAsync(
            Func<CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            // 1. Push overlay — awaits show transition so screen is fully covered
            await _uiService.Push<ScreenTransitionView>(transitionViewTag, ct: ct);

            try
            {
                // 2. Run the caller's action while screen is covered
                await action(ct);
            }
            finally
            {
                // 3. Pop overlay — awaits hide transition to reveal. Use an uncancelled
                // caller token so a cancelled load does not leave the overlay stuck.
                await _uiService.Pop<ScreenTransitionView>(CancellationToken.None);
            }
        }
    }
}
