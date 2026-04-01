using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Infrastructure.UI
{
    public static class InputBlocker
    {
        static readonly Color BlockerColor = new Color(0f, 0f, 0f, 0f);

        public static GameObject Create(Transform parent)
        {
            var blocker = new GameObject("InputBlocker");
            blocker.transform.SetParent(parent, false);

            var rect = blocker.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var image = blocker.AddComponent<Image>();
            image.color = BlockerColor;
            image.raycastTarget = true;

            return blocker;
        }
    }
}
