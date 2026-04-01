using UnityEngine;

namespace StickerFwk.Core
{
    public static class CameraExtensions
    {
        public static void FitToArea(this Camera camera, float width, float height, float safeHeightMultiplier = 1f, bool useSafeArea = true)
        {
            var screenWidth = (float)camera.pixelWidth;
            var screenHeight = (float)camera.pixelHeight;
            var safeWidth = screenWidth;
            var safeHeight = screenHeight;

            if (useSafeArea)
            {
                var safeArea = Screen.safeArea;
                var halfScreenW = screenWidth / 2f;
                var halfScreenH = screenHeight / 2f;
                safeWidth = 2f * Mathf.Min(halfScreenW - safeArea.xMin, safeArea.xMax - halfScreenW);
                safeHeight = 2f * Mathf.Min(halfScreenH - safeArea.yMin, safeArea.yMax - halfScreenH);
            }

            safeHeight *= safeHeightMultiplier;

            camera.orthographicSize = screenHeight / 2f * Mathf.Max(height / safeHeight, width / safeWidth);

            Log.Info(
                $"Camera {camera.name} fit to area: {width}x{height} ({screenWidth}x{screenHeight}) safe: {safeWidth}x{safeHeight}");
        }
    }
}