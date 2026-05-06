using System;
using StickerFwk.Core.UI;
using UnityEngine;

namespace StickerFwk.Infrastructure.UI
{
    internal class WindowHandle
    {
        public string Key { get; }
        public WindowView View { get; }
        public GameObject Blocker { get; }
        public UILayer Layer { get; }
        public IDisposable AssetHandle { get; }
        public IDisposable CameraLease { get; }

        public WindowHandle(string key, WindowView view, GameObject blocker, UILayer layer, IDisposable assetHandle, IDisposable cameraLease)
        {
            Key = key;
            View = view;
            Blocker = blocker;
            Layer = layer;
            AssetHandle = assetHandle;
            CameraLease = cameraLease;
        }

        public void Dispose()
        {
            if (View != null)
            {
                View.OnDispose();
            }

            CameraLease?.Dispose();
            AssetHandle?.Dispose();

            if (Blocker != null)
            {
                UnityEngine.Object.Destroy(Blocker);
            }
            if (View != null)
            {
                UnityEngine.Object.Destroy(View.gameObject);
            }
        }
    }
}

