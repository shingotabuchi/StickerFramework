using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;
using UnityEngine;

namespace StickerFwk.Tests.Runtime
{
    public class WindowLifecycleRunnerTests
    {
        [Test]
        public async Task ShowAndHideWithoutTransitionRunLifecycleHooks()
        {
            var gameObject = new GameObject("window", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var view = gameObject.AddComponent<TestWindowView>();
                var runner = new WindowLifecycleRunner();

                await runner.Show(view, TransitionType.None, 0f, CancellationToken.None);
                runner.HideWithoutTransition(view);

                Assert.That(view.BeforeShowCount, Is.EqualTo(1));
                Assert.That(view.ShowCount, Is.EqualTo(1));
                Assert.That(view.BeforeHideCount, Is.EqualTo(1));
                Assert.That(view.HideCount, Is.EqualTo(1));
                Assert.That(view.CanvasGroup.alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        sealed class TestWindowView : WindowView
        {
            public int BeforeShowCount { get; private set; }
            public int ShowCount { get; private set; }
            public int BeforeHideCount { get; private set; }
            public int HideCount { get; private set; }

            protected override void OnBeforeShowInternal() => BeforeShowCount++;

            protected override void OnShowInternal() => ShowCount++;

            protected override void OnBeforeHideInternal() => BeforeHideCount++;

            protected override void OnHideInternal() => HideCount++;
        }
    }
}
