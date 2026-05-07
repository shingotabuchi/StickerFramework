using System.IO;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor.AssetTools
{
    public static class MaterialToPngExporter
    {
        private const string MenuRoot = "Assets/Export Material to PNG/";

        [MenuItem(MenuRoot + "512", priority = 2000)]
        private static void Export512()
        {
            ExportSelected(512);
        }

        [MenuItem(MenuRoot + "1024", priority = 2001)]
        private static void Export1024()
        {
            ExportSelected(1024);
        }

        [MenuItem(MenuRoot + "2048", priority = 2002)]
        private static void Export2048()
        {
            ExportSelected(2048);
        }

        [MenuItem(MenuRoot + "512", validate = true)]
        [MenuItem(MenuRoot + "1024", validate = true)]
        [MenuItem(MenuRoot + "2048", validate = true)]
        private static bool ValidateExport()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Material)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ExportSelected(int size)
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Material mat)
                {
                    Export(mat, size);
                }
            }
        }

        private static void Export(Material material, int size)
        {
            var assetPath = AssetDatabase.GetAssetPath(material);
            var defaultDirectory = string.IsNullOrEmpty(assetPath)
                ? "Assets"
                : Path.GetDirectoryName(assetPath);
            var defaultName = $"{material.name}_{size}";

            var savePath = EditorUtility.SaveFilePanel(
                "Export Material to PNG",
                defaultDirectory,
                defaultName,
                "png");

            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            var bytes = RenderMaterialToPng(material, size);
            File.WriteAllBytes(savePath, bytes);

            if (savePath.StartsWith(Application.dataPath))
            {
                AssetDatabase.Refresh();
            }

            Debug.Log($"Exported '{material.name}' ({size}x{size}) to: {savePath}", material);
        }

        private static byte[] RenderMaterialToPng(Material material, int size)
        {
            var descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = true,
            };

            var rt = RenderTexture.GetTemporary(descriptor);
            var previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(Texture2D.whiteTexture, rt, material);

                RenderTexture.active = rt;
                var readback = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
                readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                readback.Apply();

                var png = readback.EncodeToPNG();
                Object.DestroyImmediate(readback);
                return png;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
