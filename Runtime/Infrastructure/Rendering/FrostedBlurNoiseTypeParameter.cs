using System;
using UnityEngine.Rendering;

namespace StickerFwk.Infrastructure.Rendering
{
    [Serializable]
    public sealed class FrostedBlurNoiseTypeParameter : VolumeParameter<FrostedBlurNoiseType>
    {
        public FrostedBlurNoiseTypeParameter(FrostedBlurNoiseType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
