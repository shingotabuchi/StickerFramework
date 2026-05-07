using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StickerFwk.Core.InspectorTools;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor.InspectorTools
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public sealed class SubclassSelectorDrawer : PropertyDrawer
    {
        static readonly Dictionary<Type, Type[]> _cache = new Dictionary<Type, Type[]>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label.text, "[SubclassSelector] requires [SerializeReference]");
                return;
            }

            var fieldType = ResolveFieldType();
            if (fieldType == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var subclasses = GetSubclasses(fieldType);
            var currentTypeName = ExtractTypeName(property.managedReferenceFullTypename);
            var currentIndex = Array.FindIndex(subclasses, t => t == null ? string.IsNullOrEmpty(currentTypeName) : t.Name == currentTypeName);

            var labels = subclasses.Select(t => t == null ? new GUIContent("<null>") : new GUIContent(t.Name)).ToArray();

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var labelRect = new Rect(headerRect.x, headerRect.y, EditorGUIUtility.labelWidth, headerRect.height);
            var dropdownRect = new Rect(headerRect.x + EditorGUIUtility.labelWidth, headerRect.y,
                headerRect.width - EditorGUIUtility.labelWidth, headerRect.height);

            EditorGUI.LabelField(labelRect, label);
            var newIndex = EditorGUI.Popup(dropdownRect, Mathf.Max(0, currentIndex), labels);
            if (newIndex != currentIndex)
            {
                var newType = subclasses[newIndex];
                property.managedReferenceValue = newType == null ? null : Activator.CreateInstance(newType);
                property.serializedObject.ApplyModifiedProperties();
            }

            var bodyRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width, position.height - EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing);
            EditorGUI.PropertyField(bodyRect, property, GUIContent.none, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUIUtility.singleLineHeight;
            }
            var inner = EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + inner;
        }

        Type ResolveFieldType()
        {
            var t = fieldInfo.FieldType;
            if (t.IsArray)
            {
                t = t.GetElementType();
            }
            else if (t.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(t))
            {
                t = t.GetGenericArguments()[0];
            }
            return t;
        }

        static Type[] GetSubclasses(Type baseType)
        {
            if (_cache.TryGetValue(baseType, out var cached))
            {
                return cached;
            }

            var list = new List<Type> { null };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition)
                    {
                        continue;
                    }
                    if (!baseType.IsAssignableFrom(t))
                    {
                        continue;
                    }
                    if (t.GetConstructor(Type.EmptyTypes) == null)
                    {
                        continue;
                    }
                    list.Add(t);
                }
            }

            list.Sort((a, b) => string.Compare(a?.Name, b?.Name, StringComparison.Ordinal));
            var arr = list.ToArray();
            _cache[baseType] = arr;
            return arr;
        }

        static string ExtractTypeName(string fullTypename)
        {
            if (string.IsNullOrEmpty(fullTypename))
            {
                return null;
            }
            var space = fullTypename.IndexOf(' ');
            var typeFull = space >= 0 ? fullTypename.Substring(space + 1) : fullTypename;
            var dot = typeFull.LastIndexOf('.');
            return dot >= 0 ? typeFull.Substring(dot + 1) : typeFull;
        }
    }
}
