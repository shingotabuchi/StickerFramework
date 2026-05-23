using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using VContainer;

namespace StickerFwk.Infrastructure.UI
{
    public class ScreenTransitionService : IScreenTransitionService
    {
        private static readonly IProgress<float> NoProgress = new NullProgress();

        readonly IUIService _uiService;
        readonly IWipeCameraService _wipeCameraService;

        [Inject]
        public ScreenTransitionService(
            IUIService uiService,
            IWipeCameraService wipeCameraService)
        {
            _uiService = uiService;
            _wipeCameraService = wipeCameraService;
        }

        public async UniTask ExecuteAsync(
            Func<CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await ExecuteAsync((_, actionCt) => action(actionCt), transitionViewTag, ct);
        }

        public async UniTask ExecuteAsync(
            Func<IProgress<float>, CancellationToken, UniTask> action,
            string transitionViewTag = null,
            CancellationToken ct = default)
        {
            // 1. Push overlay — awaits show transition so screen is fully covered
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var cameraLease = _wipeCameraService.Acquire();
            var didPush = false;

            try
            {
                var view = await _uiService.Push<ScreenTransitionView>(transitionViewTag, ct: ct);
                didPush = true;

                var progress = view is IScreenTransitionProgressSink sink
                    ? new ScreenTransitionProgress(sink)
                    : NoProgress;
                progress.Report(0f);

                // 2. Run the caller's action while screen is covered
                await action(progress, ct);
                progress.Report(1f);
            }
            finally
            {
                try
                {
                    if (didPush)
                    {
                        // 3. Pop overlay — awaits hide transition to reveal. Use an uncancelled
                        // caller token so a cancelled load does not leave the overlay stuck.
                        await _uiService.Pop<ScreenTransitionView>(CancellationToken.None);
                    }
                }
                finally
                {
                    cameraLease.Dispose();
                }
            }
        }

        private sealed class NullProgress : IProgress<float>
        {
            public void Report(float value) { }
        }

        private sealed class ScreenTransitionProgress : IProgress<float>
        {
            private readonly IScreenTransitionProgressSink _sink;

            public ScreenTransitionProgress(IScreenTransitionProgressSink sink)
            {
                _sink = sink;
            }

            public void Report(float value)
            {
                _sink.SetProgress(value);
            }
        }
    }
}
