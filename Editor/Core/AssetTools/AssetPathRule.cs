using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StickerFwk.Core.Editor.AssetTools
{
    [Serializable]
    public class AssetPathRule
    {
        public string Filter = string.Empty;
        public string Replacement = string.Empty;
        [HideInInspector] public string AddressReplaceFrom = string.Empty;
        [HideInInspector] public string AddressReplaceTo = string.Empty;

        [NonSerialized] private Regex _compiledFilter;

        public void CompileRegex()
        {
            _compiledFilter = new Regex(Filter, RegexOptions.Compiled);
        }

        public bool IsMatch(string assetPath)
        {
            if (_compiledFilter == null) CompileRegex();

            return _compiledFilter!.IsMatch(assetPath);
        }

        public string ApplyReplacement(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (_compiledFilter == null) CompileRegex();

            return _compiledFilter!.Replace(input, Replacement ?? string.Empty);
        }

        public string GetAddressFromAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;

            if (!string.IsNullOrEmpty(AddressReplaceFrom))
            {
                if (!assetPath.StartsWith(AddressReplaceFrom)) return assetPath;

                var replacedPrefix = AddressReplaceTo ?? string.Empty;
                var remainingPath = assetPath.Substring(AddressReplaceFrom.Length);

                if (string.IsNullOrEmpty(replacedPrefix)) return remainingPath.TrimStart('/');
                if (string.IsNullOrEmpty(remainingPath)) return replacedPrefix.TrimEnd('/');

                return $"{replacedPrefix.TrimEnd('/')}/{remainingPath.TrimStart('/')}";
            }

            return ApplyReplacement(assetPath);
        }
    }
}