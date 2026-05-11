using System;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Infrastructure.UI
{
    /// <summary>
    /// Configures the <see cref="Canvas"/> and <see cref="CanvasScaler"/> that
    /// <see cref="UILayerManager"/> creates for each <see cref="UILayer"/>.
    /// Register an instance with the DI container (e.g. via
    /// <c>builder.RegisterInstance(new UICanvasOptions { ... })</c>) to override
    /// the framework defaults. If no instance is registered, the previous
    /// hardcoded values are used (1920×1080 ScaleWithScreenSize, match 0.5).
    /// </summary>
    public sealed class UICanvasOptions
    {
        public CanvasScaler.ScaleMode UiScaleMode { get; set; } = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        public Vector2 ReferenceResolution { get; set; } = new Vector2(1920f, 1080f);
        public CanvasScaler.ScreenMatchMode ScreenMatchMode { get; set; } = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        public float MatchWidthOrHeight { get; set; } = 0.5f;
        public float ReferencePixelsPerUnit { get; set; } = 100f;
        public float ScaleFactor { get; set; } = 1f;
        public float DynamicPixelsPerUnit { get; set; } = 1f;
        public float PlaneDistance { get; set; } = 100f;
        public bool PixelPerfect { get; set; }
        public AdditionalCanvasShaderChannels AdditionalShaderChannels { get; set; } = AdditionalCanvasShaderChannels.None;

        /// <summary>
        /// Optional per-layer hook invoked after the canvas has been created and the
        /// base settings (render mode, camera, sort order) have been applied. Use this
        /// to apply layer-specific tweaks that the simple property bag above cannot
        /// express.
        /// </summary>
        public Action<UILayer, Canvas> ConfigureCanvas { get; set; }

        /// <summary>
        /// Optional per-layer hook invoked after the canvas scaler has been created
        /// and the base properties above have been applied. Use this for layer-specific
        /// overrides (e.g. a different match value on the overlay layer).
        /// </summary>
        public Action<UILayer, CanvasScaler> ConfigureScaler { get; set; }
    }
}
