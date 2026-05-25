using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace StickerFwk.Core.Editor.AssetTools
{
    public static class AddressablesProcessor
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        [MenuItem("Tools/Sticker Framework/Addressables/Apply Rules")]
        public static void ApplyRules()
        {
            var settings = Settings;
            if (!EnsureSettings(settings))
            {
                return;
            }

            try
            {
                UpdateProgressBar("Applying Addressable Group Settings...", 0.2f);
                ApplyGroupSettings(settings);

                UpdateProgressBar("Processing Duplicates...", 0.8f);
                DuplicatesProcessor.SetDuplicatesAsAddressable();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(settings);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Tools/Sticker Framework/Addressables/Clear All and Apply Rules")]
        public static void ResetAndSetup()
        {
            var settings = Settings;
            if (!EnsureSettings(settings))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Clear All Addressables?",
                    "This will remove every Addressables entry from every group in this project, then reapply Sticker Framework rules. This cannot be scoped automatically.",
                    "Clear All and Apply",
                    "Cancel"))
            {
                return;
            }

            try
            {
                // 1. Clear Addressables
                UpdateProgressBar("Clearing all Addressable groups...", 0.0f);
                ClearAllAddressables(settings);

                // 2. Apply Settings
                UpdateProgressBar("Applying Addressable Group Settings...", 0.2f);
                ApplyGroupSettings(settings);

                // 3. Process Duplicates
                UpdateProgressBar("Processing Duplicates...", 0.8f);
                DuplicatesProcessor.SetDuplicatesAsAddressable();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(settings);
                AssetDatabase.Refresh();
            }
        }

        private static bool EnsureSettings(AddressableAssetSettings settings)
        {
            if (settings != null)
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "Addressables Settings Missing",
                "Create Addressables settings before applying Sticker Framework Addressables rules.",
                "OK");
            return false;
        }

        private static void ClearAllAddressables(AddressableAssetSettings settings)
        {
            // Remove all assets from all groups
            // We iterate backwards or use a list copy to avoid modification during iteration issues if we were removing groups,
            // but here we are removing entries from groups.
            foreach (var group in settings.groups)
            {
                if (group == null) continue;

                // Skip ReadOnly groups if necessary, but usually we want to clear everything user-defined.
                // Default group and Built-in data usually shouldn't be touched in a way that breaks them, 
                // but RemoveAllAssetsInGroup handles entries.

                AddressablesTools.RemoveAllAssetsInGroup(settings, group);
            }
        }

        private static void ApplyGroupSettings(AddressableAssetSettings addressableSettings)
        {
            var settings = AddressableGroupSettings.GetOrCreate();
            settings.CompileRegexes();

            // Iterate through each group setting and find matching assets
            // This avoids GetAllAssetPaths() which is slow for large projects.
            foreach (var groupSetting in settings.GroupSettings)
            {
                if (groupSetting.Disabled) continue;

                var group = AddressablesTools.GetOrCreateGroup(addressableSettings, groupSetting.GroupName);

                for (var i = 0; i < groupSetting.CompiledAssetPathRules.Length; i++)
                {
                    var assetPathRule = groupSetting.CompiledAssetPathRules[i];
                    var regexPattern = assetPathRule.Filter;

                    // Extract the root folder from the regex to limit the search scope
                    var searchFolder = GetRootFolderFromRegex(regexPattern);

                    string[] guids;
                    if (Directory.Exists(searchFolder))
                        guids = AssetDatabase.FindAssets("", new[] { searchFolder });
                    else
                        // Fallback if regex doesn't point to a specific folder (e.g. "Assets/.*")
                        // We search in Assets folder but this might still be broad.
                        // Ideally regexes should start with a specific folder.
                        guids = AssetDatabase.FindAssets("", new[] { "Assets" });

                    foreach (var guid in guids)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                        if (Directory.Exists(assetPath)) continue;

                        if (assetPathRule.IsMatch(assetPath))
                        {
                            var entry = addressableSettings.CreateOrMoveEntry(guid, group);

                            if (entry != null)
                            {
                                entry.address = assetPathRule.GetAddressFromAssetPath(assetPath);

                                // Apply labels
                                foreach (var labelRegex in groupSetting.Labels)
                                    if (labelRegex.IsMatch(assetPath))
                                    {
                                        var labelName = labelRegex.GetLabel(assetPath);
                                        addressableSettings.AddLabel(labelName);
                                        entry.SetLabel(labelName, true, true);
                                    }
                            }
                        }
                    }
                }

                // Apply Schema Settings (Packing Mode, etc.)
                ApplySchemaSettings(group, groupSetting.SchemaSettings);
            }
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
        }

        private static string GetRootFolderFromRegex(string regex)
        {
            // Simple parser to extract a static path prefix from a regex
            // Assumes regex starts with ^ and uses / as separator
            // Example: "^Assets/Textures/.*" -> "Assets/Textures"

            var cleanRegex = regex;
            if (cleanRegex.StartsWith("^")) cleanRegex = cleanRegex.Substring(1);

            // Find the first character that is likely a regex special character
            // We want to stop before any of these: . * ? [ ( { \ | +
            // But we also want to stop at the last '/' before that.

            var specialChars = new[] { '.', '*', '?', '[', '(', '{', '\\', '|', '+' };
            var firstSpecialIndex = cleanRegex.IndexOfAny(specialChars);

            string prefix;
            if (firstSpecialIndex == -1)
                prefix = cleanRegex;
            else
                prefix = cleanRegex.Substring(0, firstSpecialIndex);

            // If the prefix ends with '/', remove it to get the folder path.
            // If it doesn't end with '/', we might be in the middle of a folder name (e.g. "Assets/Tex"),
            // so we should go back to the last '/'.

            if (prefix.EndsWith("/"))
            {
                prefix = prefix.TrimEnd('/');
            }
            else
            {
                var lastSlash = prefix.LastIndexOf('/');
                if (lastSlash != -1) prefix = prefix.Substring(0, lastSlash);
                // No slash found, maybe just "Assets"?
                // If the regex was "^Assets.*", prefix is "Assets".
                // If regex was "^.*", prefix is empty.
            }

            if (string.IsNullOrEmpty(prefix) || !Directory.Exists(prefix)) return "Assets";

            return prefix;
        }

        private static void UpdateProgressBar(string message, float progress)
        {
            EditorUtility.DisplayProgressBar("Sticker Framework Addressables", message, progress);
        }
    }
}
