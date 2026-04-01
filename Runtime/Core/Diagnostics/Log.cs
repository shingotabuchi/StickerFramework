using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StickerFwk.Core
{
    /// <summary>
    /// Lightweight Debug.Log wrapper.
    /// Info and Warn calls are stripped by the compiler when STICKER_LOGS is not defined.
    /// Error calls are always active.
    ///
    /// To enable: Player Settings → Scripting Define Symbols → add STICKER_LOGS
    /// </summary>
    public static class Log
    {
        [Conditional("STICKER_LOGS")]
        public static void Info(string message)
        {
            Debug.Log(message);
        }

        [Conditional("STICKER_LOGS")]
        public static void Info(string tag, string message)
        {
            Debug.Log($"[{tag}] {message}");
        }

        [Conditional("STICKER_LOGS")]
        public static void Warning(string message)
        {
            Debug.LogWarning(message);
        }

        [Conditional("STICKER_LOGS")]
        public static void Warning(string tag, string message)
        {
            Debug.LogWarning($"[{tag}] {message}");
        }

        public static void Error(string message)
        {
            Debug.LogError(message);
        }

        public static void Error(string tag, string message)
        {
            Debug.LogError($"[{tag}] {message}");
        }
    }
}
