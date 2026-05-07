using System;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    internal sealed class WindowAsset<T> : IDisposable where T : WindowView
    {
        public WindowAsset(string key, IAssetHandle<GameObject> assetHandle, GameObject prefab, T prefabWindow)
        {
            Key = key;
            AssetHandle = assetHandle;
            Prefab = prefab;
            PrefabWindow = prefabWindow;
        }

        public string Key { get; }
        public IAssetHandle<GameObject> AssetHandle { get; }
        public GameObject Prefab { get; }
        public T PrefabWindow { get; }

        public void Dispose()
        {
            AssetHandle?.Dispose();
        }
    }
}
