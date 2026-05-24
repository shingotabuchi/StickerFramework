using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class ScreenTransitionServiceTests
    {
        [Test]
        public void ExecuteAsync_PopsOverlayWhenActionIsCanceled()
        {
            var uiService = new FakeUIService();
            var wipeCameraService = new FakeWipeCameraService();
            var service = new ScreenTransitionService(uiService, wipeCameraService);
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
            Assert.That(wipeCameraService.AcquireCount, Is.EqualTo(1));
            Assert.That(wipeCameraService.DisposeCount, Is.EqualTo(1));
        }

        sealed class FakeWipeCameraService : IWipeCameraService
        {
            public int AcquireCount { get; private set; }
            public int DisposeCount { get; private set; }

            public Camera EnsureCamera() => null;

            public IWipeCameraLease Acquire()
            {
                AcquireCount++;
                return new Lease(this);
            }

            sealed class Lease : IWipeCameraLease
            {
                readonly FakeWipeCameraService _owner;
                bool _disposed;

                public Lease(FakeWipeCameraService owner)
                {
                    _owner = owner;
                }

                public Camera Camera => null;

                public void Dispose()
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    _owner.DisposeCount++;
                }
            }
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
            public UniTask<WindowPushHandle<T>> PushWithHandle<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView => throw new NotSupportedException();
            public UniTask<WindowPushHandle<T>> PushWithHandle<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView, IWindowWithArgs<TArgs> => throw new NotSupportedException();
            public UniTask<WindowPushHandle<T>> PushBelow<T>(WindowView coveringView, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView => throw new NotSupportedException();
            public UniTask<T> PushPrepared<T>(Func<T, CancellationToken, UniTask> prepareAsync, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView => throw new NotSupportedException();
            public bool IsOpen<T>() where T : WindowView => false;
            public T GetWindow<T>() where T : WindowView => null;
            public int GetStackCount(UILayer layer) => 0;
            public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView => UniTask.CompletedTask;
            public void Unload<T>(string tag = null) where T : WindowView { }
        }
    }
}
