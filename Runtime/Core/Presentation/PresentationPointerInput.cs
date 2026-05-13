using System;
using UnityEngine;

namespace StickerFwk.Core.Presentation
{
    /// <summary>
    /// Small pointer abstraction for presentation flows that need fresh press/release semantics.
    /// </summary>
    public readonly struct PresentationPointerInput
    {
        readonly Func<bool> _isPressed;
        readonly Func<bool> _pressedThisFrame;

        public PresentationPointerInput(Func<bool> isPressed, Func<bool> pressedThisFrame)
        {
            _isPressed = isPressed ?? throw new ArgumentNullException(nameof(isPressed));
            _pressedThisFrame = pressedThisFrame ?? throw new ArgumentNullException(nameof(pressedThisFrame));
        }

        /// <summary>
        /// Pointer source backed by UnityEngine.Input mouse button 0 and first touch.
        /// </summary>
        public static PresentationPointerInput UnityDefault { get; } = new(
            IsUnityPointerPressed,
            WasUnityPointerPressedThisFrame);

        /// <summary>
        /// Pointer source backed by a Sticker input service.
        /// </summary>
        public static PresentationPointerInput FromInputService(IInputService inputService)
        {
            if (inputService == null)
            {
                throw new ArgumentNullException(nameof(inputService));
            }

            return new PresentationPointerInput(
                () => inputService.IsPointerDown || inputService.TouchCount > 0,
                () => inputService.PointerDownThisFrame);
        }

        public bool IsPressed()
        {
            return _isPressed();
        }

        public bool WasPressedThisFrame()
        {
            return _pressedThisFrame();
        }

        static bool WasUnityPointerPressedThisFrame()
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    return true;
                }
            }

            return Input.GetMouseButtonDown(0);
        }

        static bool IsUnityPointerPressed()
        {
            return Input.touchCount > 0 || Input.GetMouseButton(0);
        }
    }
}
