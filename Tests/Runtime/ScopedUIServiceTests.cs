using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;
using UnityEngine;
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

        sealed class TestWindowView : WindowView { }

        sealed class FakeUIService : IUIService
        {
            readonly Queue<TestWindowView> _views;

            public FakeUIService(params TestWindowView[] views)
            {
                _views = new Queue<TestWindowView>(views);
            }

            public List<WindowView> PoppedViews { get; } = new List<WindowView>();

            public UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView
            {
                return UniTask.FromResult((T)(WindowView)_views.Dequeue());
            }

            public UniTask Pop(WindowView view, CancellationToken ct = default)
            {
                PoppedViews.Add(view);
                return UniTask.CompletedTask;
            }

            public UniTask<T> Replace<T>(string tag = null, WindowOptions options = null,
                CancellationToken ct = default) where T : WindowView
            {
                return Push<T>(tag, options, ct);
            }

            public UniTask Pop(UILayer layer = UILayer.UI, CancellationToken ct = default) => UniTask.CompletedTask;

            public UniTask Pop<T>(CancellationToken ct = default) where T : WindowView => UniTask.CompletedTask;

            public UniTask PopAll(UILayer layer, CancellationToken ct = default) => UniTask.CompletedTask;

            public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView =>
                UniTask.CompletedTask;

            public bool IsOpen<T>() where T : WindowView => false;

            public T GetWindow<T>() where T : WindowView => null;

            public int GetStackCount(UILayer layer) => 0;
        }
    }
}
