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

        [Serializable]
        private sealed class PropsManifest
        {
            public PropEntry[] props;
        }

        [Serializable]
        private sealed class PropEntry
        {
            public string file;
            public int[] size;
            public int pixelsPerUnit;
            public float[] pivot;
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

            const string campaignTilesRoot = "Assets/BreakoutIsle/Art/Environment/";
            if ((normalizedPath.StartsWith(campaignTilesRoot + "Forest/Tiles/", StringComparison.Ordinal) ||
                 normalizedPath.StartsWith(campaignTilesRoot + "City/Tiles/", StringComparison.Ordinal)) &&
                Path.GetExtension(normalizedPath) == ".png")
            {
                ApplyTileSettings(importer, Path.GetFileName(normalizedPath).EndsWith("-atlas.png", StringComparison.Ordinal));
                return;
            }

            const string environmentRoot = "Assets/BreakoutIsle/Art/Environment/";
            const string propsSegment = "/Props/";
            var propsSegmentIndex = normalizedPath.IndexOf(propsSegment, StringComparison.Ordinal);
            if (normalizedPath.StartsWith(environmentRoot, StringComparison.Ordinal) &&
                propsSegmentIndex >= environmentRoot.Length && Path.GetExtension(normalizedPath) == ".png")
            {
                var propsRoot = normalizedPath.Substring(0, propsSegmentIndex + propsSegment.Length);
                ApplyPropSettings(importer, normalizedPath, propsRoot);
                return;
            }

            const string inventoryIconsPath = "Assets/BreakoutIsle/Art/UI/Icons/";
            if (normalizedPath.StartsWith(inventoryIconsPath, StringComparison.Ordinal) && Path.GetExtension(normalizedPath) == ".png")
            {
                ApplyIconSettings(importer);
                return;
            }

            const string uiSkinsPath = "Assets/BreakoutIsle/Art/UI/Skins/";
            if (normalizedPath.StartsWith(uiSkinsPath, StringComparison.Ordinal) && Path.GetExtension(normalizedPath) == ".png")
            {
                ApplySkinSettings(importer);
                return;
            }

            const string menuArtPath = "Assets/BreakoutIsle/Art/UI/Menu/";
            if (normalizedPath.StartsWith(menuArtPath, StringComparison.Ordinal) && Path.GetExtension(normalizedPath) == ".png")
            {
                ApplyIconSettings(importer);
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
            SetSpritePivot(importer, manifest.pivot.x, manifest.pivot.y);
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

        private static void ApplyPropSettings(TextureImporter importer, string normalizedPath, string propsRoot)
        {
            var manifestPath = Path.Combine(propsRoot, "props.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"Prop texture '{normalizedPath}' has no manifest at '{manifestPath}'.");
                return;
            }
            var manifest = JsonUtility.FromJson<PropsManifest>(File.ReadAllText(manifestPath));
            PropEntry entry = null;
            var relativeFile = normalizedPath.Substring(propsRoot.Length).Replace('\\', '/');
            if (manifest != null && manifest.props != null)
            {
                foreach (var prop in manifest.props)
                {
                    if (prop != null && prop.file != null && prop.file.Replace('\\', '/') == relativeFile)
                    {
                        entry = prop;
                        break;
                    }
                }
            }

            if (entry == null || entry.size == null || entry.size.Length != 2 || entry.pivot == null || entry.pivot.Length != 2 || entry.pixelsPerUnit < 1)
            {
                Debug.LogError($"Prop texture '{normalizedPath}' has no valid entry in '{manifestPath}'.");
                return;
            }

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            if (width != entry.size[0] || height != entry.size[1])
            {
                Debug.LogError($"Prop texture '{normalizedPath}' is {width}x{height}; its manifest expects {entry.size[0]}x{entry.size[1]}.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = entry.pixelsPerUnit;
            SetSpritePivot(importer, entry.pivot[0], entry.pivot[1]);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private static void ApplyIconSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            SetSpritePivot(importer, 0.5f, 0.5f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private static void ApplySkinSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            SetSpritePivot(importer, 0.5f, 0.5f, new Vector4(8f, 8f, 8f, 8f));
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private static void SetSpritePivot(TextureImporter importer, float x, float y, Vector4? border = null)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(x, y);
            if (border.HasValue)
                settings.spriteBorder = border.Value;
            importer.SetTextureSettings(settings);
        }
    }
}
