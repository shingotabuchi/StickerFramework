using UnityEngine;

namespace StickerFwk.Core
{
    // Raw hardware input that ignores app-level input locks.
    // Inject this only in exceptional cases where lock bypass is intentional.
    public interface IRawInputService
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
