using UnityEditor;
using UnityEngine;

namespace DragLab.Editor
{
    public sealed class SpriteImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/DragLab/Resources/Sprites/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.alphaIsTransparency = true;
            // Car artwork is also shown at thumbnail size. Bilinear sampling of the
            // full-size image skips thin outlines/spokes when reducing it 4–8 times.
            bool roster = assetPath.StartsWith("Assets/DragLab/Resources/Sprites/Vehicles/roster/");
            importer.mipmapEnabled = roster;
            importer.filterMode = roster ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
