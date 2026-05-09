using System.Collections.Generic;
using System.IO;
using System.Linq;
using StickerFwk.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace StickerFwk.Core.Editor.AssetTools
{
    public static class DuplicatesProcessor
    {
        private const string GeneratedSignature = "(Dupes)";
        private static readonly AddressableAssetSettings s_settings;

        static DuplicatesProcessor()
        {
            s_settings = AddressableAssetSettingsDefaultObject.Settings;
        }

        public static void SetDuplicatesAsAddressable()
        {
            try
            {
                UpdateProgressBar("Clearing generated groups...", 0.0f);

                var generatedGroups = GetGeneratedGroups();

                foreach (var group in generatedGroups) AddressablesTools.RemoveAllAssetsInGroup(s_settings, group);

                UpdateProgressBar("Running 'Check for Duplicate Bundle Dependencies' rule...", 0.1f);
                var duplicateBundleRule = new DuplicateBundleRule();
                var duplicateAssetPaths = duplicateBundleRule.GetDuplicateAssetPaths(s_settings);

                var processedCount = SetDuplicatesAsAddressableInternal(s_settings, duplicateAssetPaths);

                generatedGroups = GetGeneratedGroups();
                foreach (var group in generatedGroups)
                    if (group.entries.Count == 0)
                        s_settings.RemoveGroup(group);

                Log.Info($"Set {processedCount} duplicate assets as Addressable.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(s_settings);
                AssetDatabase.Refresh();
            }
        }

        private static List<AddressableAssetGroup> GetGeneratedGroups()
        {
            return s_settings.groups
                .Where(g => g != null && g.name.Contains(GeneratedSignature))
                .ToList();
        }

        private static int SetDuplicatesAsAddressableInternal(AddressableAssetSettings settings,
            IReadOnlyList<string> duplicateAssetPaths)
        {
            var duplicatesGroupSettings = DuplicatesGroupSettings.GetOrCreate();
            duplicatesGroupSettings.CompileRegexes();
            var schemaSettingsDict = new Dictionary<AddressableAssetGroup, SchemaSettings>();
            var totalEntries = duplicateAssetPaths.Count;
            var processedEntries = 0;
            for (var i = 0; i < totalEntries; i++)
            {
                var assetPath = duplicateAssetPaths[i];
                var guid = AssetDatabase.AssetPathToGUID(assetPath);

                if (string.IsNullOrEmpty(guid))
                {
                    Log.Warning($"Invalid asset path: {assetPath}");
                    continue;
                }

                var existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null)
                {
                    Log.Warning($"Already set as Addressable: {assetPath}");
                    continue;
                }

                var context = new DuplicatesGroupContext(settings, duplicatesGroupSettings, assetPath);

                schemaSettingsDict[context.Group] = context.SchemaSettings;
                var entry = settings.CreateOrMoveEntry(guid, context.Group);
                entry.address = context.Address;

                foreach (var label in context.LabelsToApply) entry.SetLabel(label, true, true);

                processedEntries++;
                var progress = (i + 1) / (float)totalEntries;
                var progressInfo = $"({i + 1}/{totalEntries}): {Path.GetFileName(assetPath)}";
                UpdateProgressBar($"Moving duplicates... {progressInfo}", 0.2f + progress * 0.8f);
            }

            foreach (var kvp in schemaSettingsDict) ApplySchemaSettings(kvp.Key, kvp.Value);

            return processedEntries;
        }

        private static void UpdateProgressBar(string message, float progress)
        {
            EditorUtility.DisplayProgressBar("Processing Duplicate Assets", message, progress);
        }

        private static void ApplySchemaSettings(AddressableAssetGroup group, SchemaSettings schemaSettings)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) schema = group.AddSchema<BundledAssetGroupSchema>();

            schema.BundleMode = schemaSettings.PackingMode;

            switch (schemaSettings.DistributionType)
            {
                case DistributionType.Local:
                    schema.BuildPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalLoadPath);
                    break;
                case DistributionType.Remote:
                    schema.BuildPath.SetVariableByName(group.Settings, AddressableAssetSettings.kRemoteBuildPath);
                    schema.LoadPath.SetVariableByName(group.Settings, AddressableAssetSettings.kRemoteLoadPath);
                    break;
            }

            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(s_settings);
        }

        /// <summary>
        ///     Information about the group to store duplicate assets
        /// </summary>
        private class DuplicatesGroupContext
        {
            public DuplicatesGroupContext(
                AddressableAssetSettings settings,
                DuplicatesGroupSettings duplicatesGroupSettings,
                string assetPath)
            {
                var labels = new List<string>();

                if (duplicatesGroupSettings.TryGetGroupEntryFromAssetPath(assetPath, out var groupSetting))
                {
                    var name = $"{groupSetting.GroupName} {GeneratedSignature}";
                    Group = AddressablesTools.GetOrCreateGroup(settings, name);
                    SchemaSettings = groupSetting.SchemaSettings;
                    Address = groupSetting.GetAddressFromAssetPath(assetPath);

                    foreach (var label in groupSetting.Labels)
                    {
                        if (!label.IsMatch(assetPath)) continue;

                        var labelName = label.GetLabel(assetPath);
                        // Create label in AddressableAssetSettings if it doesn't exist
                        if (!settings.GetLabels().Contains(labelName)) settings.AddLabel(labelName);

                        labels.Add(labelName);
                    }
                }
                else
                {
                    // Use default settings
                    var name = $"{duplicatesGroupSettings.DefaultGroupName} {GeneratedSignature}";
                    Group = AddressablesTools.GetOrCreateGroup(settings, name);
                    SchemaSettings = duplicatesGroupSettings.DefaultSchemaSettings;
                    Address = assetPath;
                }

                LabelsToApply = labels;
            }

            public AddressableAssetGroup Group { get; }
            public SchemaSettings SchemaSettings { get; }
            public IReadOnlyList<string> LabelsToApply { get; }
            public string Address { get; }
        }

        /// <summary>
        ///     Class to get CheckDupeResults
        /// </summary>
        private class DuplicateBundleRule : CheckBundleDupeDependencies
        {
            public List<string> GetDuplicateAssetPaths(AddressableAssetSettings settings)
            {
                RefreshAnalysis(settings);
                return CheckDupeResults.Select(r => r.AssetPath).Distinct().ToList();
            }
        }
    }
}