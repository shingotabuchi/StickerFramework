using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class DualKawaseBlurFeature : ScriptableRendererFeature
    {
        private const int MaxIterations = 8;

        [SerializeField] private Shader _blurShader;

        private Material _material;
        private DualKawaseBlurPass _pass;
        private CachedBlurBlitPass _cachedBlitPass;
        private RTHandle _cachedBlur;
        private int _cachedWidth;
        private int _cachedHeight;
        private GraphicsFormat _cachedFormat;
        private int _cachedCacheVersion = -1;
        private BlurVolume _cachedBlurSource;
        private bool _cacheReady;

        public override void Create()
        {
            if (_blurShader == null)
            {
                _blurShader = Shader.Find("Hidden/DualKawaseBlur");
            }

            if (_blurShader == null)
            {
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_blurShader);
            _pass = new DualKawaseBlurPass(_material, MaxIterations);
            _cachedBlitPass = new CachedBlurBlitPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
            {
                return;
            }

            var stack = VolumeManager.instance.stack;
            var blur = stack.GetComponent<BlurVolume>();

            if (blur == null || !blur.IsActive())
            {
                return;
            }

            var isManual = blur.manualUpdate.value;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            var cacheVersion = blur.CacheVersion;

            var hasCacheMatch = _cacheReady
                && _cachedBlur != null
                && _cachedWidth == desc.width
                && _cachedHeight == desc.height
                && _cachedFormat == desc.graphicsFormat
                && _cachedCacheVersion == cacheVersion
                && _cachedBlurSource == blur;

            if (isManual && hasCacheMatch)
            {
                _cachedBlitPass.renderPassEvent = blur.injectionPoint.value;
                _cachedBlitPass.Setup(_cachedBlur);
                renderer.EnqueuePass(_cachedBlitPass);
                return;
            }

            if (isManual)
            {
                EnsureCacheTexture(desc.width, desc.height, desc.graphicsFormat);
                _cacheReady = true;
                _cachedCacheVersion = cacheVersion;
                _cachedBlurSource = blur;
            }

            _pass.renderPassEvent = blur.injectionPoint.value;
            _pass.Setup(
                blur.intensity.value,
                blur.iterations.value,
                blur.offset.value,
                blur.downsample.value,
                isManual ? _cachedBlur : null);

            renderer.EnqueuePass(_pass);
        }

        private void EnsureCacheTexture(int width, int height, GraphicsFormat format)
        {
            if (_cachedBlur != null && _cachedWidth == width && _cachedHeight == height && _cachedFormat == format)
            {
                return;
            }

            _cachedBlur?.Release();
            _cachedBlur = RTHandles.Alloc(
                width, height,
                colorFormat: format,
                name: "_BlurCache");
            _cachedWidth = width;
            _cachedHeight = height;
            _cachedFormat = format;
        }

        protected override void Dispose(bool disposing)
        {
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
            }

            _cachedBlur?.Release();
            _cachedBlur = null;
            _cachedCacheVersion = -1;
            _cachedBlurSource = null;
            _cacheReady = false;
        }
    }
}
