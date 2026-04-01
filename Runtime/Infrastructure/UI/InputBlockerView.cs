using System;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using VContainer;

namespace StickerFwk.Infrastructure.UI
{
    public class InputBlockerView : MonoBehaviour, IDisposable
    {
        [SerializeField] GameObject _blocker;

        IDisposable _subscription;

        [Inject]
        public void Construct(ISubscriber<InputLockChangedEvent> inputLockChangedSubscriber)
        {
            _subscription = inputLockChangedSubscriber.Subscribe(OnInputLockChanged);
        }

        void Awake()
        {
            if (_blocker != null)
            {
                _blocker.SetActive(false);
            }
        }

        void OnInputLockChanged(InputLockChangedEvent e)
        {
            if (_blocker != null)
            {
                _blocker.SetActive(e.IsLocked);
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        void OnDestroy()
        {
            Dispose();
        }
    }
}
