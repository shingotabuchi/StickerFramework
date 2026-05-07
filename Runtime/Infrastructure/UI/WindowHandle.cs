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
        public ITransition HideTransition { get; }
        public float TransitionDuration { get; }

        public WindowHandle(
            string key,
            WindowView view,
            GameObject blocker,
            UILayer layer,
            IDisposable assetHandle,
            ITransition hideTransition,
            float transitionDuration)
        {
            Key = key;
            View = view;
            Blocker = blocker;
            Layer = layer;
            AssetHandle = assetHandle;
            HideTransition = hideTransition;
            TransitionDuration = transitionDuration;
        }

        public void Dispose()
        {
            if (View != null)
            {
                View.OnDispose();
            }

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

