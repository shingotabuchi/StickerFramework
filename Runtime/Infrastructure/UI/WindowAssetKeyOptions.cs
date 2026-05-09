namespace StickerFwk.Infrastructure.UI
{
    /// <summary>
    /// Configures how <see cref="UIService"/> builds Addressables keys for window prefabs.
    /// The default produces keys of the form <c>"Views/{TypeName}.prefab"</c>. Projects that
    /// store window prefabs under a different Addressables root (e.g. the full project asset
    /// path <c>"Assets/AddressableResources/Views/..."</c>) register a custom instance to
    /// have <see cref="Prefix"/> prepended to every generated key.
    /// </summary>
    public sealed class WindowAssetKeyOptions
    {
        /// <summary>
        /// String prepended to every generated window key. Empty by default.
        /// Set to e.g. <c>"Assets/AddressableResources/"</c> to match Addressables groups
        /// that index entries by full asset path.
        /// </summary>
        public string Prefix { get; set; } = string.Empty;
    }
}
