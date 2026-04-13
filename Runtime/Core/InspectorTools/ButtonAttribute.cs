using System;

namespace StickerFwk.Core.InspectorTools
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class ButtonAttribute : Attribute
    {
        public readonly string Name;
        public readonly string Row;
        public readonly float Space;
        public readonly bool HasRow;
        public readonly bool PlayModeOnly;

        public ButtonAttribute(string name = default, string row = default, float space = default,
            bool playModeOnly = false)
        {
            Row = row;
            HasRow = !string.IsNullOrEmpty(Row);
            Name = name;
            Space = space;
            PlayModeOnly = playModeOnly;
        }
    }
}
