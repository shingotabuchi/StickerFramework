using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.Presentation;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Tests.Runtime
{
    public class WindowViewGenericTests
    {
        [Test]
        public void BindPresenterBindsPresenterToView()
        {
            var go = new GameObject("window", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var view = go.AddComponent<TestWindow>();
                var presenter = new TestPresenter();

                view.Inject(presenter);

                Assert.That(presenter.BoundView, Is.SameAs(view));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BindPresenterTwiceWithDifferentInstancesThrows()
        {
            var go = new GameObject("window", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var view = go.AddComponent<TestWindow>();
                view.Inject(new TestPresenter());

                Assert.Throws<System.InvalidOperationException>(() => view.Inject(new TestPresenter()));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task LifecycleHooksForwardToPresenter()
        {
            var go = new GameObject("window", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var view = go.AddComponent<TestWindow>();
                var presenter = new TestPresenter();
                view.Inject(presenter);

                await view.OnInitialize(CancellationToken.None);
                view.OnBeforeShow();
                view.OnShow();
                view.OnBeforeHide();
                view.OnHide();
                view.OnDispose();

                Assert.That(presenter.InitializeCount, Is.EqualTo(1));
                Assert.That(presenter.BeforeShowCount, Is.EqualTo(1));
                Assert.That(presenter.ShowCount, Is.EqualTo(1));
                Assert.That(presenter.BeforeHideCount, Is.EqualTo(1));
                Assert.That(presenter.HideCount, Is.EqualTo(1));
                Assert.That(presenter.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnDisposeDisposesPresenterEvenWhenNotOverridden()
        {
            var go = new GameObject("window", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var view = go.AddComponent<TestWindow>();
                var presenter = new TestPresenter();
                view.Inject(presenter);

                view.OnDispose();

                Assert.That(presenter.DisposeCount, Is.EqualTo(1));

                // A second dispose call (e.g., redundant teardown) must be safe.
                view.OnDispose();
                Assert.That(presenter.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        sealed class TestWindow : WindowView<TestWindow, TestPresenter>
        {
            public void Inject(TestPresenter presenter) => BindPresenter(presenter);
        }

        sealed class TestPresenter : IWindowPresenter<TestWindow>
        {
            public TestWindow BoundView { get; private set; }
            public int InitializeCount { get; private set; }
            public int BeforeShowCount { get; private set; }
            public int ShowCount { get; private set; }
            public int BeforeHideCount { get; private set; }
            public int HideCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Bind(TestWindow view) => BoundView = view;
            public void Unbind() => BoundView = null;

            public UniTask InitializeAsync(CancellationToken ct)
            {
                InitializeCount++;
                return UniTask.CompletedTask;
            }

            public void OnBeforeShow() => BeforeShowCount++;
            public void OnShow() => ShowCount++;
            public void OnBeforeHide() => BeforeHideCount++;
            public void OnHide() => HideCount++;

            public void Dispose()
            {
                DisposeCount++;
                BoundView = null;
            }
        }
    }
}
