using System;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Core.UI
{
    [RequireComponent(typeof(Button))]
    public class CoolButton : MonoBehaviour
    {
        sealed class ClickSubscription : IDisposable
        {
            readonly CoolButton _button;
            readonly Action _listener;
            bool _isDisposed;

            public ClickSubscription(CoolButton button, Action listener)
            {
                _button = button;
                _listener = listener;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _button.RemoveClickListener(_listener);
            }
        }

        Button _button;

        public event Action Clicked;

        void Awake()
        {
            _button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.AddListener(HandleClick);
        }

        void OnDisable()
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(HandleClick);
        }

        public IDisposable AddClickListener(Action listener)
        {
            Clicked += listener;
            return new ClickSubscription(this, listener);
        }

        void RemoveClickListener(Action listener)
        {
            Clicked -= listener;
        }

        void HandleClick()
        {
            Clicked?.Invoke();
        }

        public bool Interactable
        {
            get => _button != null && _button.interactable;
            set
            {
                if (_button != null)
                {
                    _button.interactable = value;
                }
            }
        }
    }
}
