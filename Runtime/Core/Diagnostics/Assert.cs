using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StickerFwk.Core
{
    /// <summary>
    /// Lightweight Debug.Assert wrapper.
    /// All calls are stripped by the compiler when STICKER_ASSERTIONS is not defined.
    /// To enable: Player Settings → Scripting Define Symbols → add STICKER_ASSERTIONS
    /// </summary>
    public static class Assert
    {
        [Conditional("STICKER_ASSERTIONS")]
        public static void That(bool condition, string message)
        {
            Debug.Assert(condition, message);
        }

        [Conditional("STICKER_ASSERTIONS")]
        public static void That(bool condition, string tag, string message)
        {
            Debug.Assert(condition, $"[{tag}] {message}");
        }

        [Conditional("STICKER_ASSERTIONS")]
        public static void That(bool condition, string message, Object context)
        {
            Debug.Assert(condition, message, context);
        }

        [Conditional("STICKER_ASSERTIONS")]
        public static void That(bool condition, string tag, string message, Object context)
        {
            Debug.Assert(condition, $"[{tag}] {message}", context);
        }
    }
}
