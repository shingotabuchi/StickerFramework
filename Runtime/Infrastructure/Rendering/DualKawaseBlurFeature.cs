using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class DualKawaseBlurFeature : ScriptableRendererFeature
    {
        private const int MaxIterations = 8;
        private const int NoiseOnlyIterations = 1;
        private const int NoiseOnlyDownsample = 0;
        private const float NoiseOnlyOffset = 0f;
        private const float DefaultNoiseScale = 80f;
        private const float DefaultNoiseSeed = 0f;
        private const RenderPassEvent DefaultInjectionPoint = RenderPassEvent.AfterRenderingTransparents;

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
        private FrostedBlurNoiseVolume _cachedNoiseSource;
        private FrostedBlurNoiseType _cachedNoiseType = FrostedBlurNoiseType.None;
        private float _cachedNoiseStrength;
        private float _cachedNoiseScale = DefaultNoiseScale;
        private float _cachedNoiseSeed = DefaultNoiseSeed;
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

            if (!renderingData.cameraData.postProcessEnabled)
            {
                return;
            }

            var stack = VolumeManager.instance.stack;
            var blur = stack.GetComponent<BlurVolume>();
            var noise = stack.GetComponent<FrostedBlurNoiseVolume>();
            var blurActive = blur != null && blur.IsActive();
            var noiseActive = noise != null && noise.IsActive();

            if (!blurActive && !noiseActive)
            {
                return;
            }

            var noiseType = noiseActive ? noise.type.value : FrostedBlurNoiseType.None;
            var noiseStrength = noiseActive ? noise.strength.value : 0f;
            var noiseScale = noiseActive ? noise.scale.value : DefaultNoiseScale;
            var noiseSeed = noiseActive ? noise.seed.value : DefaultNoiseSeed;
            var isManual = blurActive && blur.manualUpdate.value;
            var injectionPoint = blurActive ? blur.injectionPoint.value : DefaultInjectionPoint;
            var cacheVersion = blurActive ? blur.CacheVersion : -1;
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            var hasCacheMatch = _cacheReady
                && _cachedBlur != null
                && _cachedWidth == desc.width
                && _cachedHeight == desc.height
                && _cachedFormat == desc.graphicsFormat
                && _cachedCacheVersion == cacheVersion
                && _cachedBlurSource == blur
                && _cachedNoiseSource == noise
                && _cachedNoiseType == noiseType
                && Mathf.Approximately(_cachedNoiseStrength, noiseStrength)
                && Mathf.Approximately(_cachedNoiseScale, noiseScale)
                && Mathf.Approximately(_cachedNoiseSeed, noiseSeed);

            if (isManual && hasCacheMatch)
            {
                _cachedBlitPass.renderPassEvent = injectionPoint;
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
                _cachedNoiseSource = noise;
                _cachedNoiseType = noiseType;
                _cachedNoiseStrength = noiseStrength;
                _cachedNoiseScale = noiseScale;
                _cachedNoiseSeed = noiseSeed;
            }

            _pass.renderPassEvent = injectionPoint;
            _pass.Setup(
                blurActive ? blur.iterations.value : NoiseOnlyIterations,
                blurActive ? blur.offset.value * blur.intensity.value : NoiseOnlyOffset,
                blurActive ? blur.downsample.value : NoiseOnlyDownsample,
                noiseType,
                noiseStrength,
                noiseScale,
                noiseSeed,
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
            _cachedNoiseSource = null;
            _cachedNoiseType = FrostedBlurNoiseType.None;
            _cachedNoiseStrength = 0f;
            _cachedNoiseScale = DefaultNoiseScale;
            _cachedNoiseSeed = DefaultNoiseSeed;
            _cacheReady = false;
        }
    }
}
