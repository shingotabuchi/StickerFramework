using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace StickerFwk.Core.Editor.AssetTools
{
    public class DuplicatesGroupSettings : ScriptableObject
    {
        private const string SettingsAssetPath = "Assets/AddressableAssetsData/DuplicatesGroupSettings.asset";

        public string DefaultGroupName = "Duplicates";

        public SchemaSettings DefaultSchemaSettings = new()
        {
            DistributionType = DistributionType.Local,
            PackingMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether
        };

        public GroupSetting[] GroupSettings = Array.Empty<GroupSetting>();

        public void CompileRegexes()
        {
            foreach (var setting in GroupSettings) setting.CompileRegexes();
        }

        public static DuplicatesGroupSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DuplicatesGroupSettings>(SettingsAssetPath);
            if (settings != null) return settings;

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath) ?? "Assets");
            settings = CreateInstance<DuplicatesGroupSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            settings = AssetDatabase.LoadAssetAtPath<DuplicatesGroupSettings>(SettingsAssetPath);

            return settings;
        }

        public bool TryGetGroupEntryFromAssetPath(string assetPath, [NotNullWhen(true)] out GroupSetting groupSetting)
        {
            groupSetting = null;

            foreach (var setting in GroupSettings)
            {
                if (setting.Disabled) continue;

                if (setting.TryGetAssetPathRule(assetPath, out _))
                {
                    groupSetting = setting;
                    return true;
                }
            }

            return false;
        }
    }
}