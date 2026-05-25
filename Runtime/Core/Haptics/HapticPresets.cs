namespace StickerFwk.Core.Haptics
{
    /// <summary>
    /// Canonical names of the built-in haptic patterns shipped in the framework's default haptic profile.
    /// Use these constants instead of string literals for compile-checked lookups.
    /// </summary>
    public static class HapticPresets
    {
        public const string Selection = "Selection";
        public const string LightImpact = "LightImpact";
        public const string MediumImpact = "MediumImpact";
        public const string HeavyImpact = "HeavyImpact";
        public const string RigidImpact = "RigidImpact";
        public const string SoftImpact = "SoftImpact";
        public const string Success = "Success";
        public const string Warning = "Warning";
        public const string Error = "Error";
    }
}
