using System;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace StickerFwk.Core.Editor.AssetTools
{
    [Serializable]
    public struct SchemaSettings
    {
        public DistributionType DistributionType;
        public BundledAssetGroupSchema.BundlePackingMode PackingMode;
    }
}