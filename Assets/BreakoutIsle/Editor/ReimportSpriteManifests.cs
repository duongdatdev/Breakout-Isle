using System.IO;
using UnityEditor;
using UnityEngine;

namespace BreakoutIsle.Editor
{
    public static class ReimportSpriteManifests
    {
        [MenuItem("Breakout Isle/Art/Reimport Manifest Sheets")]
        public static void ReimportAll()
        {
            var manifests = Directory.GetFiles(Application.dataPath, "unity-import-settings.json", SearchOption.AllDirectories);
            foreach (var manifestPath in manifests)
            {
                var manifest = JsonUtility.FromJson<ManifestSheet>(File.ReadAllText(manifestPath));
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.sheet))
                    continue;

                var directory = Path.GetDirectoryName(manifestPath);
                var assetPath = Path.Combine(directory ?? Application.dataPath, manifest.sheet)
                    .Replace(Application.dataPath, "Assets")
                    .Replace('\\', '/');
                if (File.Exists(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length))))
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var tilesPath = Path.Combine(Application.dataPath, "BreakoutIsle", "Art", "Environment", "Prison", "Tiles");
            if (Directory.Exists(tilesPath))
            {
                foreach (var texturePath in Directory.GetFiles(tilesPath, "*.png", SearchOption.TopDirectoryOnly))
                {
                    var assetPath = "Assets/BreakoutIsle/Art/Environment/Prison/Tiles/" + Path.GetFileName(texturePath);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        [System.Serializable]
        private sealed class ManifestSheet
        {
            public string sheet;
        }
    }
}
