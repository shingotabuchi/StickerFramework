using System;
using UnityEngine;

namespace StickerFwk.Infrastructure.Haptics
{
    /// <summary>
    /// Unity-serializable mirror of <see cref="StickerFwk.Core.Haptics.HapticCurvePoint"/>.
    /// readonly struct does not serialize cleanly inside a List on a ScriptableObject,
    /// so authored cue sheets store this wrapper and convert on read.
    /// </summary>
    [Serializable]
    public struct SerializableHapticCurvePoint
    {
        public float TimeSeconds;
        [Range(0f, 1f)] public float Value;
    }
}
