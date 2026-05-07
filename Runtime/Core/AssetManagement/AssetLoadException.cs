using System;
using System.Runtime.Serialization;

namespace StickerFwk.Core.AssetManagement
{
    /// <summary>
    /// Thrown when an addressable asset fails to load. Carries the offending key so callers
    /// can branch on it programmatically instead of parsing message strings.
    /// </summary>
    [Serializable]
    public class AssetLoadException : Exception
    {
        private const string KeySerializationName = "AssetLoadException.Key";

        public string Key { get; }

        public AssetLoadException()
        {
        }

        public AssetLoadException(string message)
            : base(message)
        {
        }

        public AssetLoadException(string message, Exception inner)
            : base(message, inner)
        {
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

        public static AssetLoadException ForKey(string key)
            => new AssetLoadException(key, $"Failed to load asset of key '{key}'.");

        protected AssetLoadException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Key = info.GetString(KeySerializationName);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            base.GetObjectData(info, context);
            info.AddValue(KeySerializationName, Key);
        }
    }
}
