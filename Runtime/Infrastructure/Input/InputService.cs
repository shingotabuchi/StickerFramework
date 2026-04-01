using StickerFwk.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace StickerFwk.Infrastructure.Input
{
    public class InputService : IRawInputService
    {
        public InputService()
        {
            EnhancedTouchSupport.Enable();
        }

        public Vector2 PointerPosition
        {
            get
            {
                var pointer = Pointer.current;
                return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            }
        }

        public bool IsPointerDown
        {
            get
            {
                var pointer = Pointer.current;
                return pointer != null && pointer.press.isPressed;
            }
        }

        public bool PointerDownThisFrame
        {
            get
            {
                var pointer = Pointer.current;
                return pointer != null && pointer.press.wasPressedThisFrame;
            }
        }

        public bool PointerUpThisFrame
        {
            get
            {
                var pointer = Pointer.current;
                return pointer != null && pointer.press.wasReleasedThisFrame;
            }
        }

        public int TouchCount
        {
            get
            {
                if (Touch.activeTouches.Count > 0) return Touch.activeTouches.Count;

                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed ? 1 : 0;
            }
        }

        public Vector2 GetTouchPosition(int index)
        {
            if (Touch.activeTouches.Count > index) return Touch.activeTouches[index].screenPosition;

            var pointer = Pointer.current;
            return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
        }

        public float ScrollDelta
        {
            get
            {
                var mouse = Mouse.current;
                return mouse?.scroll.y.ReadValue() ?? 0f;
            }
        }

        public bool IsPointerOverUI
        {
            get
            {
                var eventSystem = EventSystem.current;
                return eventSystem != null && eventSystem.IsPointerOverGameObject();
            }
        }
    }
}