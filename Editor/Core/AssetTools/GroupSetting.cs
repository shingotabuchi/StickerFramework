using System;
using UnityEngine;

namespace StickerFwk.Core.Editor.AssetTools
{
    [Serializable]
    public class GroupSetting
    {
        public string GroupName = string.Empty;
        public string Comment = string.Empty;
        public SchemaSettings SchemaSettings;
        public AssetPathRule[] AssetPathRules = Array.Empty<AssetPathRule>();
        [HideInInspector] public string[] AssetPathFilters = Array.Empty<string>();
        [HideInInspector] public string AddressReplaceFrom = string.Empty;
        [HideInInspector] public string AddressReplaceTo = string.Empty;
        public RegexLabel[] Labels = Array.Empty<RegexLabel>();
        public bool Disabled;

        [NonSerialized] public AssetPathRule[] CompiledAssetPathRules = Array.Empty<AssetPathRule>();

        public void CompileRegexes()
        {
            CompiledAssetPathRules = BuildCompiledAssetPathRules();
            for (var i = 0; i < CompiledAssetPathRules.Length; i++)
                CompiledAssetPathRules[i].CompileRegex();
        }

        public bool TryGetAssetPathRule(string assetPath, out AssetPathRule assetPathRule)
        {
            for (var i = 0; i < CompiledAssetPathRules.Length; i++)
            {
                var rule = CompiledAssetPathRules[i];
                if (!rule.IsMatch(assetPath)) continue;
                assetPathRule = rule;
                return true;
            }

            assetPathRule = null;
            return false;
        }

        public string GetAddressFromAssetPath(string assetPath)
        {
            if (!TryGetAssetPathRule(assetPath, out var assetPathRule)) return assetPath;
            return assetPathRule.GetAddressFromAssetPath(assetPath);
        }

        private AssetPathRule[] BuildCompiledAssetPathRules()
        {
            if (AssetPathRules.Length > 0) return AssetPathRules;

            var legacyRules = new AssetPathRule[AssetPathFilters.Length];
            for (var i = 0; i < AssetPathFilters.Length; i++)
                legacyRules[i] = new AssetPathRule
                {
                    Filter = AssetPathFilters[i],
                    Replacement = string.Empty,
                    AddressReplaceFrom = AddressReplaceFrom,
                    AddressReplaceTo = AddressReplaceTo
                };

            return legacyRules;
        }
    }
}