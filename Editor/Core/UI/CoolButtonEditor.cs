using StickerFwk.Core.UI;
using UnityEditor;
using UnityEditor.UI;

namespace StickerFwk.Core.Editor.UI
{
    [CustomEditor(typeof(CoolButton), true)]
    [CanEditMultipleObjects]
    public class CoolButtonEditor : ButtonEditor
    {
        private SerializedProperty _seName;

        protected override void OnEnable()
        {
            base.OnEnable();
            _seName = serializedObject.FindProperty("_seName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_seName);
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }
    }
}
