using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    internal sealed class WindowAssetResolver
    {
        readonly IAssetRequester _assetRequester;

        public WindowAssetResolver(IAssetRequester assetRequester)
        {
            _assetRequester = assetRequester;
        }

        public async UniTask<WindowAsset<T>> Load<T>(string key, CancellationToken ct) where T : WindowView
        {
            var assetHandle = await _assetRequester.RequestAsset<GameObject>(key, ct);
            var prefab = assetHandle.Asset;
            if (prefab == null)
            {
                assetHandle.Dispose();
                throw new InvalidOperationException($"Addressable window asset '{key}' resolved to null.");
            }

            var prefabWindow = prefab.GetComponent<T>();
            if (prefabWindow == null)
            {
                assetHandle.Dispose();
                throw new InvalidOperationException(
                    $"Prefab '{key}' must have a {typeof(T).Name} component on its root GameObject.");
            }

            return new WindowAsset<T>(key, assetHandle, prefab, prefabWindow);
        }
    }
}
