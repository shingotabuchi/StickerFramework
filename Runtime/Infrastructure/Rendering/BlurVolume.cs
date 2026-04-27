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

    [Serializable]
    public sealed class RenderPassEventParameter : VolumeParameter<RenderPassEvent>
    {
        public RenderPassEventParameter(RenderPassEvent value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
