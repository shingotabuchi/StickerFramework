using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;

namespace StickerFwk.Tests.Runtime
{
    public class ScreenTransitionServiceTests
    {
        [Test]
        public void ExecuteAsync_PopsOverlayWhenActionIsCanceled()
        {
            var uiService = new FakeUIService();
            var service = new ScreenTransitionService(uiService);
            using var cts = new CancellationTokenSource();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await service.ExecuteAsync(ct =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(ct);
                }, ct: cts.Token).AsTask());

            Assert.That(uiService.PushCount, Is.EqualTo(1));
            Assert.That(uiService.PopGenericCount, Is.EqualTo(1));
            Assert.That(uiService.PopGenericTokenWasCanceled, Is.False);
        }

        sealed class FakeUIService : IUIService
        {
            public int PushCount { get; private set; }
            public int PopGenericCount { get; private set; }
            public bool PopGenericTokenWasCanceled { get; private set; }

            public UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView
            {
                PushCount++;
                return UniTask.FromResult<T>(null);
            }

            public UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView
            {
                PopGenericCount++;
                PopGenericTokenWasCanceled = ct.IsCancellationRequested;
                return UniTask.FromResult(true);
            }

            public UniTask<T> Push<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView, IWindowWithArgs<TArgs> => throw new NotSupportedException();
            public UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default) => throw new NotSupportedException();
            public UniTask<bool> Pop(WindowView view, CancellationToken ct = default) => throw new NotSupportedException();
            public UniTask<bool> Pop(WindowView view, bool immediate, CancellationToken ct = default) => throw new NotSupportedException();
            public UniTask<T> Replace<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default) where T : WindowView => throw new NotSupportedException();
            public UniTask<T> Replace<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView, IWindowWithArgs<TArgs> => throw new NotSupportedException();
            public UniTask<int> PopAll(UILayer layer, bool immediate = false, CancellationToken ct = default) => throw new NotSupportedException();
            public bool IsOpen<T>() where T : WindowView => false;
            public T GetWindow<T>() where T : WindowView => null;
            public int GetStackCount(UILayer layer) => 0;
            public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView => UniTask.CompletedTask;
            public void Unload<T>(string tag = null) where T : WindowView { }
        }
    }
}
