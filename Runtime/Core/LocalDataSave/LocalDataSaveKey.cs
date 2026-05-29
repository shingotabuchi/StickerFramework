using System;

namespace StickerFwk.Core.LocalDataSave
{
    public readonly struct LocalDataSaveKey : IEquatable<LocalDataSaveKey>
    {
        public string Value { get; }

        public LocalDataSaveKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("LocalDataSaveKey value must not be empty.", nameof(value));
            }

            Value = value;
        }

        public bool Equals(LocalDataSaveKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LocalDataSaveKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(LocalDataSaveKey left, LocalDataSaveKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LocalDataSaveKey left, LocalDataSaveKey right)
        {
            return !left.Equals(right);
        }
    }
}
