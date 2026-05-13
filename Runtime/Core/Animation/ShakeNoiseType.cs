namespace StickerFwk.Core
{
    /// <summary>
    /// Noise source used by shake animation helpers.
    /// </summary>
    public enum ShakeNoiseType
    {
        /// <summary>
        /// Produces unrelated random offsets on every sample.
        /// </summary>
        Random = 0,

        /// <summary>
        /// Produces smooth deterministic offsets over time.
        /// </summary>
        Perlin = 1
    }
}
