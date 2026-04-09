using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StickerFwk.Core.InspectorTools;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor.InspectorTools
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public class ButtonAttributeEditor : UnityEditor.Editor
    {
        private List<(MethodInfo method, ButtonAttribute attribute)> _buttonMethods;

        private void OnEnable()
        {
            _buttonMethods = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(m => (method: m, attribute: m.GetCustomAttribute<ButtonAttribute>()))
                .Where(pair => pair.attribute != null)
                .ToList();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (_buttonMethods == null || _buttonMethods.Count == 0)
                return;

            EditorGUILayout.Space();

            // Group buttons by row
            var standalone = _buttonMethods.Where(b => !b.attribute.HasRow).ToList();
            var grouped = _buttonMethods.Where(b => b.attribute.HasRow)
                .GroupBy(b => b.attribute.Row)
                .ToList();

            // Draw standalone buttons
            foreach (var (method, attribute) in standalone)
            {
                if (attribute.Space > 0)
                    GUILayout.Space(attribute.Space);

                var label = string.IsNullOrEmpty(attribute.Name)
                    ? ObjectNames.NicifyVariableName(method.Name)
                    : attribute.Name;

                if (GUILayout.Button(label))
                {
                    foreach (var t in targets)
                    {
                        method.Invoke(t, null);
                    }
                }
            }

            // Draw grouped buttons (horizontal rows)
            foreach (var group in grouped)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var (method, attribute) in group)
                {
                    var label = string.IsNullOrEmpty(attribute.Name)
                        ? ObjectNames.NicifyVariableName(method.Name)
                        : attribute.Name;

                    if (GUILayout.Button(label))
                    {
                        foreach (var t in targets)
                        {
                            method.Invoke(t, null);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}

