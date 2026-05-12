namespace StickerFwk.Core.Initialization
{
    /// <summary>
    /// Ordered phases of root initialization. Within a phase:
    /// <list type="bullet">
    ///   <item><description><b>Bootstrap</b> — sequential, in registration order.</description></item>
    ///   <item><description><b>Load</b> — parallel via <c>UniTask.WhenAll</c>.</description></item>
    ///   <item><description><b>Warmup</b> — sequential, in registration order.</description></item>
    /// </list>
    /// Phases always run in the order declared by this enum.
    /// </summary>
    public enum InitPhase
    {
        /// <summary>Cheap synchronous setup before heavy work (e.g., target frame rate, locale).</summary>
        Bootstrap = 0,

        /// <summary>Heavy I/O run in parallel (e.g., master data, cue sheets, addressables).</summary>
        Load = 1,

        /// <summary>Sequential post-load wiring that uses loaded data (e.g., repository <c>Initialize()</c>).</summary>
        Warmup = 2,
    }
}
