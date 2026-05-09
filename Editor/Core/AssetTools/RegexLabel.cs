using System;

namespace StickerFwk.Core.Editor.AssetTools
{
    [Serializable]
    public class RegexLabel : AssetPathRule
    {
        public string GetLabel(string assetPath)
        {
            return ApplyReplacement(assetPath);
        }
    }
}