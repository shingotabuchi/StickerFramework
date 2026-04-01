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

            // 2. Run the caller's action while screen is covered
            await action(ct);

            // 3. Pop overlay — awaits hide transition to reveal
            await _uiService.Pop<ScreenTransitionView>(ct);
        }
    }
}
