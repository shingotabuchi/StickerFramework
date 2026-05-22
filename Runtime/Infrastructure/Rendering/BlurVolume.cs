using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Custom/Dual Kawase Blur")]
    public sealed class BlurVolume : VolumeComponent, IPostProcessComponent
    {
        private static int s_nextCacheVersion;

        public BoolParameter enabled = new BoolParameter(false, overrideState: true);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public NoInterpClampedIntParameter iterations = new NoInterpClampedIntParameter(4, 1, 8);
        public NoInterpClampedFloatParameter offset = new NoInterpClampedFloatParameter(1.5f, 0f, 4f);
        public NoInterpClampedIntParameter downsample = new NoInterpClampedIntParameter(1, 0, 4);
        public FrostedBlurNoiseTypeParameter noiseType = new FrostedBlurNoiseTypeParameter(FrostedBlurNoiseType.None);
        public NoInterpClampedFloatParameter noiseStrength = new NoInterpClampedFloatParameter(0f, 0f, 32f);
        public NoInterpClampedFloatParameter noiseScale = new NoInterpClampedFloatParameter(80f, 1f, 512f);
        public NoInterpFloatParameter noiseSeed = new NoInterpFloatParameter(0f);
        public NoInterpClampedFloatParameter reflectionStrength = new NoInterpClampedFloatParameter(0f, 0f, 2f);
        public NoInterpClampedFloatParameter reflectionRoughness = new NoInterpClampedFloatParameter(0.35f, 0.02f, 1f);
        public NoInterpClampedFloatParameter reflectionNormalStrength = new NoInterpClampedFloatParameter(1f, 0f, 8f);
        public NoInterpVector3Parameter reflectionLightDirection = new NoInterpVector3Parameter(new Vector3(-0.35f, 0.55f, 0.75f));
        public RenderPassEventParameter injectionPoint = new RenderPassEventParameter(RenderPassEvent.AfterRenderingTransparents);
        public BoolParameter manualUpdate = new BoolParameter(false, overrideState: true);
        [HideInInspector] public NoInterpIntParameter cacheVersion = new NoInterpIntParameter(0, overrideState: true);

        public int CacheVersion => cacheVersion.value;

        public new void SetDirty()
        {
            cacheVersion.Override(++s_nextCacheVersion);
        }

        public bool IsActive()
        {
            return enabled.value && intensity.value > 0f && iterations.value > 0;
        }
    }

    public enum FrostedBlurNoiseType
    {
        None = 0,
        Value = 1,
        Perlin = 2,
        FbmValue = 3,
        FbmPerlin = 4
    }

    [Serializable]
    public sealed class FrostedBlurNoiseTypeParameter : VolumeParameter<FrostedBlurNoiseType>
    {
        public FrostedBlurNoiseTypeParameter(FrostedBlurNoiseType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class NoInterpVector3Parameter : VolumeParameter<Vector3>
    {
        public NoInterpVector3Parameter(Vector3 value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class RenderPassEventParameter : VolumeParameter<RenderPassEvent>
    {
        public RenderPassEventParameter(RenderPassEvent value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
