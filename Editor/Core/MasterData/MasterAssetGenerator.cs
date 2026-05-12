using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using StickerFwk.Core.MasterData;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor.MasterData
{
    /// <summary>
    /// Generates <c>{TypeName}Asset.cs</c> ScriptableObject wrappers for every concrete
    /// <see cref="MasterData{T}"/> subclass found in the project. Each generated file is
    /// placed in a sibling <c>ScriptableObjects/</c> folder next to the source script
    /// and reuses the source type's namespace.
    /// </summary>
    public static class MasterAssetGenerator
    {
        private const string GeneratedFolderName = "ScriptableObjects";
        private const string MenuPathPrefix = "Master";

        [MenuItem("Tools/Master/Generate Asset Scripts")]
        public static void GenerateAssetScripts()
        {
            var masterTypes = FindMasterDataTypes();
            if (masterTypes.Count == 0)
            {
                Debug.Log("[MasterAssetGenerator] No MasterData<T> subclasses found.");
                return;
            }

            var generated = 0;
            var skipped = 0;

            foreach (var type in masterTypes)
            {
                if (!TryGetScriptPath(type, out var scriptPath))
                {
                    Debug.LogWarning($"[MasterAssetGenerator] Could not find source script for {type.FullName}; skipping.");
                    continue;
                }

                var sourceDir = Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(sourceDir))
                {
                    continue;
                }

                var targetDir = $"{sourceDir}/{GeneratedFolderName}";
                if (!AssetDatabase.IsValidFolder(targetDir))
                {
                    AssetDatabase.CreateFolder(sourceDir, GeneratedFolderName);
                }

                var assetClassName = type.Name + "Asset";
                var targetPath = $"{targetDir}/{assetClassName}.cs";
                if (File.Exists(targetPath))
                {
                    skipped++;
                    continue;
                }

                File.WriteAllText(targetPath, GenerateAssetScript(type, assetClassName));
                generated++;
                Debug.Log($"[MasterAssetGenerator] Generated {targetPath}");
            }

            if (generated > 0)
            {
                AssetDatabase.Refresh();
            }

            Debug.Log($"[MasterAssetGenerator] Done. Generated: {generated}, already-existing: {skipped}.");
        }

        [MenuItem("Tools/Master/List Master Classes")]
        public static void ListMasterClasses()
        {
            var masterTypes = FindMasterDataTypes();
            Debug.Log($"[MasterAssetGenerator] Found {masterTypes.Count} MasterData<T> subclass(es):");
            foreach (var type in masterTypes)
            {
                var assetName = type.Name + "Asset";
                var assetType = type.Assembly.GetType((string.IsNullOrEmpty(type.Namespace) ? "" : type.Namespace + ".") + assetName)
                                ?? FindTypeByName(assetName);
                Debug.Log($"  {type.FullName} -> {assetName} (asset class exists: {assetType != null})");
            }
        }

        private static List<Type> FindMasterDataTypes()
        {
            var result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsGenericTypeDefinition)
                    {
                        continue;
                    }

                    if (IsSubclassOfMasterData(type))
                    {
                        result.Add(type);
                    }
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return result;
        }

        private static bool IsSubclassOfMasterData(Type type)
        {
            var current = type.BaseType;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(MasterData<>))
                {
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }

        private static bool TryGetScriptPath(Type type, out string assetPath)
        {
            var guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    assetPath = path;
                    return true;
                }
            }

            assetPath = null;
            return false;
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string GenerateAssetScript(Type masterType, string assetClassName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// This file is auto-generated by MasterAssetGenerator. Do not modify this file manually.");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using StickerFwk.Core.MasterData;");

            var sourceNamespace = masterType.Namespace;
            var hasNamespace = !string.IsNullOrEmpty(sourceNamespace);
            if (hasNamespace)
            {
                sb.AppendLine($"using {sourceNamespace};");
            }

            sb.AppendLine();

            var targetNamespace = hasNamespace ? sourceNamespace + ".ScriptableObjects" : null;
            var indent = string.Empty;
            if (targetNamespace != null)
            {
                sb.AppendLine($"namespace {targetNamespace}");
                sb.AppendLine("{");
                indent = "    ";
            }

            sb.AppendLine($"{indent}[CreateAssetMenu(fileName = \"{assetClassName}\", menuName = \"{MenuPathPrefix}/{assetClassName}\", order = 0)]");
            sb.AppendLine($"{indent}public class {assetClassName} : MasterAsset<{masterType.Name}>");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}}}");

            if (targetNamespace != null)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
    }
}
