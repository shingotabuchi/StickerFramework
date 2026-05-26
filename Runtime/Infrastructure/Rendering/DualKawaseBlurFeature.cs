using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class DualKawaseBlurFeature : ScriptableRendererFeature
    {
        private const int MaxIterations = 8;
        private const float DefaultNoiseScale = 80f;
        private const float DefaultNoiseSeed = 0f;
        private const RenderPassEvent DefaultInjectionPoint = RenderPassEvent.AfterRenderingTransparents;

        [SerializeField] private Shader _blurShader;
        [SerializeField] private Shader _noiseShader;

        private Material _material;
        private Material _noiseMaterial;
        private DualKawaseBlurPass _pass;
        private FrostedBlurNoisePass _noiseOnlyPass;
        private FrostedNoiseBakePass _bakePass;
        private CachedBlurBlitPass _cachedBlitPass;
        private RTHandle _cachedBlur;
        private RTHandle _noiseTex;
        private int _noiseTexWidth;
        private int _noiseTexHeight;
        private FrostedBlurNoiseType _bakedNoiseType = FrostedBlurNoiseType.None;
        private float _bakedNoiseScale = DefaultNoiseScale;
        private float _bakedNoiseSeed = DefaultNoiseSeed;
        private Vector2 _bakedAspect;
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

            if (_noiseShader == null)
            {
                _noiseShader = Shader.Find("Hidden/FrostedBlurNoise");
            }

            if (_blurShader == null && _noiseShader == null)
            {
                return;
            }

            if (_blurShader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(_blurShader);
                _pass = new DualKawaseBlurPass(_material, MaxIterations);
            }

            if (_noiseShader != null)
            {
                _noiseMaterial = CoreUtils.CreateEngineMaterial(_noiseShader);
                _noiseOnlyPass = new FrostedBlurNoisePass(_noiseMaterial, 0);
            }
            else if (_material != null)
            {
                _noiseOnlyPass = new FrostedBlurNoisePass(_material, 2);
            }

            // Bake the displacement field with the noise material (pass 1) when
            // available, otherwise fall back to the blur material's bake pass (3).
            if (_noiseMaterial != null)
            {
                _bakePass = new FrostedNoiseBakePass(_noiseMaterial, 1);
            }
            else if (_material != null)
            {
                _bakePass = new FrostedNoiseBakePass(_material, 3);
            }

            _cachedBlitPass = new CachedBlurBlitPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null && _noiseOnlyPass == null)
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

            var camDesc = renderingData.cameraData.cameraTargetDescriptor;

            if (!blurActive)
            {
                if (_noiseOnlyPass == null)
                {
                    return;
                }

                if (!EnsureNoiseBake(renderer, injectionPoint, camDesc.width, camDesc.height, noiseType, noiseScale, noiseSeed))
                {
                    return;
                }

                _noiseOnlyPass.renderPassEvent = injectionPoint;
                _noiseOnlyPass.Setup(noiseStrength, _noiseTex);
                renderer.EnqueuePass(_noiseOnlyPass);
                return;
            }

            if (_pass == null || _material == null)
            {
                return;
            }

            var desc = camDesc;

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

            var noiseReady = noiseActive
                && EnsureNoiseBake(renderer, injectionPoint, desc.width, desc.height, noiseType, noiseScale, noiseSeed);

            _pass.renderPassEvent = injectionPoint;
            _pass.Setup(
                blur.iterations.value,
                blur.offset.value * blur.intensity.value,
                blur.downsample.value,
                noiseReady ? noiseStrength : 0f,
                noiseReady ? _noiseTex : null,
                isManual ? _cachedBlur : null);

            renderer.EnqueuePass(_pass);
        }

        private bool EnsureNoiseBake(ScriptableRenderer renderer, RenderPassEvent injectionPoint,
            int width, int height, FrostedBlurNoiseType noiseType, float noiseScale, float noiseSeed)
        {
            if (_bakePass == null)
            {
                return false;
            }

            var reallocated = EnsureNoiseTexture(width, height);
            if (_noiseTex == null)
            {
                return false;
            }

            var minDim = Mathf.Min(width, height);
            var aspect = new Vector2(width / (float)minDim, height / (float)minDim);

            var fresh = !reallocated
                && _bakedNoiseType == noiseType
                && Mathf.Approximately(_bakedNoiseScale, noiseScale)
                && Mathf.Approximately(_bakedNoiseSeed, noiseSeed)
                && _bakedAspect == aspect;

            if (!fresh)
            {
                _bakePass.renderPassEvent = injectionPoint;
                _bakePass.Setup(_noiseTex, noiseType, noiseScale, noiseSeed, aspect);
                renderer.EnqueuePass(_bakePass);

                _bakedNoiseType = noiseType;
                _bakedNoiseScale = noiseScale;
                _bakedNoiseSeed = noiseSeed;
                _bakedAspect = aspect;
            }

            return true;
        }

        private bool EnsureNoiseTexture(int width, int height)
        {
            if (_noiseTex != null && _noiseTexWidth == width && _noiseTexHeight == height)
            {
                return false;
            }

            _noiseTex?.Release();
            _noiseTex = RTHandles.Alloc(
                width, height,
                colorFormat: GraphicsFormat.R8G8_UNorm,
                name: "_FrostedNoiseTex");
            _noiseTexWidth = width;
            _noiseTexHeight = height;
            return true;
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

            if (_noiseMaterial != null)
            {
                CoreUtils.Destroy(_noiseMaterial);
            }

            _cachedBlur?.Release();
            _cachedBlur = null;
            _noiseTex?.Release();
            _noiseTex = null;
            _noiseTexWidth = 0;
            _noiseTexHeight = 0;
            _bakedNoiseType = FrostedBlurNoiseType.None;
            _bakedNoiseScale = DefaultNoiseScale;
            _bakedNoiseSeed = DefaultNoiseSeed;
            _bakedAspect = Vector2.zero;
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
