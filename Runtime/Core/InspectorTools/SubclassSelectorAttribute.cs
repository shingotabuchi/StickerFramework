using System;
using UnityEngine;

namespace StickerFwk.Core.InspectorTools
{
    /// <summary>
    /// Apply to a [SerializeReference] field to render a dropdown in the inspector
    /// listing every concrete, non-abstract subclass of the field's declared type.
    /// Lets users assign a polymorphic instance without writing custom UI code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
