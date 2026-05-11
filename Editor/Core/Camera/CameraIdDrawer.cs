using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StickerFwk.Core;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor
{
    [CustomPropertyDrawer(typeof(CameraId))]
    public class CameraIdDrawer : PropertyDrawer
    {
        static string[] _cachedDisplayNames;
        static string[] _cachedValues;
        static int _cachedAssemblyLoadVersion = -1;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureCache();
            var valueProp = property.FindPropertyRelative("_value");
            var current = valueProp.stringValue ?? string.Empty;
            var index = Array.IndexOf(_cachedValues, current);
            if (index < 0)
            {
                var displayWithMissing = new string[_cachedDisplayNames.Length + 1];
                var valuesWithMissing = new string[_cachedValues.Length + 1];
                displayWithMissing[0] = $"<missing: {(string.IsNullOrEmpty(current) ? "<invalid>" : current)}>";
                valuesWithMissing[0] = current;
                Array.Copy(_cachedDisplayNames, 0, displayWithMissing, 1, _cachedDisplayNames.Length);
                Array.Copy(_cachedValues, 0, valuesWithMissing, 1, _cachedValues.Length);
                var newIndex = EditorGUI.Popup(position, label.text, 0, displayWithMissing);
                if (newIndex != 0)
                {
                    valueProp.stringValue = valuesWithMissing[newIndex];
                }
            }
            else
            {
                var newIndex = EditorGUI.Popup(position, label.text, index, _cachedDisplayNames);
                if (newIndex != index && newIndex >= 0 && newIndex < _cachedValues.Length)
                {
                    valueProp.stringValue = _cachedValues[newIndex];
                }
            }
        }

        static void EnsureCache()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var version = assemblies.Length;
            if (_cachedDisplayNames != null && version == _cachedAssemblyLoadVersion)
                return;

            var entries = new List<(string display, string value)>();
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    FieldInfo[] fields;
                    try { fields = t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly); }
                    catch { continue; }
                    foreach (var f in fields)
                    {
                        if (!f.IsInitOnly) continue;
                        if (f.FieldType != typeof(CameraId)) continue;
                        CameraId id;
                        try { id = (CameraId)f.GetValue(null); }
                        catch { continue; }
                        if (!id.IsValid) continue;
                        var display = $"{t.FullName}.{f.Name}";
                        entries.Add((display, id.Value));
                    }
                }
            }

            entries = entries
                .GroupBy(e => e.value)
                .Select(g => g.First())
                .OrderBy(e => e.display, StringComparer.Ordinal)
                .ToList();

            _cachedDisplayNames = entries.Select(e => e.display).ToArray();
            _cachedValues = entries.Select(e => e.value).ToArray();
            _cachedAssemblyLoadVersion = version;
        }
    }
}
