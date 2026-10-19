using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Editor
{
    public static class CarPreviewFilteringVerification
    {
        public static void Run()
        {
            int checks = 0; var report = new StringBuilder();
            var panel = Resources.Load<PanelSettings>("UI/NativePanel");
            foreach (string path in Directory.GetFiles("Assets/DragLab/Resources/Sprites/Vehicles/roster", "*.png", SearchOption.AllDirectories)) {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear) importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); var texture = sprite.texture;
                if (texture.mipmapCount <= 1 || texture.filterMode != FilterMode.Trilinear || importer.textureCompression != TextureImporterCompression.Uncompressed)
                    throw new InvalidOperationException("Missing filtered mip chain: " + path);
                checks++; report.AppendLine("PASS: filtered uncompressed mip chain / " + path);
                // Dynamic atlases have no mip chain. These layered car images must
                // continue to sample their original textures in the native panel.
                if ((panel.dynamicAtlasSettings.activeFilters & DynamicAtlasFilters.Size) == 0 ||
                    Math.Max(texture.width, texture.height) <= panel.dynamicAtlasSettings.maxSubTextureSize)
                    throw new InvalidOperationException("Car texture can enter the non-mipmapped dynamic atlas: " + path);
                checks++; report.AppendLine("PASS: retained outside dynamic atlas / " + path);
            }
            if (checks != 400) throw new InvalidOperationException("Expected 200 roster textures.");
            Directory.CreateDirectory("../reports/car-antialiasing");
            File.WriteAllText("../reports/car-antialiasing/verification.txt", report + "Checks: " + checks + " / failures: 0\n", new UTF8Encoding(false));
            Debug.Log("Car preview filtering: " + checks + " checks passed.");
        }
    }
}
