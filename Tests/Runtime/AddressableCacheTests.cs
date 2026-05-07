using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickerFwk.Infrastructure.AssetManagement;
using UnityEngine;
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
    }
}
