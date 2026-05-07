using System;

namespace StickerFwk.Core.AssetManagement
{
    /// <summary>
    /// Thrown when an addressable asset fails to load. Carries the offending key so callers
    /// can branch on it programmatically instead of parsing message strings.
    /// </summary>
    public class AssetLoadException : Exception
    {
        public string Key { get; }

        public AssetLoadException(string key)
            : base($"Failed to load asset of key '{key}'.")
        {
            Key = key;
        }

        public AssetLoadException(string key, string message)
            : base(message)
        {
            Key = key;
        }

        public AssetLoadException(string key, string message, Exception inner)
            : base(message, inner)
        {
            Key = key;
        }
    }
}
