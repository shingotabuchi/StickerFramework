using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    public static class CameraStackConfigurator
    {
        public static void ConfigureOverlayCamera(
            UnityEngine.Camera camera,
            float depth,
            int cullingMask,
            CameraClearFlags clearFlags = CameraClearFlags.Depth)
        {
            AssertCamera(camera);

            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderType = CameraRenderType.Overlay;
            camera.clearFlags = clearFlags;
            camera.cullingMask = cullingMask;
            camera.depth = depth;
        }

        public static UnityEngine.Camera CreateBaseCamera(
            string name,
            Transform parent,
            Color backgroundColor,
            float depth,
            float orthographicSize,
            int cullingMask)
        {
            var cameraObject = new GameObject(name);
            if (parent != null)
            {
                cameraObject.transform.SetParent(parent, false);
            }

            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.cullingMask = cullingMask;
            camera.depth = depth;
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Base;

            return camera;
        }

        private static void AssertCamera(UnityEngine.Camera camera)
        {
            if (camera == null)
            {
                throw new System.ArgumentNullException(nameof(camera));
            }
        }
    }
}
