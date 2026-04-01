using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Infrastructure.Input
{
    public class LockingInputService : IInputService
    {
        private readonly IRawInputService _inner;
        private readonly IInputLockService _inputLockService;

        public LockingInputService(IRawInputService inner, IInputLockService inputLockService)
        {
            _inner = inner;
            _inputLockService = inputLockService;
        }

        public Vector2 PointerPosition => _inner.PointerPosition;
        public bool IsPointerDown => !_inputLockService.IsLocked && _inner.IsPointerDown;
        public bool PointerDownThisFrame => !_inputLockService.IsLocked && _inner.PointerDownThisFrame;
        public bool PointerUpThisFrame => !_inputLockService.IsLocked && _inner.PointerUpThisFrame;
        public int TouchCount => _inputLockService.IsLocked ? 0 : _inner.TouchCount;

        public Vector2 GetTouchPosition(int index)
        {
            return _inner.GetTouchPosition(index);
        }

        public float ScrollDelta => _inputLockService.IsLocked ? 0f : _inner.ScrollDelta;
        public bool IsPointerOverUI => _inner.IsPointerOverUI;
    }
}