using System;

namespace StickerFwk.Core.LocalDataSave
{
    public readonly struct LocalDataSaveKey
    {
        public LocalDataSaveKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Local data save key must be non-empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }
}
