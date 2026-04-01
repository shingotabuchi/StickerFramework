using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StickerFwk.Core.AssetManagement
{
    public interface IAssetHandle<T> : IDisposable where T : Object
    {
        T Asset { get; }
    }

    public interface IAssetHandle : IDisposable
    {
    }

    public interface IPreloadHandle : IDisposable
    {
        IList<string> Keys { get; }
    }

    public interface IAssetRequester
    {
        UniTask<IAssetHandle<T>> RequestAsset<T>(
            string key,
            CancellationToken cancellationToken = default
        ) where T : Object;

        T GetAssetImmediate<T>(string key) where T : Object;

        UniTask<IAssetHandle> Preload<T>(
            IEnumerable<string> keys,
            CancellationToken cancellationToken = default,
            IProgress<float> progress = null
        ) where T : Object;

        UniTask<IPreloadHandle> PreloadFromLabel<T>(
            string assetLabel,
            CancellationToken cancellationToken = default,
            IProgress<float> progress = null
        ) where T : Object;

        void Release(IEnumerable<string> keys);

        UniTask ReleaseFromLabel(string assetLabel, CancellationToken cancellationToken = default);

        bool IsLoaded(string key);

        bool IsLoaded(IEnumerable<string> keys);
    }
}