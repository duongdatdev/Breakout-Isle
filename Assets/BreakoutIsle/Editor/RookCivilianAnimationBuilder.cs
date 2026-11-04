using System;
using System.Collections.Generic;
using System.IO;
using BreakoutIsle.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BreakoutIsle.Editor
{
    public static class RookCivilianAnimationBuilder
    {
        private const string CivilianArtPath = "Assets/BreakoutIsle/Art/Characters/RookCivilian";
        private const string PrisonClipPath = "Assets/BreakoutIsle/Animations/RookPrison/Clips";
        private const string OutputPath = "Assets/BreakoutIsle/Animations/RookCivilian";
        private const string PrisonControllerPath = "Assets/BreakoutIsle/Animations/RookPrison/RookPrison.controller";
        private const string OverrideControllerPath = OutputPath + "/RookCivilian.overrideController";
        private const string PrefabPath = "Assets/BreakoutIsle/Prefabs/Characters/RookPlayer.prefab";

        private static readonly string[] Directions = { "S", "SW", "W", "NW", "N", "NE", "E", "SE" };
        private static readonly string[] CivilianActions =
        {
            "idle", "walk", "run", "sneak", "interact", "throw", "hide-enter", "hide-exit", "caught"
        };

        [Serializable]
        private sealed class ActionManifest
        {
            public string action;
            public string sheet;
            public string[] directionOrder;
            public int frameCount;
            public int fps;
            public bool loop;
            public int cellSizePx;
            public int pixelsPerUnit;
            public Pivot pivot;
        }

        [Serializable]
        private struct Pivot
        {
            public float x;
            public float y;
        }

        [MenuItem("Breakout Isle/Characters/Build Civilian Outfit Animations")]
        public static void Build()
        {
            EnsureFolder("Assets/BreakoutIsle/Animations");
            EnsureFolder(OutputPath);
            var prisonController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PrisonControllerPath);
            if (prisonController == null)
                throw new InvalidDataException($"Prison animation controller is missing: {PrisonControllerPath}");

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var action in CivilianActions)
            {
                var manifest = ReadAndValidateManifest(action);
                BuildCivilianClips(manifest, overrides);
            }

            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverrideControllerPath);
            if (overrideController == null)
            {
                overrideController = new AnimatorOverrideController();
                AssetDatabase.CreateAsset(overrideController, OverrideControllerPath);
            }

            overrideController.runtimeAnimatorController = prisonController;
            overrideController.ApplyOverrides(overrides);
            EditorUtility.SetDirty(overrideController);
            ConfigurePlayerPrefab(prisonController, overrideController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built the civilian Rook override controller. Pry, crawl, and climb remain explicitly bound to prison animations.");
        }

        private static ActionManifest ReadAndValidateManifest(string action)
        {
            var actionFolder = CivilianArtPath + "/" + action;
            var path = actionFolder + "/unity-import-settings.json";
            if (!File.Exists(path)) throw new FileNotFoundException($"Civilian outfit manifest is missing: {path}");
            var manifest = JsonUtility.FromJson<ActionManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.action != action || manifest.frameCount < 1 || manifest.fps < 1 ||
                manifest.sheet != action + ".png" || manifest.cellSizePx != 64 || manifest.pixelsPerUnit != 32 ||
                manifest.pivot.x != 0.5f || Mathf.Abs(manifest.pivot.y - 0.0625f) > 0.00001f ||
                manifest.directionOrder == null || manifest.directionOrder.Length != Directions.Length)
                throw new InvalidDataException($"Invalid civilian animation manifest: {path}");

            for (var i = 0; i < Directions.Length; i++)
                if (manifest.directionOrder[i] != Directions[i])
                    throw new InvalidDataException($"Unexpected direction order in {path}; expected the canonical eight directions.");
            return manifest;
        }

        private static void BuildCivilianClips(ActionManifest manifest,
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides)
        {
            var sheetPath = CivilianArtPath + "/" + manifest.action + "/" + manifest.sheet;
            var spritesByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
                if (asset is Sprite sprite) spritesByName.Add(sprite.name, sprite);

            for (var directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                var direction = Directions[directionIndex];
                var keys = new ObjectReferenceKeyframe[manifest.frameCount + 1];
                for (var frame = 0; frame < manifest.frameCount; frame++)
                {
                    var spriteName = $"{manifest.action}_{direction}_{frame:00}";
                    if (!spritesByName.TryGetValue(spriteName, out var sprite))
                        throw new InvalidDataException($"Civilian sprite '{spriteName}' is missing from '{sheetPath}'.");
                    keys[frame] = new ObjectReferenceKeyframe { time = frame / (float)manifest.fps, value = sprite };
                }
                keys[manifest.frameCount] = new ObjectReferenceKeyframe
                {
                    time = manifest.frameCount / (float)manifest.fps,
                    value = keys[manifest.frameCount - 1].value
                };

                var clipName = manifest.action + "_" + direction;
                var generated = new AnimationClip
                {
                    name = clipName,
                    frameRate = manifest.fps,
                    wrapMode = manifest.loop ? WrapMode.Loop : WrapMode.Once
                };
                AnimationUtility.SetObjectReferenceCurve(generated,
                    EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
                var clipSettings = AnimationUtility.GetAnimationClipSettings(generated);
                clipSettings.loopTime = manifest.loop;
                AnimationUtility.SetAnimationClipSettings(generated, clipSettings);

                var outputClipPath = OutputPath + "/Clips/" + clipName + ".anim";
                var civilianClip = StoreClipWithoutReplacingGuid(generated, outputClipPath);
                var prisonClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PrisonClipPath + "/" + clipName + ".anim");
                if (prisonClip == null)
                    throw new InvalidDataException($"Prison base clip is missing for civilian override: {clipName}");
                overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(prisonClip, civilianClip));
            }
        }

        private static AnimationClip StoreClipWithoutReplacingGuid(AnimationClip generated, string path)
        {
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(folder);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);
            return existing;
        }

        private static void ConfigurePlayerPrefab(AnimatorController prisonController,
            AnimatorOverrideController civilianController)
        {
            if (!File.Exists(PrefabPath)) throw new FileNotFoundException($"Rook player prefab is missing: {PrefabPath}");
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = prefabRoot.GetComponent<Animator>();
                if (animator == null) throw new InvalidDataException("Rook player prefab has no Animator component.");
                var outfitAnimator = prefabRoot.GetComponent<PlayerOutfitAnimator>();
                if (outfitAnimator == null) outfitAnimator = prefabRoot.AddComponent<PlayerOutfitAnimator>();
                outfitAnimator.Configure(animator, prisonController, civilianController);
                EditorUtility.SetDirty(outfitAnimator);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
