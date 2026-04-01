using System;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Core.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaView : MonoBehaviour
    {
        private RectTransform _rectTransform;
        [Inject] private readonly ISubscriber<ScreenChangedEvent> _subscriber;
        private IDisposable _subscription;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if(_subscriber == null)
            {
                return;
            }
            
            _subscription = _subscriber.Subscribe(_ => Apply());
            Apply();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
        }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
