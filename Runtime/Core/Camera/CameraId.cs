using System;
using UnityEngine;

namespace StickerFwk.Core
{
    [Serializable]
    public struct CameraId : IEquatable<CameraId>
    {
        [SerializeField] string _value;

        public CameraId(string value)
        {
            _value = value;
        }

        public string Value => _value;
        public bool IsValid => !string.IsNullOrEmpty(_value);

        public bool Equals(CameraId other) =>
            string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CameraId o && Equals(o);

        public override int GetHashCode() =>
            _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

        public override string ToString() => IsValid ? _value : "<invalid>";

        public static bool operator ==(CameraId a, CameraId b) => a.Equals(b);
        public static bool operator !=(CameraId a, CameraId b) => !a.Equals(b);

        // Framework-owned slots — these are the ONLY camera IDs the framework
        // itself references. Projects define their own statics elsewhere.
        public static readonly CameraId UI = new CameraId("UI");
        public static readonly CameraId UIOverlay = new CameraId("UIOverlay");
        public static readonly CameraId Wipe = new CameraId("Wipe");
    }
}
