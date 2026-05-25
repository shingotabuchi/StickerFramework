using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class ScreenTransitionServiceTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
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
        public void ExecuteAsync_HidesAndDestroysRigWhenActionIsCanceled()
        {
            var prefab = MakePrefab();
            var requester = new FakeAssetRequester();
            requester.Add("Views/ScreenTransitionView.prefab", prefab);
            var service = new ScreenTransitionService(requester, resolver: null, keyOptions: null);
            using var cts = new CancellationTokenSource();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await service.ExecuteAsync(ct =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(ct);
                }, ct: cts.Token).AsTask());

            var rig = prefab.GetComponent<TestTransitionRig>();
            Assert.That(rig.ShowCount, Is.EqualTo(1));
            Assert.That(rig.HideCount, Is.EqualTo(1));
            Assert.That(rig.DestroyedInstanceCount, Is.EqualTo(1));
            Assert.That(service.IsActive, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task ExecuteAsync_ReportsProgressAndCompletionInOrder()
        {
            var prefab = MakePrefab();
            var requester = new FakeAssetRequester();
            requester.Add("Views/ScreenTransitionView_fade.prefab", prefab);
            var service = new ScreenTransitionService(requester, resolver: null, keyOptions: null);
            var completedCount = 0;
            service.TransitionCompleted += () => completedCount++;

            await service.ExecuteAsync((progress, ct) =>
            {
                Assert.That(service.IsActive, Is.True);
                progress.Report(0.5f);
                return UniTask.CompletedTask;
            }, "fade").AsTask();

            var rig = prefab.GetComponent<TestTransitionRig>();
            Assert.That(rig.Events, Is.EqualTo(new[] { "Show", "Progress:0", "Progress:0.5", "Progress:1", "Hide", "Destroy" }));
            Assert.That(completedCount, Is.EqualTo(1));
        }

        GameObject MakePrefab()
        {
            var go = new GameObject("ScreenTransitionView", typeof(TestTransitionRig));
            _spawned.Add(go);
            return go;
        }

        sealed class TestTransitionRig : MonoBehaviour, IScreenTransitionRig
        {
            static TestTransitionRig s_lastPrefabRig;

            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public int DestroyedInstanceCount { get; private set; }
            public List<string> Events { get; } = new List<string>();

            TestTransitionRig _prefabRig;

            void Awake()
            {
                if (name.EndsWith("(Clone)", StringComparison.Ordinal))
                {
                    _prefabRig = s_lastPrefabRig;
                    return;
                }

                s_lastPrefabRig = this;
                _prefabRig = this;
            }

            public UniTask Show(CancellationToken ct)
            {
                _prefabRig.ShowCount++;
                _prefabRig.Events.Add("Show");
                return UniTask.CompletedTask;
            }

            public UniTask Hide(CancellationToken ct)
            {
                _prefabRig.HideCount++;
                _prefabRig.Events.Add("Hide");
                return UniTask.CompletedTask;
            }

            public void SetProgress(float value)
            {
                _prefabRig.Events.Add($"Progress:{value:0.#}");
            }

            void OnDestroy()
            {
                if (_prefabRig != null && _prefabRig != this)
                {
                    _prefabRig.DestroyedInstanceCount++;
                    _prefabRig.Events.Add("Destroy");
                }
            }
        }

        sealed class FakeAssetRequester : IAssetRequester
        {
            readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

            public void Add(string key, GameObject prefab) => _prefabs[key] = prefab;

            public UniTask<IAssetHandle<T>> RequestAsset<T>(string key, CancellationToken cancellationToken = default)
                where T : Object
            {
                if (!_prefabs.TryGetValue(key, out var prefab))
                {
                    throw new InvalidOperationException($"No fake prefab registered for '{key}'");
                }

                return UniTask.FromResult<IAssetHandle<T>>((IAssetHandle<T>)(object)new FakeAssetHandle(prefab));
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
            public bool IsLoaded(string key) => _prefabs.ContainsKey(key);
            public bool IsLoaded(IEnumerable<string> keys) => true;
        }

        sealed class FakeAssetHandle : IAssetHandle<GameObject>
        {
            public FakeAssetHandle(GameObject asset) => Asset = asset;
            public GameObject Asset { get; }
            public void Dispose() { }
        }
    }
}
