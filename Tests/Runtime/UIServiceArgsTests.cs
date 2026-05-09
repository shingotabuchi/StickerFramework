using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;
using UnityEngine;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime
{
    // Tests that verify IWindowWithArgs<TArgs> support in UIService:
    //   * Push<T, TArgs> calls SetArgs with the supplied value.
    //   * SetArgs is invoked BEFORE OnInitialize runs.
    //   * Replace<T, TArgs> likewise.
    //   * Two pushes with different args each receive their own args (no leakage).
    //   * Cancellation during the Inject→SetArgs→OnInitialize window unwinds cleanly.
    //   * The existing no-args Push<T> continues to work.
    public class UIServiceArgsTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<UIService> _services = new List<UIService>();
        Camera _camera;

        [SetUp]
        public void SetUp()
        {
            var cameraGo = new GameObject("TestCamera");
            _spawned.Add(cameraGo);
            _camera = cameraGo.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var service in _services)
            {
                try { service.Dispose(); } catch { /* ignored */ }
            }
            _services.Clear();

            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
        }

        // ---------- Tests ----------

        [Test]
        public async Task PushWithArgs_CallsSetArgsWithProvidedValue()
        {
            var prefab = MakePrefab<TestArgsView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestArgsView.prefab", prefab);
            var service = NewService(requester);

            var args = new TestArgs { Value = 42 };
            var view = await service.Push<TestArgsView, TestArgs>(args, options: AutoCompleteOptions()).AsTask();

            Assert.That(view.ReceivedArgs, Is.Not.Null);
            Assert.That(view.ReceivedArgs.Value, Is.EqualTo(42));
        }

        [Test]
        public async Task PushWithArgs_SetArgsIsCalledBeforeOnInitialize()
        {
            var prefab = MakePrefab<TestArgsView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestArgsView.prefab", prefab);
            var service = NewService(requester);

            var args = new TestArgs { Value = 99 };
            var view = await service.Push<TestArgsView, TestArgs>(args, options: AutoCompleteOptions()).AsTask();

            Assert.That(view.ArgsSetBeforeInitialize, Is.True,
                "SetArgs must be called before OnInitialize runs");
        }

        [Test]
        public async Task ReplaceWithArgs_CallsSetArgsAndInitializesCorrectly()
        {
            var prefab = MakePrefab<TestArgsView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestArgsView.prefab", prefab);
            var service = NewService(requester);

            // Seed with a regular push first.
            await service.Push<TestArgsView, TestArgs>(new TestArgs { Value = 1 }, options: AutoCompleteOptions()).AsTask();

            var args = new TestArgs { Value = 77 };
            var view = await service.Replace<TestArgsView, TestArgs>(args, options: AutoCompleteOptions()).AsTask();

            Assert.That(view.ReceivedArgs, Is.Not.Null);
            Assert.That(view.ReceivedArgs.Value, Is.EqualTo(77));
            Assert.That(view.ArgsSetBeforeInitialize, Is.True);
        }

        [Test]
        public async Task TwoPushesWithDifferentArgs_EachViewGetsItsOwnArgs()
        {
            var prefabA = MakePrefab<TestArgsViewA>(UILayer.UI);
            var prefabB = MakePrefab<TestArgsViewB>(UILayer.UIOverlay);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestArgsViewA.prefab", prefabA);
            requester.Add("Views/TestArgsViewB.prefab", prefabB);
            var service = NewService(requester);

            var argsA = new TestArgs { Value = 10 };
            var argsB = new TestArgs { Value = 20 };

            var viewA = await service.Push<TestArgsViewA, TestArgs>(argsA, options: AutoCompleteOptions()).AsTask();
            var viewB = await service.Push<TestArgsViewB, TestArgs>(argsB, options: AutoCompleteOptions()).AsTask();

            Assert.That(viewA.ReceivedArgs.Value, Is.EqualTo(10), "viewA should have its own args");
            Assert.That(viewB.ReceivedArgs.Value, Is.EqualTo(20), "viewB should have its own args");
        }

        [Test]
        public async Task CancellationDuringPushWithArgs_UnwindsCleanly()
        {
            var prefab = MakePrefab<SlowInitArgsView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/SlowInitArgsView.prefab", prefab);
            var service = NewService(requester);

            using var cts = new CancellationTokenSource();
            var args = new TestArgs { Value = 5 };

            // SlowInitArgsView.OnInitialize awaits a gate. Cancel while it's blocked.
            var pushTask = service.Push<SlowInitArgsView, TestArgs>(args, options: AutoCompleteOptions(), ct: cts.Token).AsTask();

            await SlowInitArgsView.WaitForInitStartAsync();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await pushTask);

            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(0),
                "cancelled push must not leave a handle on the stack");
            Assert.That(requester.AssetHandle("Views/SlowInitArgsView.prefab").DisposeCount, Is.EqualTo(1),
                "asset handle must be disposed exactly once after cancellation");
        }

        [Test]
        public async Task NoArgsPushStillWorks()
        {
            var prefab = MakePrefab<PlainTestView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/PlainTestView.prefab", prefab);
            var service = NewService(requester);

            var view = await service.Push<PlainTestView>(options: AutoCompleteOptions()).AsTask();

            Assert.That(view, Is.Not.Null);
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));
        }

        // ---------- Helpers ----------

        UIService NewService(FakeAssetRequester requester)
        {
            var cameraService = new FakeCameraService(_camera);
            var service = new UIService(
                requester,
                resolver: null,
                cameraService,
                cameraRegisteredSubscriber: null,
                new FakePublisher<WindowOpenedEvent>(),
                new FakePublisher<WindowClosedEvent>());
            service.Start();
            _services.Add(service);
            return service;
        }

        WindowOptions AutoCompleteOptions()
        {
            return new WindowOptions
            {
                ShowTransition = new InstantTransition(),
                HideTransition = new InstantTransition(),
                TransitionDuration = 0f,
                IsBlocking = true,
                Inject = _ => { },
            };
        }

        GameObject MakePrefab<T>(UILayer layer) where T : WindowView
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
            _spawned.Add(go);
            var view = go.AddComponent<T>();
            view.__SetLayerForTests(layer);
            return go;
        }

        // ---------- Test data ----------

        sealed class TestArgs
        {
            public int Value;
        }

        // ---------- Test views ----------

        sealed class TestArgsView : WindowView, IWindowWithArgs<TestArgs>
        {
            public TestArgs ReceivedArgs { get; private set; }
            public bool ArgsSetBeforeInitialize { get; private set; }
            bool _initializeCalled;

            public void SetArgs(TestArgs args)
            {
                ReceivedArgs = args;
                ArgsSetBeforeInitialize = !_initializeCalled;
            }

            public override UniTask OnInitialize(CancellationToken ct)
            {
                _initializeCalled = true;
                return UniTask.CompletedTask;
            }
        }

        sealed class TestArgsViewA : WindowView, IWindowWithArgs<TestArgs>
        {
            public TestArgs ReceivedArgs { get; private set; }

            public void SetArgs(TestArgs args) => ReceivedArgs = args;
        }

        sealed class TestArgsViewB : WindowView, IWindowWithArgs<TestArgs>
        {
            public TestArgs ReceivedArgs { get; private set; }

            public void SetArgs(TestArgs args) => ReceivedArgs = args;
        }

        sealed class PlainTestView : WindowView { }

        sealed class SlowInitArgsView : WindowView, IWindowWithArgs<TestArgs>
        {
            static UniTaskCompletionSource _initStarted = new UniTaskCompletionSource();
            static UniTaskCompletionSource _gate = new UniTaskCompletionSource();

            public void SetArgs(TestArgs args) { }

            public static UniTask WaitForInitStartAsync() => _initStarted.Task;

            public static void Reset()
            {
                _initStarted = new UniTaskCompletionSource();
                _gate = new UniTaskCompletionSource();
            }

            public override async UniTask OnInitialize(CancellationToken ct)
            {
                _initStarted.TrySetResult();
                using (ct.Register(() => _gate.TrySetCanceled()))
                {
                    await _gate.Task;
                }
                ct.ThrowIfCancellationRequested();
            }
        }

        // ---------- Test doubles (mirrored from UIServiceConcurrencyTests) ----------

        sealed class InstantTransition : ITransition
        {
            public UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
            {
                if (view != null && view.CanvasGroup != null)
                {
                    view.CanvasGroup.alpha = isShow ? 1f : 0f;
                }
                return UniTask.CompletedTask;
            }
        }

        sealed class FakeAssetRequester : IAssetRequester
        {
            readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
            readonly Dictionary<string, FakeAssetHandle> _handles = new Dictionary<string, FakeAssetHandle>();

            public void Add(string key, GameObject prefab) => _prefabs[key] = prefab;

            public FakeAssetHandle AssetHandle(string key) => _handles[key];

            public UniTask<IAssetHandle<T>> RequestAsset<T>(string key, CancellationToken cancellationToken = default)
                where T : Object
            {
                if (!_prefabs.TryGetValue(key, out var prefab))
                {
                    throw new InvalidOperationException($"No fake prefab registered for '{key}'");
                }
                if (!_handles.TryGetValue(key, out var handle))
                {
                    handle = new FakeAssetHandle(prefab);
                    _handles[key] = handle;
                }
                handle.Acquire();
                return UniTask.FromResult<IAssetHandle<T>>((IAssetHandle<T>)(object)handle);
            }

            public T GetAssetImmediate<T>(string key) where T : Object =>
                _prefabs.TryGetValue(key, out var go) ? (T)(Object)go : null;

            public UniTask<IAssetHandle> Preload<T>(IEnumerable<string> keys,
                CancellationToken cancellationToken = default, IProgress<float> progress = null)
                where T : Object => UniTask.FromResult<IAssetHandle>(null);

            public UniTask<IPreloadHandle> PreloadFromLabel<T>(string assetLabel,
                CancellationToken cancellationToken = default, IProgress<float> progress = null)
                where T : Object => UniTask.FromResult<IPreloadHandle>(null);

            public void Release(string key) { }
            public void Release(IEnumerable<string> keys) { }
            public UniTask ReleaseFromLabel(string assetLabel, CancellationToken cancellationToken = default) =>
                UniTask.CompletedTask;
            public bool IsLoaded(string key) => _handles.ContainsKey(key);
            public bool IsLoaded(IEnumerable<string> keys)
            {
                if (keys == null) return false;
                foreach (var k in keys)
                {
                    if (!_handles.ContainsKey(k)) return false;
                }
                return true;
            }
        }

        sealed class FakeAssetHandle : IAssetHandle<GameObject>
        {
            readonly GameObject _prefab;
            int _refs;
            public int DisposeCount { get; private set; }

            public FakeAssetHandle(GameObject prefab) => _prefab = prefab;

            public void Acquire() => _refs++;

            public GameObject Asset => _prefab;

            public void Dispose()
            {
                DisposeCount++;
                _refs--;
            }
        }

        sealed class FakeCameraService : ICameraService
        {
            readonly Camera _camera;
            public FakeCameraService(Camera camera) { _camera = camera; }
            public void Register(CameraId id, Camera camera) { }
            public void Unregister(CameraId id) { }
            public bool IsRegistered(CameraId id) => true;
            public Camera GetCamera(CameraId id) => _camera;
            public bool TryGetCamera(CameraId id, out Camera camera) { camera = _camera; return _camera != null; }
            public Camera GetRequiredCamera(CameraId id) => _camera;
            public Camera GetCameraForRenderer(Renderer renderer) => _camera;
            public IReadOnlyList<CameraId> GetRegisteredIds() => Array.Empty<CameraId>();
        }

        sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }
    }
}
