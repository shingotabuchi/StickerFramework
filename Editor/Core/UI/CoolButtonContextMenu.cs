using System.Collections.Generic;
using StickerFwk.Core.UI;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Core.Editor.UI
{
    public static class CoolButtonContextMenu
    {
        private const string ConvertMenuPath = "CONTEXT/Button/Convert to Cool Button";

        private readonly struct ReferenceTarget
        {
            public readonly Object Container;
            public readonly string PropertyPath;

            public ReferenceTarget(Object container, string propertyPath)
            {
                Container = container;
                PropertyPath = propertyPath;
            }
        }

        [MenuItem(ConvertMenuPath, true)]
        private static bool ValidateConvertToCoolButton(MenuCommand menuCommand)
        {
            var button = menuCommand.context as Button;
            if (button == null)
            {
                return false;
            }

            if (button is CoolButton)
            {
                return false;
            }

            return button.GetComponent<CoolButton>() == null;
        }

        [MenuItem(ConvertMenuPath)]
        private static void ConvertToCoolButton(MenuCommand menuCommand)
        {
            var button = menuCommand.context as Button;
            if (button == null || button is CoolButton)
            {
                return;
            }

            var go = button.gameObject;
            if (go.GetComponent<CoolButton>() != null)
            {
                Debug.LogWarning("[CoolButton] GameObject already has a CoolButton component.");
                return;
            }

            Undo.RegisterCompleteObjectUndo(go, "Convert to Cool Button");

            var referenceTargets = CollectReferenceTargets(button);

            if (!ComponentUtility.CopyComponent(button))
            {
                Debug.LogError("[CoolButton] Failed to copy Button component.");
                return;
            }

            Undo.DestroyObjectImmediate(button);

            var coolButton = Undo.AddComponent<CoolButton>(go);
            if (coolButton == null)
            {
                Debug.LogError("[CoolButton] Failed to add CoolButton component.");
                return;
            }

            if (!ComponentUtility.PasteComponentValues(coolButton))
            {
                Debug.LogWarning("[CoolButton] Failed to paste Button values onto CoolButton.");
            }

            RemapReferences(referenceTargets, coolButton);

            EditorUtility.SetDirty(go);
        }

        private static List<ReferenceTarget> CollectReferenceTargets(Object target)
        {
            var results = new List<ReferenceTarget>();
            var targetId = target.GetInstanceID();
            var component = target as Component;
            if (component == null)
            {
                return results;
            }

            var components = component.transform.root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null || comp == target)
                {
                    continue;
                }

                var serializedObject = new SerializedObject(comp);
                var property = serializedObject.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (property.objectReferenceInstanceIDValue == targetId)
                    {
                        results.Add(new ReferenceTarget(comp, property.propertyPath));
                    }
                }
            }

            return results;
        }

        private static void RemapReferences(List<ReferenceTarget> references, Object newTarget)
        {
            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                if (reference.Container == null)
                {
                    continue;
                }

                Undo.RecordObject(reference.Container, "Convert to Cool Button");

                var serializedObject = new SerializedObject(reference.Container);
                var property = serializedObject.FindProperty(reference.PropertyPath);
                if (property == null)
                {
                    continue;
                }

                property.objectReferenceValue = newTarget;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(reference.Container);
            }
        }
    }
}
