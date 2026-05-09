using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace StickerFwk.Core.Editor.AssetTools
{
    public static class AddressablesTools
    {
        public static void RemoveAllAssetsInGroup(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            // NOTE: Reverse()しないと後段で追加するアセットがYAML上で逆順になる（なぜ…）
            var guids = group.entries.Select(e => e.guid).Reverse().ToArray();
            foreach (var guid in guids)
            {
                settings.RemoveAssetEntry(guid, false);
            }
        }

        public static void AddAssetsToGroup(
            AddressableAssetSettings settings, AddressableAssetGroup group,
            string filter, string searchInFolder,
            StringBuilder logger)
        {
            var assets = AssetDatabase
                .FindAssets(filter, new[] { searchInFolder })
                .Select(guid => (guid, path: AssetDatabase.GUIDToAssetPath(guid))).OrderBy(asset => asset.path)
                .ToArray();

            foreach (var asset in assets)
            {
                if (Directory.Exists(asset.path))
                {
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(asset.guid, group, true, false);

                // searchInFolderからの相対パスをアドレスとする
                entry.address = asset.path.Substring(searchInFolder.Length);

                logger.AppendLine(asset.path);
            }
        }

        public static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name);
            if (group == null)
            {
                group = settings.CreateGroup(name, false, false, false, settings.DefaultGroup.Schemas);
            }

            return group;
        }
    }
}