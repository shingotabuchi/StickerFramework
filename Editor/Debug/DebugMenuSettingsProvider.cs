#if STICKER_DEBUG
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Debug.Editor
{
    /// <summary>
    /// Exposes <see cref="DebugMenuSettings"/> under <b>Edit → Project Settings → Sticker → Debug Menu</b>.
    /// The provider loads (or creates) a single asset at <see cref="SettingsAssetPath"/> and edits it in place.
    /// </summary>
    internal static class DebugMenuSettingsProvider
    {
        private const string SettingsAssetPath = "Assets/Settings/DebugMenuSettings.asset";
        private const string SettingsMenuPath = "Project/Sticker/Debug Menu";

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            var provider = new SettingsProvider(SettingsMenuPath, SettingsScope.Project)
            {
                label = "Debug Menu",
                guiHandler = _ => DrawGui(),
                keywords = new HashSet<string>(new[]
                {
                    "Debug", "Menu", "Sticker", "Overlay", "IMGUI"
                })
            };
            return provider;
        }

        private static void DrawGui()
        {
            var settings = LoadOrCreateSettings();
            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Could not load or create DebugMenuSettings asset at " + SettingsAssetPath,
                    MessageType.Warning);
                return;
            }

            var serialized = new SerializedObject(settings);
            serialized.Update();

            EditorGUILayout.LabelField("Asset", AssetDatabase.GetAssetPath(settings), EditorStyles.miniLabel);
            EditorGUILayout.Space();

            var iterator = serialized.GetIterator();
            iterator.NextVisible(true); // skip m_Script
            while (iterator.NextVisible(false))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Ping Asset"))
            {
                EditorGUIUtility.PingObject(settings);
            }
        }

        private static DebugMenuSettings LoadOrCreateSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DebugMenuSettings>(SettingsAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var directory = Path.GetDirectoryName(SettingsAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var instance = ScriptableObject.CreateInstance<DebugMenuSettings>();
            AssetDatabase.CreateAsset(instance, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            return instance;
        }
    }
}
#endif
