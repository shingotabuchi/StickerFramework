using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Custom/Frosted Blur Noise")]
    public sealed class FrostedBlurNoiseVolume : VolumeComponent, IPostProcessComponent
    {
        public FrostedBlurNoiseTypeParameter type = new FrostedBlurNoiseTypeParameter(FrostedBlurNoiseType.None);
        public NoInterpClampedFloatParameter strength = new NoInterpClampedFloatParameter(0f, 0f, 32f);
        public NoInterpClampedFloatParameter scale = new NoInterpClampedFloatParameter(80f, 1f, 512f);
        public NoInterpFloatParameter seed = new NoInterpFloatParameter(0f);

        public bool IsActive()
        {
            return type.value != FrostedBlurNoiseType.None && strength.value > 0f;
        }
    }
}
