using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor.AssetTools
{
    public class AddressableGroupSettings : ScriptableObject
    {
        private const string SettingsAssetPath = "Assets/AddressableAssetsData/AddressableGroupSettings.asset";

        public GroupSetting[] GroupSettings = Array.Empty<GroupSetting>();

        public void CompileRegexes()
        {
            foreach (var setting in GroupSettings) setting.CompileRegexes();
        }

        public static AddressableGroupSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AddressableGroupSettings>(SettingsAssetPath);
            if (settings != null) return settings;

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath) ?? "Assets");
            settings = CreateInstance<AddressableGroupSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            settings = AssetDatabase.LoadAssetAtPath<AddressableGroupSettings>(SettingsAssetPath);

            return settings;
        }
    }
}