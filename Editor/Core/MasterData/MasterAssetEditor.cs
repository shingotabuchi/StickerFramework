using StickerFwk.Core.MasterData;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace StickerFwk.Core.Editor.MasterData
{
    [CustomEditor(typeof(MasterAsset<>), true)]
    public class MasterAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty dataList;
        private ReorderableList reorderableList;

        private void OnEnable()
        {
            dataList = serializedObject.FindProperty("_data");

            if (dataList != null)
            {
                reorderableList = new ReorderableList(serializedObject, dataList, true, true, true, true);

                reorderableList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, $"Data ({dataList.arraySize})");
                };

                reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    SerializedProperty element = dataList.GetArrayElementAtIndex(index);
                    SerializedProperty idProperty = element.FindPropertyRelative("_id");
                    SerializedProperty nameProperty = element.FindPropertyRelative("_name");
                    string idValue = idProperty != null ? idProperty.stringValue : (index + 1).ToString();
                    string nameValue = nameProperty != null ? nameProperty.stringValue : "";
                    string label = $"ID:{idValue} {nameValue}";

                    EditorGUI.PropertyField(rect, element, new GUIContent(label), true);
                };

                reorderableList.elementHeightCallback = (int index) =>
                {
                    SerializedProperty element = dataList.GetArrayElementAtIndex(index);
                    return EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
                };
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "_data");

            if (reorderableList != null)
            {
                reorderableList.DoLayoutList();
            }
            else if (dataList != null)
            {
                EditorGUILayout.PropertyField(dataList, true);
            }
            else
            {
                DrawDefaultInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
