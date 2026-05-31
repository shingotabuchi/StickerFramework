using System;
using StickerFwk.Core.UI;

namespace StickerFwk.Infrastructure.UI
{
    internal class WindowHandle
    {
        public string Key { get; }
        public WindowView View { get; }
        public UILayer Layer { get; }
        public IDisposable AssetHandle { get; }
        public ITransition HideTransition { get; }
        public float TransitionDuration { get; }

        public WindowHandle(
            string key,
            WindowView view,
            UILayer layer,
            IDisposable assetHandle,
            ITransition hideTransition,
            float transitionDuration)
        {
            Key = key;
            View = view;
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

            if (View != null)
            {
                UnityEngine.Object.Destroy(View.gameObject);
            }
        }
    }
}
