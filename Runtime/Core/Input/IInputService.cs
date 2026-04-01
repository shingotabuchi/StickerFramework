using UnityEngine;

namespace StickerFwk.Core
{
    public interface IInputService
    {
        Vector2 PointerPosition { get; }
        bool IsPointerDown { get; }
        bool PointerDownThisFrame { get; }
        bool PointerUpThisFrame { get; }
        int TouchCount { get; }
        Vector2 GetTouchPosition(int index);
        float ScrollDelta { get; }
        bool IsPointerOverUI { get; }
    }
}
