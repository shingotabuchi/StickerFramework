using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Infrastructure.AssetManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime
{
    public class AddressableCacheTests
    {
        [Test]
        public void GetTypedAssetReturnsMatchingAsset()
        {
            var gameObject = new GameObject("asset");
            try
            {
                var handle = new FakeAddressableHandle(gameObject);

                var asset = AddressableCache.GetTypedAsset<GameObject>("asset-key", handle);

                Assert.That(asset, Is.SameAs(gameObject));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GetTypedAssetThrowsForMismatchedCachedType()
        {
            var texture = new Texture2D(1, 1);
            try
            {
                var handle = new FakeAddressableHandle(texture);

                Assert.Throws<InvalidOperationException>(() =>
                    AddressableCache.GetTypedAsset<GameObject>("asset-key", handle));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public async Task RequestAssetReturnsHandleAndReleasesOnDispose()
        {
            var gameObject = new GameObject("asset");
            try
            {
                var loader = new FakeAddressableLoader { NextResult = new FakeAddressableHandle(gameObject) };
                var cache = new AddressableCache(loader);

                var handle = await cache.RequestAsset<GameObject>("k");

                Assert.That(handle.Asset, Is.SameAs(gameObject));
                Assert.That(cache.IsLoaded("k"), Is.True);
                Assert.That(loader.Calls, Is.EqualTo(1));

                handle.Dispose();
                Assert.That(cache.IsLoaded("k"), Is.False);

                cache.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task RequestAssetReleasesRefCountWhenLoaderFails()
        {
            var gameObject = new GameObject("asset");
            try
            {
                var loader = new FakeAddressableLoader();
                var cache = new AddressableCache(loader);

                loader.NextException = new InvalidOperationException("boom");
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    Assert.ThrowsAsync<InvalidOperationException>(async () =>
                        await cache.RequestAsset<GameObject>("k"));
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = false;
                }

                Assert.That(cache.IsLoaded("k"), Is.False);

                // A subsequent successful request must release on a single Dispose, proving no
                // orphan refcount leaked from the failed RequestAsset above.
                loader.NextException = null;
                loader.NextResult = new FakeAddressableHandle(gameObject);
                var handle = await cache.RequestAsset<GameObject>("k");

                Assert.That(handle.Asset, Is.SameAs(gameObject));
                handle.Dispose();
                Assert.That(cache.IsLoaded("k"), Is.False);

                cache.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task RequestAssetDeduplicatesConcurrentRequestsForSameKey()
        {
            var gameObject = new GameObject("asset");
            try
            {
                var loader = new FakeAddressableLoader();
                var cache = new AddressableCache(loader);

                var first = cache.RequestAsset<GameObject>("k").AsTask();
                await loader.WaitForCallAsync(1);

                var second = cache.RequestAsset<GameObject>("k").AsTask();

                loader.CompletePending(new FakeAddressableHandle(gameObject));

                var h1 = await first;
                var h2 = await second;

                Assert.That(loader.Calls, Is.EqualTo(1), "concurrent same-key requests must share a single load");
                Assert.That(h1.Asset, Is.SameAs(gameObject));
                Assert.That(h2.Asset, Is.SameAs(gameObject));

                h1.Dispose();
                Assert.That(cache.IsLoaded("k"), Is.True, "still referenced by the second handle");
                h2.Dispose();
                Assert.That(cache.IsLoaded("k"), Is.False);

                cache.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        sealed class FakeAddressableHandle : IAddressableHandle
        {
            public FakeAddressableHandle(Object asset)
            {
                Object = asset;
            }

            public Object Object { get; }
            public IReadOnlyList<Object> Objects => null;
            public bool Succeeded => true;
            public void Release() { }
        }

        sealed class FakeAddressableLoader : IAddressableLoader
        {
            private readonly Queue<UniTaskCompletionSource<IAddressableHandle>> _pending = new();

            public int Calls { get; private set; }
            public IAddressableHandle NextResult { get; set; }
            public Exception NextException { get; set; }

            public UniTask<IAddressableHandle> LoadAsync<T>(
                string key,
                IProgress<float> progress,
                CancellationToken cancellationToken
            ) where T : Object
            {
                Calls++;

                if (NextException != null)
                {
                    return UniTask.FromException<IAddressableHandle>(NextException);
                }

                if (NextResult != null)
                {
                    return UniTask.FromResult(NextResult);
                }

                var tcs = new UniTaskCompletionSource<IAddressableHandle>();
                _pending.Enqueue(tcs);
                return tcs.Task;
            }

            public async Task WaitForCallAsync(int expected)
            {
                while (Calls < expected) await Task.Yield();
            }

            public void CompletePending(IAddressableHandle handle)
            {
                while (_pending.Count > 0) _pending.Dequeue().TrySetResult(handle);
            }
        }
    }
}
