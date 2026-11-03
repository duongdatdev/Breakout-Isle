using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BreakoutIsle.Editor
{
    public sealed class GeneratedSpritePostprocessor : AssetPostprocessor
    {
        [Serializable]
        private sealed class ImportManifest
        {
            public string action;
            public string sheet;
            public string[] directionOrder;
            public int frameCount;
            public int cellSizePx;
            public int pixelsPerUnit;
            public Pivot pivot;
        }

        [Serializable]
        private sealed class Pivot
        {
            public float x;
            public float y;
        }

        private void OnPreprocessTexture()
        {
            var importer = assetImporter as TextureImporter;
            if (importer == null)
                return;

            var normalizedPath = assetPath.Replace('\\', '/');
            const string prisonTilesPath = "Assets/BreakoutIsle/Art/Environment/Prison/Tiles/";
            if (normalizedPath.StartsWith(prisonTilesPath, StringComparison.Ordinal) && Path.GetExtension(normalizedPath) == ".png")
            {
                ApplyTileSettings(importer, Path.GetFileName(normalizedPath) == "prison-tiles-atlas.png");
                return;
            }

            var manifestPath = Path.Combine(Path.GetDirectoryName(assetPath) ?? string.Empty, "unity-import-settings.json");
            if (!File.Exists(manifestPath))
                return;

            var manifest = JsonUtility.FromJson<ImportManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.sheet != Path.GetFileName(assetPath) ||
                string.IsNullOrWhiteSpace(manifest.action) || manifest.directionOrder == null || manifest.directionOrder.Length == 0 ||
                manifest.frameCount < 1 || manifest.cellSizePx < 1 || manifest.pixelsPerUnit < 1 || manifest.pivot == null)
            {
                Debug.LogError($"Invalid sprite import manifest for '{assetPath}': {manifestPath}");
                return;
            }

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var expectedWidth = manifest.frameCount * manifest.cellSizePx;
            var expectedHeight = manifest.directionOrder.Length * manifest.cellSizePx;
            if (width != expectedWidth || height != expectedHeight)
            {
                Debug.LogError($"Sprite sheet '{assetPath}' is {width}x{height}; its manifest expects {expectedWidth}x{expectedHeight}.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = manifest.pixelsPerUnit;
            importer.spritePivot = new Vector2(manifest.pivot.x, manifest.pivot.y);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
#pragma warning disable 0618
            var sprites = new SpriteMetaData[manifest.directionOrder.Length * manifest.frameCount];
            var index = 0;
            for (var row = 0; row < manifest.directionOrder.Length; row++)
            {
                for (var frame = 0; frame < manifest.frameCount; frame++)
                {
                    sprites[index++] = new SpriteMetaData
                    {
                        name = $"{manifest.action}_{manifest.directionOrder[row]}_{frame:00}",
                        rect = new Rect(frame * manifest.cellSizePx, height - (row + 1) * manifest.cellSizePx, manifest.cellSizePx, manifest.cellSizePx),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(manifest.pivot.x, manifest.pivot.y)
                    };
                }
            }

            importer.spritesheet = sprites;
#pragma warning restore 0618
        }

        private static void ApplyTileSettings(TextureImporter importer, bool isAtlas)
        {
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (isAtlas)
            {
                importer.textureType = TextureImporterType.Default;
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
        }
    }
}
