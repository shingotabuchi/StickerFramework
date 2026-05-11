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
using VContainer;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime
{
    // These tests cover the threading contract of UIService:
    //   * Per-layer SemaphoreSlim serializes Push/Pop/Replace on a single layer.
    //   * Different layers run in parallel.
    //   * Replace is atomic (the slot between its internal Pop and Push cannot be hijacked).
    //   * Dispose mid-push cancels in-flight ops cleanly with no leaked handles/instances.
    //   * Pop<T> rescans the layer stack after acquiring the layer lock so that ops which
    //     mutated the stack while Pop<T> was queued are observed correctly.
    public class UIServiceConcurrencyTests
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
        public async Task TwoPushesOnSameLayerSerialize()
        {
            var prefabA = MakePrefab<TestWindowViewA>(UILayer.UI);
            var prefabB = MakePrefab<TestWindowViewB>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefabA);
            requester.Add("Views/TestWindowViewB.prefab", prefabB);
            var service = NewService(requester);

            var aShow = new ControllableTransition("a-show");
            var bShow = new ControllableTransition("b-show");

            var pushA = service.Push<TestWindowViewA>(options: NewOptions(aShow)).AsTask();
            var pushB = service.Push<TestWindowViewB>(options: NewOptions(bShow)).AsTask();

            // Wait deterministically for the first push to reach its show transition,
            // then yield once so the second push can observe the layer lock and queue.
            await aShow.WaitForPlayAsync();
            await YieldOnce();

            Assert.That(aShow.PlayInvocations, Is.EqualTo(1), "first push should be running its show transition");
            Assert.That(bShow.PlayInvocations, Is.EqualTo(0), "second push must wait on the layer lock");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));

            aShow.Complete();
            await pushA;

            await bShow.WaitForPlayAsync();
            Assert.That(bShow.PlayInvocations, Is.EqualTo(1), "second push should run after first releases the layer lock");

            bShow.Complete();
            await pushB;

            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(2));
        }

        [Test]
        public async Task TwoPushesOnDifferentLayersRunInParallel()
        {
            var prefabUi = MakePrefab<TestWindowViewA>(UILayer.UI);
            var prefabOverlay = MakePrefab<TestWindowViewB>(UILayer.UIOverlay);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefabUi);
            requester.Add("Views/TestWindowViewB.prefab", prefabOverlay);
            var service = NewService(requester);

            var uiShow = new ControllableTransition("ui-show");
            var overlayShow = new ControllableTransition("overlay-show");

            var pushUi = service.Push<TestWindowViewA>(options: NewOptions(uiShow)).AsTask();
            var pushOverlay = service.Push<TestWindowViewB>(options: NewOptions(overlayShow)).AsTask();

            // Both pushes target different layers — they must reach their show transitions
            // concurrently. WhenAll on the gates is the strongest possible "ran in parallel"
            // assertion (a serializing implementation would deadlock here).
            await UniTask.WhenAll(uiShow.WaitForPlayAsync(), overlayShow.WaitForPlayAsync());

            Assert.That(uiShow.PlayInvocations, Is.EqualTo(1));
            Assert.That(overlayShow.PlayInvocations, Is.EqualTo(1),
                "different layers must not block each other");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));
            Assert.That(service.GetStackCount(UILayer.UIOverlay), Is.EqualTo(1));

            uiShow.Complete();
            overlayShow.Complete();
            await UniTask.WhenAll(pushUi.AsUniTask(), pushOverlay.AsUniTask());
        }

        [Test]
        public async Task ReplaceIsAtomicAgainstConcurrentPushOnSameLayer()
        {
            var prefabA = MakePrefab<TestWindowViewA>(UILayer.UI);
            var prefabB = MakePrefab<TestWindowViewB>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefabA);
            requester.Add("Views/TestWindowViewB.prefab", prefabB);
            var service = NewService(requester);

            // Seed: push A and let it complete. Bind a controllable HideTransition on the
            // seed via WindowOptions so Replace's internal Pop is gateable.
            var seedShow = new ControllableTransition("seed-show", autoComplete: true);
            var replaceHide = new ControllableTransition("replace-hide");
            await service.Push<TestWindowViewA>(
                options: NewOptions(seedShow, replaceHide)).AsTask();

            var replaceShow = new ControllableTransition("replace-show");
            var pushShow = new ControllableTransition("push-show");

            var replaceTask = service.Replace<TestWindowViewB>(
                options: NewOptions(replaceShow)).AsTask();
            var pushTask = service.Push<TestWindowViewA>(
                options: NewOptions(pushShow)).AsTask();

            await replaceHide.WaitForPlayAsync();
            await YieldOnce();

            Assert.That(replaceHide.PlayInvocations, Is.EqualTo(1),
                "replace should be running the hide transition for the seed window");
            Assert.That(replaceShow.PlayInvocations, Is.EqualTo(0));
            Assert.That(pushShow.PlayInvocations, Is.EqualTo(0),
                "concurrent push must wait on the layer lock");

            replaceHide.Complete();
            await replaceShow.WaitForPlayAsync();
            await YieldOnce();

            // After hide completes, Replace runs PushLocked which awaits replaceShow.
            Assert.That(replaceShow.PlayInvocations, Is.EqualTo(1));
            Assert.That(pushShow.PlayInvocations, Is.EqualTo(0),
                "the slot between Replace's pop and push must not be hijacked");

            replaceShow.Complete();
            await replaceTask;

            // Now the queued Push should finally run.
            await pushShow.WaitForPlayAsync();
            Assert.That(pushShow.PlayInvocations, Is.EqualTo(1));
            pushShow.Complete();
            await pushTask;

            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(2));
        }

        [Test]
        public async Task DisposeMidPushCancelsCleanly()
        {
            var prefab = MakePrefab<TestWindowViewA>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefab);
            var service = NewService(requester);

            var show = new ControllableTransition("show");
            var pushTask = service.Push<TestWindowViewA>(options: NewOptions(show)).AsTask();

            await show.WaitForPlayAsync();
            Assert.That(show.PlayInvocations, Is.EqualTo(1));
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));

            // Dispose mid-push: cancellation propagates to the show transition via the
            // linked token, the stack is drained (handle.Dispose runs once), and the
            // PushLocked catch path must not double-dispose or touch the disposed
            // layer manager.
            service.Dispose();
            _services.Remove(service);

            Assert.CatchAsync<OperationCanceledException>(async () => await pushTask);

            Assert.That(requester.AssetHandle("Views/TestWindowViewA.prefab").DisposeCount, Is.EqualTo(1),
                "asset handle must be disposed exactly once even after cancel-during-push");

            // After Dispose, any mutating op must throw ObjectDisposedException.
            Assert.CatchAsync<ObjectDisposedException>(async () =>
                await service.Push<TestWindowViewA>(
                    options: NewOptions(new ControllableTransition("post"))).AsTask());
        }

        [Test]
        public async Task PopGenericRescansLayerStackAfterLockHandoff()
        {
            var prefab = MakePrefab<TestWindowViewA>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefab);
            var service = NewService(requester);

            // Seed the stack with two A windows, each gated on its own hide transition.
            // PopLocked keeps a window tracked until its hide transition completes, so
            // Pop<T> can scan an entry that PopAll removes before Pop<T> receives the
            // layer lock.
            var seedShow1 = new ControllableTransition("seed1-show", autoComplete: true);
            var seedHide1 = new ControllableTransition("seed1-hide");
            await service.Push<TestWindowViewA>(
                options: NewOptions(seedShow1, seedHide1)).AsTask();

            var seedShow2 = new ControllableTransition("seed2-show", autoComplete: true);
            var seedHide2 = new ControllableTransition("seed2-hide");
            await service.Push<TestWindowViewA>(
                options: NewOptions(seedShow2, seedHide2)).AsTask();
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(2));

            // PopAll holds the layer lock for the whole drain. Iteration 1 awaits seed2's
            // hide while seed2 remains tracked.
            var popAllTask = service.PopAll(UILayer.UI).AsTask();
            await seedHide2.WaitForPlayAsync();
            Assert.That(seedHide2.PlayInvocations, Is.EqualTo(1),
                "PopAll should be running iteration 1's hide for the top window");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(2),
                "seed2 should remain tracked until its hide transition completes");

            // Pop<T> takes the global lock, scans, finds seed2, then awaits the layer
            // lock — held by PopAll. PopAll will remove seed2 before Pop<T> ever gets
            // the layer lock.
            var popGenericTask = service.Pop<TestWindowViewA>().AsTask();
            await YieldOnce();
            Assert.That(popGenericTask.IsCompleted, Is.False,
                "Pop<T> should be queued on the layer lock");

            // Let iteration 1 finish: seedHide2 completes, loop removes seed2 and
            // continues to iteration 2, then awaits seedHide1 while seed1 remains tracked.
            seedHide2.Complete();
            await seedHide1.WaitForPlayAsync();
            Assert.That(seedHide1.PlayInvocations, Is.EqualTo(1),
                "PopAll iteration 2 should now be hiding seed1");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1),
                "seed1 should remain tracked until its hide transition completes");

            // Finish iteration 2. PopAll exits and releases the layer lock.
            seedHide1.Complete();
            var popAllResult = await popAllTask;
            Assert.That(popAllResult, Is.EqualTo(2));

            // Pop<T> now acquires the layer lock, rescans the (now empty) stack via
            // PopViewLocked, observes that the previously-found target is gone, and
            // returns false instead of operating on stale state from the initial scan.
            var popGenericResult = await popGenericTask;
            Assert.That(popGenericResult, Is.False,
                "Pop<T> must rescan after the layer lock handoff and report 'not found'");
        }

        [Test]
        public async Task PopKeepsWindowTrackedWhenHideTransitionFails()
        {
            var prefab = MakePrefab<TestWindowViewA>(UILayer.UI);
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefab);
            var service = NewService(requester);
            await service.Push<TestWindowViewA>(
                options: NewOptions(new ControllableTransition("show", autoComplete: true), new ThrowingTransition())).AsTask();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await service.Pop(UILayer.UI).AsTask());

            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));
            Assert.That(requester.AssetHandle("Views/TestWindowViewA.prefab").DisposeCount, Is.EqualTo(0),
                "failed hide should leave the handle owned by the tracked window");
        }

        [Test]
        public async Task PushRestoresPrefabAlphaBeforeShowTransition()
        {
            var prefab = MakePrefab<TestWindowViewA>(UILayer.Wipe);
            prefab.GetComponent<CanvasGroup>().alpha = 1f;
            var requester = new FakeAssetRequester();
            requester.Add("Views/TestWindowViewA.prefab", prefab);
            var service = NewService(requester);
            var show = new AlphaObservingTransition();

            var view = await service.Push<TestWindowViewA>(options: NewOptions(show)).AsTask();

            Assert.That(show.AlphaAtPlay, Is.EqualTo(1f));
            Assert.That(view.CanvasGroup.alpha, Is.EqualTo(1f));
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

        WindowOptions NewOptions(ITransition show, ITransition hide = null)
        {
            return new WindowOptions
            {
                ShowTransition = show,
                HideTransition = hide ?? new InstantTransition(),
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

        // Yield once so freshly-scheduled UniTask continuations (e.g. an op that just released
        // a SemaphoreSlim and woke a waiter) get a chance to run before we assert on state.
        // Use this only to "let the scheduler breathe" between deterministic gates — never to
        // wait for an unbounded condition (use the gate's WaitForPlayAsync helper for that).
        static async UniTask YieldOnce() => await UniTask.Yield();

        // ---------- Test doubles ----------

        sealed class TestWindowViewA : WindowView { }
        sealed class TestWindowViewB : WindowView { }

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

        sealed class ControllableTransition : ITransition
        {
            readonly UniTaskCompletionSource _gate = new UniTaskCompletionSource();
            readonly UniTaskCompletionSource _playStarted = new UniTaskCompletionSource();
            readonly bool _autoComplete;
            public string Name { get; }
            public int PlayInvocations { get; private set; }

            public ControllableTransition(string name, bool autoComplete = false)
            {
                Name = name;
                _autoComplete = autoComplete;
                if (autoComplete)
                {
                    _gate.TrySetResult();
                }
            }

            public void Complete() => _gate.TrySetResult();

            // Awaitable that completes the moment Play has been invoked at least once.
            // Use to deterministically observe "the leading op has reached its transition"
            // without polling-with-yields.
            public UniTask WaitForPlayAsync() => _playStarted.Task;

            public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
            {
                PlayInvocations++;
                _playStarted.TrySetResult();
                using (ct.Register(() => _gate.TrySetCanceled()))
                {
                    await _gate.Task;
                }
                ct.ThrowIfCancellationRequested();
                if (view != null && view.CanvasGroup != null)
                {
                    view.CanvasGroup.alpha = isShow ? 1f : 0f;
                }
            }
        }

        sealed class ThrowingTransition : ITransition
        {
            public UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
            {
                return UniTask.FromException(new InvalidOperationException("hide failed"));
            }
        }

        sealed class AlphaObservingTransition : ITransition
        {
            public float AlphaAtPlay { get; private set; }

            public UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
            {
                AlphaAtPlay = view.CanvasGroup.alpha;
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
                where T : UnityEngine.Object
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

            public T GetAssetImmediate<T>(string key) where T : UnityEngine.Object =>
                _prefabs.TryGetValue(key, out var go) ? (T)(UnityEngine.Object)go : null;

            public UniTask<IAssetHandle> Preload<T>(IEnumerable<string> keys,
                CancellationToken cancellationToken = default, IProgress<float> progress = null)
                where T : UnityEngine.Object => UniTask.FromResult<IAssetHandle>(null);

            public UniTask<IPreloadHandle> PreloadFromLabel<T>(string assetLabel,
                CancellationToken cancellationToken = default, IProgress<float> progress = null)
                where T : UnityEngine.Object => UniTask.FromResult<IPreloadHandle>(null);

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

            public FakeAssetHandle(GameObject prefab)
            {
                _prefab = prefab;
            }

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
            public bool TryGetCamera(CameraId id, out Camera camera) { camera = _camera; return _camera != null; }
            public Camera GetRequiredCamera(CameraId id) => _camera;
            public IReadOnlyCollection<CameraId> GetRegisteredIds() => Array.Empty<CameraId>();
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
    }
}
