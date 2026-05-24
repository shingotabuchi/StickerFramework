using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime
{
    public class UIServicePushExtensionsTests
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
                try { service.Dispose(); } catch { }
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

        [Test]
        public async Task PushWithHandle_ReturnsHandleAndPopIsIdempotent()
        {
            var prefab = MakePrefab<PlainHandleView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/PlainHandleView.prefab", prefab);
            var service = NewService(requester);

            var handle = await service.PushWithHandle<PlainHandleView>(options: AutoCompleteOptions()).AsTask();

            Assert.That(handle.View, Is.Not.Null);
            Assert.That(service.GetWindow<PlainHandleView>(), Is.SameAs(handle.View));
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));

            await handle.PopAsync().AsTask();
            await handle.PopAsync().AsTask();

            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(0));
            Assert.That(requester.AssetHandle("Views/PlainHandleView.prefab").DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PushWithHandleWithArgs_SetsArgsBeforeInitialize()
        {
            var prefab = MakePrefab<ArgsHandleView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/ArgsHandleView.prefab", prefab);
            var service = NewService(requester);

            var handle = await service.PushWithHandle<ArgsHandleView, TestArgs>(
                new TestArgs { Value = 123 }, options: AutoCompleteOptions()).AsTask();

            Assert.That(handle.View.ReceivedArgs.Value, Is.EqualTo(123));
            Assert.That(handle.View.ArgsSetBeforeInitialize, Is.True);
        }

        [Test]
        public async Task PushBelow_PushesLogicalTopAndPlacesBelowCoveringSibling()
        {
            var coveringPrefab = MakePrefab<CoveringView>(UILayer.UI);
            var belowPrefab = MakePrefab<BelowView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/CoveringView.prefab", coveringPrefab);
            requester.Add("Views/BelowView.prefab", belowPrefab);
            var service = NewService(requester);

            var covering = await service.Push<CoveringView>(options: AutoCompleteOptions()).AsTask();
            var handle = await service.PushBelow<BelowView>(covering, options: AutoCompleteOptions()).AsTask();

            Assert.That(service.IsOpen<BelowView>(), Is.True);
            Assert.That(service.GetWindow<BelowView>(), Is.SameAs(handle.View), "new below-pushed view should be logical top");
            Assert.That(handle.View.transform.GetSiblingIndex(), Is.LessThan(covering.transform.GetSiblingIndex()));
        }

        [Test]
        public async Task PushBelow_DifferentParentLogsWarningAndStillPushes()
        {
            var belowPrefab = MakePrefab<BelowView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/BelowView.prefab", belowPrefab);
            var service = NewService(requester);
            var externalGo = new GameObject("ExternalCovering", typeof(RectTransform), typeof(CanvasGroup));
            _spawned.Add(externalGo);
            var externalCovering = externalGo.AddComponent<CoveringView>();

            LogAssert.Expect(LogType.Warning, new Regex(".*PushBelow skipped sibling reorder.*"));

            var handle = await service.PushBelow<BelowView>(externalCovering, options: AutoCompleteOptions()).AsTask();

            Assert.That(handle.View, Is.Not.Null);
            Assert.That(service.IsOpen<BelowView>(), Is.True);
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));
        }

        [Test]
        public async Task PushPrepared_PreparesHiddenAfterInitializeThenShows()
        {
            var prefab = MakePrefab<PreparedView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/PreparedView.prefab", prefab);
            var service = NewService(requester);
            var prepareSawInitialized = false;
            var prepareSawHidden = false;

            var view = await service.PushPrepared<PreparedView>((preparedView, token) =>
            {
                prepareSawInitialized = preparedView.Initialized;
                prepareSawHidden = preparedView.CanvasGroup.alpha == 0f
                    && !preparedView.CanvasGroup.interactable
                    && !preparedView.CanvasGroup.blocksRaycasts;
                preparedView.PreparedValue = 456;
                return UniTask.CompletedTask;
            }, options: AutoCompleteOptions()).AsTask();

            Assert.That(prepareSawInitialized, Is.True);
            Assert.That(prepareSawHidden, Is.True);
            Assert.That(view.PreparedValue, Is.EqualTo(456));
            Assert.That(view.CanvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(service.GetWindow<PreparedView>(), Is.SameAs(view));
        }

        [Test]
        public async Task PushPrepared_CancellationDuringPrepareDestroysInstanceAndRethrows()
        {
            var prefab = MakePrefab<PreparedView>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/PreparedView.prefab", prefab);
            var service = NewService(requester);
            using var cts = new CancellationTokenSource();
            PreparedView captured = null;

            var pushTask = service.PushPrepared<PreparedView>(async (preparedView, token) =>
            {
                captured = preparedView;
                cts.Cancel();
                await UniTask.Yield();
                token.ThrowIfCancellationRequested();
            }, options: AutoCompleteOptions(), ct: cts.Token).AsTask();

            OperationCanceledException caught = null;
            try { await pushTask; }
            catch (OperationCanceledException ex) { caught = ex; }
            Assert.That(caught, Is.Not.Null, "PushPrepared must surface cancellation.");
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(0));
            Assert.That(captured == null || captured.gameObject == null, Is.True,
                "cancelled prepared push should destroy the instantiated GameObject");
            Assert.That(requester.AssetHandle("Views/PreparedView.prefab").DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ScopedUIService_TracksAllPushExtensionVariantsAndPopsOnDispose()
        {
            var objects = new List<GameObject>();
            try
            {
                var first = NewScopedTrackView("first", objects);
                var second = NewScopedTrackView("second", objects);
                var third = NewScopedTrackView("third", objects);
                var fourth = NewScopedTrackView("fourth", objects);
                var inner = new ScopedFakeUIService(first, second, third, fourth);
                var scoped = new ScopedUIService(inner);

                await scoped.PushWithHandle<ScopedTrackView>();
                await scoped.PushWithHandle<ScopedTrackView, TestArgs>(new TestArgs { Value = 1 });
                await scoped.PushBelow<ScopedTrackView>(first);
                await scoped.PushPrepared<ScopedTrackView>((view, token) => UniTask.CompletedTask);

                scoped.Dispose();
                await UniTask.Yield();

                CollectionAssert.AreEqual(new WindowView[] { fourth, third, second, first }, inner.PoppedViews);
            }
            finally
            {
                foreach (var go in objects)
                {
                    if (go != null)
                    {
                        Object.DestroyImmediate(go);
                    }
                }
            }
        }

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
                IsBlocking = false,
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

        static ScopedTrackView NewScopedTrackView(string name, List<GameObject> objects)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            objects.Add(go);
            return go.AddComponent<ScopedTrackView>();
        }

        sealed class TestArgs
        {
            public int Value;
        }

        sealed class PlainHandleView : WindowView { }
        sealed class CoveringView : WindowView { }
        sealed class BelowView : WindowView { }

        sealed class ArgsHandleView : WindowView, IWindowWithArgs<TestArgs>
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

        sealed class PreparedView : WindowView
        {
            public bool Initialized { get; private set; }
            public int PreparedValue { get; set; }

            public override UniTask OnInitialize(CancellationToken ct)
            {
                Initialized = true;
                return UniTask.CompletedTask;
            }
        }

        sealed class ScopedTrackView : WindowView, IWindowWithArgs<TestArgs>
        {
            public void SetArgs(TestArgs args) { }
        }

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
                foreach (var key in keys)
                {
                    if (!_handles.ContainsKey(key)) return false;
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
            public CameraId ActiveBase => default;
            public event Action<ActiveBaseChangedEvent> ActiveBaseChanged { add { } remove { } }
            public void SetDefaultBase(CameraId id) { }
            public IDisposable PushBase(CameraId id) => new NoopDisposable();
            public IDisposable DisableOverlay(CameraId id) => new NoopDisposable();
            sealed class NoopDisposable : IDisposable { public void Dispose() { } }
        }

        sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        sealed class ScopedFakeUIService : IUIService
        {
            readonly Queue<ScopedTrackView> _views;
            public List<WindowView> PoppedViews { get; } = new List<WindowView>();

            public ScopedFakeUIService(params ScopedTrackView[] views)
            {
                _views = new Queue<ScopedTrackView>(views);
            }

            public UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView => UniTask.FromResult((T)(WindowView)_views.Dequeue());

            public UniTask<T> Push<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null,
                CancellationToken ct = default) where T : WindowView, IWindowWithArgs<TArgs> => Push<T>(tag, options, ct);

            public async UniTask<WindowPushHandle<T>> PushWithHandle<T>(string tag = null, WindowOptions options = null,
                CancellationToken ct = default) where T : WindowView
            {
                var view = await Push<T>(tag, options, ct);
                return new WindowPushHandle<T>(view, this);
            }

            public async UniTask<WindowPushHandle<T>> PushWithHandle<T, TArgs>(TArgs args, string tag = null,
                WindowOptions options = null, CancellationToken ct = default) where T : WindowView, IWindowWithArgs<TArgs>
            {
                var view = await Push<T, TArgs>(args, tag, options, ct);
                return new WindowPushHandle<T>(view, this);
            }

            public UniTask<WindowPushHandle<T>> PushBelow<T>(WindowView coveringView, string tag = null,
                WindowOptions options = null, CancellationToken ct = default) where T : WindowView => PushWithHandle<T>(tag, options, ct);

            public async UniTask<T> PushPrepared<T>(Func<T, CancellationToken, UniTask> prepareAsync, string tag = null,
                WindowOptions options = null, CancellationToken ct = default) where T : WindowView
            {
                var view = await Push<T>(tag, options, ct);
                await prepareAsync(view, ct);
                return view;
            }

            public UniTask<bool> Pop(WindowView view, CancellationToken ct = default) => Pop(view, immediate: false, ct);
            public UniTask<bool> Pop(WindowView view, bool immediate, CancellationToken ct = default)
            {
                PoppedViews.Add(view);
                return UniTask.FromResult(true);
            }

            public UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default) => UniTask.FromResult(true);
            public UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView => UniTask.FromResult(true);
            public UniTask<T> Replace<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
                where T : WindowView => Push<T>(tag, options, ct);
            public UniTask<T> Replace<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null,
                CancellationToken ct = default) where T : WindowView, IWindowWithArgs<TArgs> => Push<T>(tag, options, ct);
            public UniTask<int> PopAll(UILayer layer, bool immediate = false, CancellationToken ct = default) => UniTask.FromResult(0);
            public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView => UniTask.CompletedTask;
            public void Unload<T>(string tag = null) where T : WindowView { }
            public bool IsOpen<T>() where T : WindowView => false;
            public T GetWindow<T>() where T : WindowView => null;
            public int GetStackCount(UILayer layer) => 0;
        }
    }
}
