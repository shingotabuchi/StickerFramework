using System;
using System.Collections.Generic;
using System.Reflection;
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

            // Yield enough times for the first push to reach its show transition. The second
            // push must NOT reach its transition: layer lock is held by the first.
            await PumpAsync();

            Assert.That(aShow.PlayInvocations, Is.EqualTo(1), "first push should be running its show transition");
            Assert.That(bShow.PlayInvocations, Is.EqualTo(0), "second push must wait on the layer lock");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1));

            aShow.Complete();
            await pushA;

            await PumpAsync();
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

            await PumpAsync();

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

            await PumpAsync();

            Assert.That(replaceHide.PlayInvocations, Is.EqualTo(1),
                "replace should be running the hide transition for the seed window");
            Assert.That(replaceShow.PlayInvocations, Is.EqualTo(0));
            Assert.That(pushShow.PlayInvocations, Is.EqualTo(0),
                "concurrent push must wait on the layer lock");

            replaceHide.Complete();
            await PumpAsync();

            // After hide completes, Replace runs PushLocked which awaits replaceShow.
            Assert.That(replaceShow.PlayInvocations, Is.EqualTo(1));
            Assert.That(pushShow.PlayInvocations, Is.EqualTo(0),
                "the slot between Replace's pop and push must not be hijacked");

            replaceShow.Complete();
            await replaceTask;
            await PumpAsync();

            // Now the queued Push should finally run.
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

            await PumpAsync();
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
            // PopLocked removes a window synchronously before awaiting its hide, so to
            // keep the scan target alive at scan time we need an op that holds the layer
            // lock while there are still entries on the stack — PopAll iterates pop+await
            // in one lock hold, so during iteration #1 the second seed entry is still
            // present.
            var seedShow1 = new ControllableTransition("seed1-show", autoComplete: true);
            var seedHide1 = new ControllableTransition("seed1-hide");
            await service.Push<TestWindowViewA>(
                options: NewOptions(seedShow1, seedHide1)).AsTask();

            var seedShow2 = new ControllableTransition("seed2-show", autoComplete: true);
            var seedHide2 = new ControllableTransition("seed2-hide");
            await service.Push<TestWindowViewA>(
                options: NewOptions(seedShow2, seedHide2)).AsTask();
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(2));

            // PopAll holds the layer lock for the whole drain. Iteration 1 pops the top
            // window (seed2) synchronously then awaits seedHide2 — at this point seed1 is
            // still on the stack.
            var popAllTask = service.PopAll(UILayer.UI).AsTask();
            await PumpAsync();
            Assert.That(seedHide2.PlayInvocations, Is.EqualTo(1),
                "PopAll should be running iteration 1's hide for the top window");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(1),
                "iteration 1 has popped seed2; seed1 is still on the stack");

            // Pop<T> takes the global lock, scans, finds seed1 (still on the stack),
            // releases — wait, in this implementation Pop<T> holds the global lock
            // while waiting on the layer lock. The layer lock is held by PopAll, so
            // Pop<T> queues. PopAll iteration 2 will pop seed1 synchronously next.
            var popGenericTask = service.Pop<TestWindowViewA>().AsTask();
            await PumpAsync();
            Assert.That(popGenericTask.IsCompleted, Is.False,
                "Pop<T> should be queued on the layer lock");

            // Let iteration 1 finish: seedHide2 completes, loop continues to iteration 2,
            // pops seed1 synchronously, awaits seedHide1.
            seedHide2.Complete();
            await PumpAsync();
            Assert.That(seedHide1.PlayInvocations, Is.EqualTo(1),
                "PopAll iteration 2 should now be hiding seed1");
            Assert.That(service.GetStackCount(UILayer.UI), Is.EqualTo(0),
                "seed1 has been popped synchronously by iteration 2");

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
            SetPrivateField(view, "_layer", layer);
            return go;
        }

        static void SetPrivateField(object target, string name, object value)
        {
            var t = target.GetType();
            FieldInfo fi = null;
            while (t != null && fi == null)
            {
                fi = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                t = t.BaseType;
            }
            if (fi == null) throw new InvalidOperationException($"field {name} not found");
            fi.SetValue(target, value);
        }

        // Pump UniTask continuations a few times so awaits scheduled by the previous step
        // get a chance to run. UniTask continuations posted via SetResult typically resume
        // synchronously, but anything awaiting layer-lock wait queues needs a yield to
        // observe the lock release.
        static async UniTask PumpAsync()
        {
            for (var i = 0; i < 8; i++)
            {
                await UniTask.Yield();
            }
        }

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

            public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
            {
                PlayInvocations++;
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
            public bool IsLoaded(IEnumerable<string> keys) => false;
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
