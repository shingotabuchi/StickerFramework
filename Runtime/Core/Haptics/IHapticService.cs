using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Haptics
{
    public interface IHapticService
    {
        /// <summary>
        /// Master haptic intensity in [0, 1]. Setter clamps to range and persists
        /// the value via PlayerPrefs under key "HapticIntensity". A value of 0
        /// disables all haptic output regardless of pattern.
        /// </summary>
        float Intensity { get; set; }

        /// <summary>
        /// Loads a haptic cue sheet asset by Addressable key. Patterns inside the
        /// sheet become playable by name. Awaitable; cancelled by the supplied
        /// token. First-registered pattern name wins on duplicates (warning logged).
        /// </summary>
        UniTask LoadCueSheetAsync(string addressableKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unloads a previously loaded cue sheet by Addressable key. Patterns
        /// contributed by that key are removed from the playable catalogue and
        /// the asset handle is released. Logs a warning if the key is not loaded.
        /// </summary>
        void UnloadCueSheet(string addressableKey);

        /// <summary>True iff the given Addressable key is currently loaded.</summary>
        bool IsCueSheetLoaded(string addressableKey);

        /// <summary>True iff a pattern with the given name is currently playable.</summary>
        bool HasPattern(string patternName);

        /// <summary>
        /// Plays a single-shot haptic identified by name. Returns immediately;
        /// the actual feedback is delivered asynchronously by the platform backend.
        /// Silent no-op when: intensity is 0, the runtime platform is unsupported,
        /// the pattern is not loaded (warning logged), or the service is in any
        /// state that prevents output. Never throws on missing patterns or
        /// unsupported platforms.
        /// </summary>
        /// <param name="patternName">Name of a currently-loaded pattern.</param>
        /// <param name="intensityScale">Per-call multiplier in [0,1] applied on top of <see cref="Intensity"/>.</param>
        void PlayOneShot(string patternName, float intensityScale = 1.0f);
    }
}
