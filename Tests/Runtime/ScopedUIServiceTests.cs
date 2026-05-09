using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime
{
    public class ScopedUIServiceTests
    {
        [Test]
        public async Task DisposePopsTrackedWindowsInReversePushOrder()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            var secondObject = new GameObject("second", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var second = secondObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first, second);
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();
                await scoped.Push<TestWindowView>();
                scoped.Dispose();
                await UniTask.Yield();

                CollectionAssert.AreEqual(new[] { second, first }, inner.PoppedViews);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public async Task PopByViewRemovesFromTracking()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            var secondObject = new GameObject("second", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var second = secondObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first, second);
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();
                await scoped.Push<TestWindowView>();
                await scoped.Pop(second);
                await scoped.Pop(first);
                inner.PoppedViews.Clear();

                scoped.Dispose();
                await UniTask.Yield();

                CollectionAssert.IsEmpty(inner.PoppedViews);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public async Task PopGenericRemovesTopmostMatchFromTracking()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            var secondObject = new GameObject("second", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var second = secondObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first, second);
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();
                await scoped.Push<TestWindowView>();
                await scoped.Pop<TestWindowView>();

                scoped.Dispose();
                await UniTask.Yield();

                CollectionAssert.AreEqual(new[] { first }, inner.PoppedViews);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public async Task PopAllRemovesTrackedWindowsOnLayer()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            var secondObject = new GameObject("second", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var second = secondObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first, second);
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();
                await scoped.Push<TestWindowView>();
                await scoped.PopAll(UILayer.UI);

                scoped.Dispose();
                await UniTask.Yield();

                CollectionAssert.IsEmpty(inner.PoppedViews);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public async Task DestroyedTrackedWindowsArePrunedOnPush()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            var secondObject = new GameObject("second", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var second = secondObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first, second);
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();
                Object.DestroyImmediate(firstObject);
                await scoped.Push<TestWindowView>();

                scoped.Dispose();
                await UniTask.Yield();

                CollectionAssert.AreEqual(new[] { second }, inner.PoppedViews);
            }
            finally
            {
                if (firstObject != null) Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public async Task DisposeSwallowsOperationCanceledFromInnerPop()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first) { PopException = new OperationCanceledException() };
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();

                scoped.Dispose();
                await UniTask.Yield();

                // No LogAssert.Expect call: if Log.Error was emitted, the test runner
                // would fail with "Tests should not generate any logs".
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
            }
        }

        [Test]
        public async Task DisposeLogsNonCancellationFailuresFromInnerPop()
        {
            var firstObject = new GameObject("first", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var first = firstObject.AddComponent<TestWindowView>();
                var inner = new FakeUIService(first) { PopException = new InvalidOperationException("boom") };
                var scoped = new ScopedUIService(inner);

                await scoped.Push<TestWindowView>();

                scoped.Dispose();
                await UniTask.Yield();

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Pop during scope dispose failed.*"));
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
            }
        }

        sealed class TestWindowView : WindowView { }

        sealed class FakeUIService : IUIService
        {
            readonly Queue<TestWindowView> _views;

            public FakeUIService(params TestWindowView[] views)
            {
                _views = new Queue<TestWindowView>(views);
            }

            public List<WindowView> PoppedViews { get; } = new List<WindowView>();

            public Exception PopException { get; set; }

            public UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView
            {
                return UniTask.FromResult((T)(WindowView)_views.Dequeue());
            }

            public UniTask<T> Push<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView, IWindowWithArgs<TArgs>
            {
                return Push<T>(tag, options, ct);
            }

            public UniTask<bool> Pop(WindowView view, CancellationToken ct = default)
            {
                return Pop(view, immediate: false, ct);
            }

            public UniTask<bool> Pop(WindowView view, bool immediate, CancellationToken ct = default)
            {
                if (PopException != null)
                {
                    return UniTask.FromException<bool>(PopException);
                }
                PoppedViews.Add(view);
                return UniTask.FromResult(true);
            }

            public UniTask<T> Replace<T>(string tag = null, WindowOptions options = null,
                CancellationToken ct = default) where T : WindowView
            {
                return Push<T>(tag, options, ct);
            }

            public UniTask<T> Replace<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView, IWindowWithArgs<TArgs>
            {
                return Push<T>(tag, options, ct);
            }

            public UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default) => UniTask.FromResult(true);

            public UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView => UniTask.FromResult(true);

            public UniTask<int> PopAll(UILayer layer, bool immediate = false, CancellationToken ct = default) => UniTask.FromResult(0);

            public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView =>
                UniTask.CompletedTask;

            public void Unload<T>(string tag = null) where T : WindowView { }

            public bool IsOpen<T>() where T : WindowView => false;

            public T GetWindow<T>() where T : WindowView => null;

            public int GetStackCount(UILayer layer) => 0;
        }
    }
}
