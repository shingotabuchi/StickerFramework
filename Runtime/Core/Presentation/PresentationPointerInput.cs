using System;
using UnityEngine.InputSystem;

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
        /// Pointer source backed by the active Input System pointer.
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
            var pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        static bool IsUnityPointerPressed()
        {
            var pointer = Pointer.current;
            return pointer != null && pointer.press.isPressed;
        }
    }
}
