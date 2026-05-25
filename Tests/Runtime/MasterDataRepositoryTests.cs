using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.MasterData;
using StickerFwk.Infrastructure.MasterData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime
{
    public class MasterDataRepositoryTests
    {
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _assets.Clear();
        }

        [Test]
        public async Task LoadAsync_UsesMasterDataAddressablesLabel()
        {
            var requester = new FakeAssetRequester(Array.Empty<string>());
            var repository = new MasterDataRepository(requester);

            await repository.LoadAsync().AsTask();

            Assert.That(requester.LastPreloadLabel, Is.EqualTo("MasterData"));
            Assert.That(repository.IsLoaded, Is.True);
        }

        [Test]
        public void LoadAsync_InvalidAssetDoesNotPoisonRepository()
        {
            var invalid = CreateAsset<InvalidMasterDataAsset>();
            var requester = new FakeAssetRequester(new[] { "bad" });
            requester.Add("bad", invalid);
            var repository = new MasterDataRepository(requester);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await repository.LoadAsync().AsTask());

            Assert.That(repository.IsLoaded, Is.False);
            Assert.That(requester.PreloadHandle.DisposeCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => repository.GetAll<TestData>());
        }

        [Test]
        public void LoadAsync_DuplicateIdDoesNotPoisonRepository()
        {
            var first = CreateAsset<TestMasterDataAsset>();
            first.SetData(new TestData("same"));
            var second = CreateAsset<TestMasterDataAsset>();
            second.SetData(new TestData("same"));
            var requester = new FakeAssetRequester(new[] { "first", "second" });
            requester.Add("first", first);
            requester.Add("second", second);
            var repository = new MasterDataRepository(requester);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await repository.LoadAsync().AsTask());

            Assert.That(repository.IsLoaded, Is.False);
            Assert.That(requester.PreloadHandle.DisposeCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => repository.Get<TestData>("same"));
        }

        T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        sealed class TestData : IMasterData
        {
            public TestData(string id)
            {
                Id = id;
            }

            public string Id { get; }
            public Type Type => typeof(TestData);
        }

        sealed class TestMasterDataAsset : ScriptableObject, IMasterDataScriptableObject
        {
            IReadOnlyList<IMasterData> _data = Array.Empty<IMasterData>();

            public Type Type => typeof(TestData);
            public IReadOnlyList<IMasterData> Data => _data;

            public void SetData(params TestData[] data)
            {
                _data = data;
            }
        }

        sealed class InvalidMasterDataAsset : ScriptableObject { }

        sealed class FakePreloadHandle : IPreloadHandle
        {
            public FakePreloadHandle(IList<string> keys)
            {
                Keys = keys;
            }

            public IList<string> Keys { get; }
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        sealed class FakeAssetRequester : IAssetRequester
        {
            readonly Dictionary<string, ScriptableObject> _assets = new Dictionary<string, ScriptableObject>();

            public FakeAssetRequester(IList<string> keys)
            {
                PreloadHandle = new FakePreloadHandle(keys);
            }

            public FakePreloadHandle PreloadHandle { get; }
            public string LastPreloadLabel { get; private set; }

            public void Add(string key, ScriptableObject asset)
            {
                _assets[key] = asset;
            }

            public T GetAssetImmediate<T>(string key) where T : Object => (T)(Object)_assets[key];

            public UniTask<IPreloadHandle> PreloadFromLabel<T>(string assetLabel,
                CancellationToken cancellationToken = default, IProgress<float> progress = null) where T : Object
            {
                LastPreloadLabel = assetLabel;
                return UniTask.FromResult<IPreloadHandle>(PreloadHandle);
            }

            public UniTask<IAssetHandle<T>> RequestAsset<T>(string key,
                CancellationToken cancellationToken = default) where T : Object =>
                throw new NotSupportedException();

            public UniTask<IAssetHandle> Preload<T>(IEnumerable<string> keys,
                CancellationToken cancellationToken = default, IProgress<float> progress = null) where T : Object =>
                throw new NotSupportedException();

            public void Release(string key) { }
            public void Release(IEnumerable<string> keys) { }
            public UniTask ReleaseFromLabel(string assetLabel, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public bool IsLoaded(string key) => false;
            public bool IsLoaded(IEnumerable<string> keys) => false;
        }
    }
}
