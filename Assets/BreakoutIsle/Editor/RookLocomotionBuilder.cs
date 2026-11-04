using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using BreakoutIsle.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreakoutIsle.Editor
{
    public static class RookLocomotionBuilder
    {
        private const string CharacterArtPath = "Assets/BreakoutIsle/Art/Characters/RookPrison";
        private const string OutputPath = "Assets/BreakoutIsle/Animations/RookPrison";
        private const string PrefabPath = "Assets/BreakoutIsle/Prefabs/Characters/RookPlayer.prefab";
        private const string InputPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [Serializable]
        private sealed class ActionManifest
        {
            public string action;
            public string sheet;
            public string[] directionOrder;
            public int frameCount;
            public int fps;
            public bool loop;
        }

        [MenuItem("Breakout Isle/Characters/Build Rook Animations and Prefab")]
        public static void Build()
        {
            var locomotionActionNames = new[] { "idle", "walk", "run", "sneak" };
            var actionNames = new[] { "interact", "pry", "crawl", "hide-enter", "hide-exit", "caught", "climb", "throw" };
            var clipsByAction = new Dictionary<string, Dictionary<string, AnimationClip>>();
            var actionDefinitions = new List<PlayerActionAnimator.ActionDefinition>();
            var clipsFolder = OutputPath + "/Clips";
            EnsureFolder("Assets/BreakoutIsle/Animations");
            EnsureFolder(OutputPath);
            EnsureFolder(clipsFolder);

            foreach (var actionName in locomotionActionNames)
            {
                var manifest = ReadManifest(actionName);
                clipsByAction.Add(actionName, BuildAction(actionName, clipsFolder, manifest));
            }
            foreach (var actionName in actionNames)
            {
                var manifest = ReadManifest(actionName);
                clipsByAction.Add(actionName, BuildAction(actionName, clipsFolder, manifest));
                actionDefinitions.Add(new PlayerActionAnimator.ActionDefinition
                {
                    action = actionName,
                    frameCount = manifest.frameCount,
                    fps = manifest.fps,
                    loop = manifest.loop
                });
            }

            var controllerPath = OutputPath + "/RookPrison.controller";
            var controller = BuildController(controllerPath, clipsByAction, actionNames);
            BuildPlayerPrefab(controller, LoadIdleSouthSprite(), actionDefinitions.ToArray());
            RookCivilianAnimationBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Updated Rook's prison and civilian animation controllers in place and saved the player prefab at {PrefabPath}.");
        }

        private static ActionManifest ReadManifest(string actionName)
        {
            var actionFolder = CharacterArtPath + "/" + actionName;
            var manifestPath = actionFolder + "/unity-import-settings.json";
            var manifest = JsonUtility.FromJson<ActionManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.action != actionName || manifest.directionOrder == null || manifest.frameCount < 1 || manifest.fps < 1)
                throw new InvalidDataException($"Invalid animation manifest: {manifestPath}");
            return manifest;
        }

        private static Dictionary<string, AnimationClip> BuildAction(string actionName, string clipsFolder, ActionManifest manifest)
        {
            var actionFolder = CharacterArtPath + "/" + actionName;

            var sheetPath = actionFolder + "/" + manifest.sheet;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            var spritesByName = new Dictionary<string, Sprite>();
            foreach (var asset in sprites)
            {
                if (asset is Sprite sprite)
                    spritesByName.Add(sprite.name, sprite);
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var direction in manifest.directionOrder)
            {
                var frames = new ObjectReferenceKeyframe[manifest.frameCount + 1];
                for (var frame = 0; frame < manifest.frameCount; frame++)
                {
                    var spriteName = $"{actionName}_{direction}_{frame:00}";
                    if (!spritesByName.TryGetValue(spriteName, out var sprite))
                        throw new InvalidDataException($"Sprite '{spriteName}' is missing from '{sheetPath}'. Reimport the sheet from its manifest.");

                    frames[frame] = new ObjectReferenceKeyframe
                    {
                        time = (float)frame / manifest.fps,
                        value = sprite
                    };
                }

                frames[manifest.frameCount] = new ObjectReferenceKeyframe
                {
                    time = (float)manifest.frameCount / manifest.fps,
                    value = frames[manifest.frameCount - 1].value
                };

                var generated = new AnimationClip
                {
                    name = $"{actionName}_{direction}",
                    frameRate = manifest.fps,
                    wrapMode = manifest.loop ? WrapMode.Loop : WrapMode.Once
                };
                var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(generated, binding, frames);
                var settings = AnimationUtility.GetAnimationClipSettings(generated);
                settings.loopTime = manifest.loop;
                AnimationUtility.SetAnimationClipSettings(generated, settings);
                var clip = StoreClipWithoutReplacingGuid(generated, clipsFolder + "/" + generated.name + ".anim");
                clips.Add(direction, clip);
            }

            return clips;
        }

        private static AnimatorController BuildController(string controllerPath, Dictionary<string, Dictionary<string, AnimationClip>> clipsByAction, string[] actionNames)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var child in stateMachine.states)
                stateMachine.RemoveState(child.state);
            stateMachine.defaultState = null;
            for (var i = controller.parameters.Length - 1; i >= 0; i--)
                controller.RemoveParameter(i);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
                if (asset is BlendTree oldTree)
                    UnityEngine.Object.DestroyImmediate(oldTree, true);

            controller.AddParameter("MoveMode", AnimatorControllerParameterType.Float);
            controller.AddParameter("FacingX", AnimatorControllerParameterType.Float);
            controller.AddParameter("FacingY", AnimatorControllerParameterType.Float);

            var locomotion = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "MoveMode",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(locomotion, controller);

            AddDirectionalTree(controller, locomotion, "idle", clipsByAction["idle"], 0f);
            AddDirectionalTree(controller, locomotion, "walk", clipsByAction["walk"], 1f);
            AddDirectionalTree(controller, locomotion, "run", clipsByAction["run"], 2f);
            AddDirectionalTree(controller, locomotion, "sneak", clipsByAction["sneak"], 3f);

            var state = stateMachine.AddState("Rook Locomotion");
            state.motion = locomotion;
            stateMachine.defaultState = state;
            foreach (var actionName in actionNames)
            {
                foreach (var pair in clipsByAction[actionName])
                {
                    var actionState = stateMachine.AddState(PlayerActionAnimator.StateName(actionName, pair.Key));
                    actionState.motion = pair.Value;
                }
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip StoreClipWithoutReplacingGuid(AnimationClip generated, string path)
        {
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

        private static Sprite LoadIdleSouthSprite()
        {
            var sheetPath = CharacterArtPath + "/idle/idle.png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (asset is Sprite sprite && sprite.name == "idle_S_00")
                    return sprite;
            }

            throw new InvalidDataException($"Sprite 'idle_S_00' is missing from '{sheetPath}'. Reimport the sheet from its manifest.");
        }

        private static void AddDirectionalTree(AnimatorController controller, BlendTree locomotion, string actionName, Dictionary<string, AnimationClip> clips, float threshold)
        {
            var directional = new BlendTree
            {
                name = actionName + " 8 Directions",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "FacingX",
                blendParameterY = "FacingY",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(directional, controller);
            foreach (var pair in clips)
                directional.AddChild(pair.Value, DirectionVector(pair.Key));
            locomotion.AddChild(directional, threshold);
        }

        private static Vector2 DirectionVector(string direction)
        {
            const float diagonal = 0.70710678f;
            switch (direction)
            {
                case "S": return new Vector2(0f, -1f);
                case "SW": return new Vector2(-diagonal, -diagonal);
                case "W": return new Vector2(-1f, 0f);
                case "NW": return new Vector2(-diagonal, diagonal);
                case "N": return new Vector2(0f, 1f);
                case "NE": return new Vector2(diagonal, diagonal);
                case "E": return new Vector2(1f, 0f);
                case "SE": return new Vector2(diagonal, -diagonal);
                default: throw new InvalidDataException($"Unknown Rook direction '{direction}'.");
            }
        }

        private static void BuildPlayerPrefab(AnimatorController controller, Sprite idleSouth, PlayerActionAnimator.ActionDefinition[] actionDefinitions)
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputActions == null)
                throw new FileNotFoundException($"Input actions asset is missing: {InputPath}");

            EnsureFolder("Assets/BreakoutIsle/Prefabs");
            EnsureFolder("Assets/BreakoutIsle/Prefabs/Characters");
            var player = new GameObject("Rook");
            player.SetActive(false);
            player.layer = LayerMask.NameToLayer("Player");

            var renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = idleSouth;
            renderer.sortingLayerName = "Actors";
            player.AddComponent<YSortRenderer>();

            var animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.48f, 0.62f);
            collider.offset = new Vector2(0f, 0.32f);

            var inputReader = player.AddComponent<PlayerInputReader>();
            var serializedInput = new SerializedObject(inputReader);
            serializedInput.FindProperty("inputActions").objectReferenceValue = inputActions;
            serializedInput.ApplyModifiedPropertiesWithoutUndo();
            var motor = player.AddComponent<PlayerMotor>();
            var actionAnimator = player.AddComponent<PlayerActionAnimator>();
            actionAnimator.Configure(animator, motor, inputReader, actionDefinitions);
            EditorUtility.SetDirty(actionAnimator);

            player.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
        }

        private static void EnsureFolder(string path)
        {
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) || AssetDatabase.IsValidFolder(path))
                return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
