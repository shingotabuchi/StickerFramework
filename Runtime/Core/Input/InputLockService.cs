using System;
using MessagePipe;

namespace StickerFwk.Core
{
    public class InputLockService : IInputLockService
    {
        readonly IPublisher<InputLockChangedEvent> _publisher;
        int _lockCount;

        public bool IsLocked => _lockCount > 0;

        public InputLockService(IPublisher<InputLockChangedEvent> publisher)
        {
            _publisher = publisher;
        }

        public IDisposable Lock()
        {
            _lockCount++;

            if (_lockCount == 1)
            {
                _publisher.Publish(new InputLockChangedEvent(true));
            }

            return new LockHandle(this);
        }

        void Unlock()
        {
            if (_lockCount == 0)
            {
                return;
            }

            _lockCount--;
            if (_lockCount == 0)
            {
                _publisher.Publish(new InputLockChangedEvent(false));
            }
        }

        sealed class LockHandle : IDisposable
        {
            InputLockService _owner;

            public LockHandle(InputLockService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.Unlock();
                _owner = null;
            }
        }
    }
}
