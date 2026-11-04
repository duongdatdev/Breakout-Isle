using System.Collections.Generic;
using BreakoutIsle.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using UnityEditor.Animations;

namespace BreakoutIsle.Editor
{
    public static class Level01SceneBuilder
    {
        public static string LastTileReport = string.Empty;
        private const string Root = "Assets/BreakoutIsle";
        private const string ScenePath = Root + "/Scenes/Level01_Prison.unity";
        private const string TileRoot = Root + "/Tiles/Prison";
        private const string ObjectivePath = Root + "/Data/Level01_Objectives.asset";
        private const string CampaignPath = Root + "/Data/CampaignLevel01.asset";

        [MenuItem("Breakout Isle/Scenes/Build Level 01 Prison")]
        public static void Build()
        {
            EnsureFolder(Root + "/Tiles");
            EnsureFolder(TileRoot);
            EnsureFolder(Root + "/Data");
            GetTile("floor_clean");
            GetTile("floor_cracked");
            GetTile("floor_dark");
            GetTile("floor_drain");
            GetTile("floor_warning");
            GetTile("wall_clean", true);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floorClean = GetTile("floor_clean");
            var floorCracked = GetTile("floor_cracked");
            var floorDark = GetTile("floor_dark");
            var floorDrain = GetTile("floor_drain");
            var floorWarning = GetTile("floor_warning");
            var wall = GetTile("wall_clean", true);
            var cellDividerTile = GetTile("boundary_right", true);
            var globalLight = new GameObject("Global 2D Light", typeof(Light2D)).GetComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;
            globalLight.color = Color.white;
            var gridObject = new GameObject("Prison Tile Grid", typeof(Grid));
            var grid = gridObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;
            var floorObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            floorObject.transform.SetParent(gridObject.transform, false);
            floorObject.layer = LayerMask.NameToLayer("World");
            var floorMap = floorObject.GetComponent<Tilemap>();
            var floorRenderer = floorObject.GetComponent<TilemapRenderer>();
            floorRenderer.sortingLayerName = "Ground";
            floorRenderer.sortingOrder = -100;
            var wallObject = new GameObject("Walls and sight blockers", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
            wallObject.transform.SetParent(gridObject.transform, false);
            wallObject.layer = LayerMask.NameToLayer("Vision");
            var wallMap = wallObject.GetComponent<Tilemap>();
            wallMap.GetComponent<TilemapRenderer>().sortingLayerName = "Actors";
            wallMap.GetComponent<TilemapRenderer>().sortingOrder = -500;
            var walkable = new HashSet<Vector3Int>();
            for (var y = 1; y <= 22; y++)
            for (var x = 1; x <= 38; x++)
            {
                if (!IsFloor(x, y)) continue;
                var p = new Vector3Int(x, y, 0);
                walkable.Add(p);
                var tile = floorClean;
                if ((x * 13 + y * 7) % 17 == 0) tile = floorCracked;
                if (x >= 9 && x <= 17 && y >= 2 && y <= 5) tile = floorDark;
                if (x == 11 && y == 8 || x == 18 && y == 8) tile = floorDrain;
                if (x == 27 && y == 12) tile = floorWarning;
                floorMap.SetTile(p, tile);
            }

            LastTileReport = $"cells={walkable.Count}, renderedCells={floorMap.GetUsedTilesCount()}, tilePresent={floorClean != null}, spritePresent={(floorClean != null && floorClean.sprite != null)}";
            Debug.Log("Level01 tile fill: " + LastTileReport);

            foreach (var p in walkable)
            foreach (var offset in Cardinal)
            {
                var neighbour = p + offset;
                if (!walkable.Contains(neighbour) && neighbour.x >= 0 && neighbour.x <= 39 && neighbour.y >= 0 && neighbour.y <= 23)
                    wallMap.SetTile(neighbour, wall);
            }
            // Keep the cell and lower dark passage separated except for the vent doorway at row 8.
            // The authored wall tiles carry both the visible boundary and the Vision-layer collider.
            for (var y = 2; y <= 6; y++)
                wallMap.SetTile(new Vector3Int(9, y, 0), cellDividerTile);
            var dividerCell = wallMap.GetTile(new Vector3Int(9, 6, 0));
            LastTileReport += ", dividerTile=" + (cellDividerTile == null ? "NULL" : cellDividerTile.name) + ", dividerCell=" + (dividerCell == null ? "NULL" : dividerCell.name);
            Debug.Log("Level01 divider fill: " + LastTileReport);
            floorMap.RefreshAllTiles();
            wallMap.RefreshAllTiles();
            EditorUtility.SetDirty(floorMap);
            EditorUtility.SetDirty(wallMap);

            var objectives = CreateObjectives();
            RookLocomotionBuilder.Build();
            var guardController = GuardAnimationBuilder.Build();
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Characters/RookPlayer.prefab");
            if (playerPrefab == null) throw new System.InvalidOperationException("Build Rook locomotion prefab first.");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Rook - Player";
            player.transform.position = new Vector3(4.5f, 4.5f, 0);
            if (player.GetComponent<PlayerStealthState>() == null) player.AddComponent<PlayerStealthState>();
            if (player.GetComponent<PlayerStealthZone>() == null) player.AddComponent<PlayerStealthZone>();
            var sessionObject = new GameObject("Level Session");
            var session = sessionObject.AddComponent<LevelSession>();
            var sessionSerialized = new SerializedObject(session);
            sessionSerialized.FindProperty("objectives").objectReferenceValue = objectives;
            sessionSerialized.ApplyModifiedPropertiesWithoutUndo();
            var interactor = player.AddComponent<PlayerInteractor>();
            var interactorSerialized = new SerializedObject(interactor);
            interactorSerialized.FindProperty("session").objectReferenceValue = session;
            interactorSerialized.FindProperty("interactionMask").intValue = ~0;
            interactorSerialized.ApplyModifiedPropertiesWithoutUndo();
            var actionAnimator = player.GetComponent<PlayerActionAnimator>();
            if (actionAnimator == null) throw new System.InvalidOperationException("Rook player action animator prefab was not rebuilt.");

            var safeSpawn = new GameObject("Safe Spawn - Cell");
            safeSpawn.transform.position = player.transform.position;
            var anchors = new[]
            {
                MakeCheckpoint("cell-start", safeSpawn.transform, player.transform.position),
                MakeCheckpoint("after-vent", null, new Vector2(11f, 6f)),
                MakeCheckpoint("after-keycard", null, new Vector2(25f, 16f))
            };
            var coordinator = sessionObject.AddComponent<CheckpointSaveCoordinator>();
            var coordinatorSerialized = new SerializedObject(coordinator);
            coordinatorSerialized.FindProperty("levelId").stringValue = "prison-01";
            coordinatorSerialized.FindProperty("initialCheckpointId").stringValue = "cell-start";
            coordinatorSerialized.FindProperty("session").objectReferenceValue = session;
            coordinatorSerialized.FindProperty("playerAdapterComponent").objectReferenceValue = player.GetComponent<PlayerMotor>();
            coordinatorSerialized.FindProperty("checkpointAnchors").arraySize = anchors.Length;
            for (var i = 0; i < anchors.Length; i++)
                coordinatorSerialized.FindProperty("checkpointAnchors").GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
            coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            var campaignLevel = AssetDatabase.LoadAssetAtPath<CampaignLevelDefinition>(CampaignPath);
            if (campaignLevel == null) throw new System.InvalidOperationException("Missing CampaignLevel01 definition.");
            var campaignTransition = sessionObject.AddComponent<CampaignTransitionService>();
            var campaignSerialized = new SerializedObject(campaignTransition);
            campaignSerialized.FindProperty("session").objectReferenceValue = session;
            campaignSerialized.FindProperty("saveCoordinator").objectReferenceValue = coordinator;
            campaignSerialized.FindProperty("currentLevel").objectReferenceValue = campaignLevel;
            campaignSerialized.ApplyModifiedPropertiesWithoutUndo();
            var checkpointRelay = sessionObject.AddComponent<ObjectiveCheckpointRelay>();
            var relaySerialized = new SerializedObject(checkpointRelay);
            relaySerialized.FindProperty("session").objectReferenceValue = session;
            relaySerialized.FindProperty("coordinator").objectReferenceValue = coordinator;
            var checkpoints = relaySerialized.FindProperty("checkpoints");
            checkpoints.arraySize = 2;
            checkpoints.GetArrayElementAtIndex(0).FindPropertyRelative("objectiveId").stringValue = "open-vent";
            checkpoints.GetArrayElementAtIndex(0).FindPropertyRelative("checkpointId").stringValue = "after-vent";
            checkpoints.GetArrayElementAtIndex(1).FindPropertyRelative("objectiveId").stringValue = "find-keycard";
            checkpoints.GetArrayElementAtIndex(1).FindPropertyRelative("checkpointId").stringValue = "after-keycard";
            relaySerialized.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(PixelCameraFollow));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(4.5f, 4.5f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            var cameraFollow = new SerializedObject(cameraObject.GetComponent<PixelCameraFollow>());
            cameraFollow.FindProperty("target").objectReferenceValue = player.transform;
            cameraFollow.ApplyModifiedPropertiesWithoutUndo();
            var supportProjectile = SupportItemSceneBuilder.BuildProjectilePrefab();
            var supportItems = SupportItemSceneBuilder.ConfigurePlayer(player, session, camera,
                (1 << LayerMask.NameToLayer("Vision")) | (1 << LayerMask.NameToLayer("Hiding")), supportProjectile);
            actionAnimator.BindGameplayEvents(interactor, session, player.GetComponent<PlayerStealthState>(), supportItems);

            MakeProp("Cell Bed", "bed", new Vector2(5.5f, 7.1f), true);
            var pillow = MakeProp("Hidden Spoon Pillow", "pillow", new Vector2(6.1f, 7.9f), false);
            AddPickup(pillow, "pillow-spoon", "spoon", "find-spoon", "Inspect pillow");
            var vent = MakeProp("Floor Vent", "vent_00", new Vector2(7.0f, 8.0f), false);
            AddAction(vent, "vent-access", "open-vent", "Pry open the vent");
            var ventGate = MakeGate("Vent Route Gate", new Vector2(8.5f, 8.0f), "open-vent", true);
            ventGate.gameObject.AddComponent<VentCrawlTrigger>();
            var crawlVolume = ventGate.gameObject.AddComponent<BoxCollider2D>();
            crawlVolume.isTrigger = true;
            crawlVolume.size = new Vector2(1.2f, 2.9f);
            MakeProp("Cell Locker", "locker_closed", new Vector2(7.3f, 4.0f), true);
            var locker = GameObject.Find("Cell Locker");
            AddHidingSpot(locker, "cell-locker", new Vector2(8.2f, 5.2f));
            MakeProp("Cell Desk", "desk", new Vector2(3.2f, 8.1f), true);
            MakeProp("Prisoner Bedding", "blanket_roll", new Vector2(6.7f, 7.3f), false);

            var keycard = MakeProp("Guard Desk", "desk", new Vector2(30.0f, 15.0f), true);
            var keyIcon = SpriteAt("Assets/BreakoutIsle/Art/UI/Icons/keycard.png");
            var key = MakeIconObject("Office Keycard", keyIcon, new Vector2(29.7f, 16.1f));
            AddPickup(key, "office-keycard", "keycard", "find-keycard", "Take keycard");
            MakeProp("Office Locker", "locker_closed", new Vector2(32f, 16.2f), true);
            AddHidingSpot(GameObject.Find("Office Locker"), "office-locker", new Vector2(33f, 16.4f));
            MakeGate("Prison Exit Gate", new Vector2(37.0f, 13.5f), "escape", false);
            var campaignExit = new GameObject("Exit Passage - Continue Campaign");
            campaignExit.transform.position = new Vector2(37.8f, 13.5f);
            campaignExit.layer = LayerMask.NameToLayer("Interactable");
            var campaignExitCollider = campaignExit.AddComponent<BoxCollider2D>();
            campaignExitCollider.isTrigger = true;
            campaignExitCollider.size = new Vector2(0.7f, 2.6f);
            var campaignExitTrigger = campaignExit.AddComponent<CampaignExitTrigger>();
            var campaignExitSerialized = new SerializedObject(campaignExitTrigger);
            campaignExitSerialized.FindProperty("transitionService").objectReferenceValue = campaignTransition;
            campaignExitSerialized.ApplyModifiedPropertiesWithoutUndo();

            var obstacleMask = (1 << LayerMask.NameToLayer("Vision")) | (1 << LayerMask.NameToLayer("Hiding"));
            var guards = new[]
            {
                CreateGuard("Guard Corridor", new Vector2(14f, 8.1f), new[] { new Vector2(12, 8.1f), new Vector2(20, 8.1f) }, obstacleMask, guardController),
                CreateGuard("Guard Office", new Vector2(29f, 12.5f), new[] { new Vector2(27, 11), new Vector2(32, 15) }, obstacleMask, guardController)
            };
            var chaseRelay = sessionObject.AddComponent<GuardChaseSaveRelay>();
            var chaseSerialized = new SerializedObject(chaseRelay);
            chaseSerialized.FindProperty("coordinator").objectReferenceValue = coordinator;
            chaseSerialized.FindProperty("guards").arraySize = guards.Length;
            for (var i = 0; i < guards.Length; i++)
                chaseSerialized.FindProperty("guards").GetArrayElementAtIndex(i).objectReferenceValue = guards[i];
            chaseSerialized.ApplyModifiedPropertiesWithoutUndo();
            CreateDarkZone(new Vector2(13.5f, 4f), new Vector2(9f, 4f));
            BreakoutUiBuilder.BuildLevelHud(player, session, coordinator);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Built Level 01: cell, vent route, guarded corridor, dark alternate route, guard office, keycard, lockers, and exit.");
        }

        private static readonly Vector3Int[] Cardinal = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        private static bool IsFloor(int x, int y) =>
            (x >= 2 && x <= 8 && y >= 2 && y <= 9) ||
            (x >= 8 && x <= 26 && y >= 7 && y <= 9) ||
            (x >= 9 && x <= 18 && y >= 2 && y <= 7) ||
            (x >= 20 && x <= 23 && y >= 6 && y <= 9) ||
            (x >= 23 && x <= 34 && y >= 10 && y <= 17) ||
            (x >= 33 && x <= 38 && y >= 12 && y <= 14);

        private static Tile GetTile(string name, bool wall = false)
        {
            var path = TileRoot + "/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null) return tile;
            var source = SpriteAt("Assets/BreakoutIsle/Art/Environment/Prison/Tiles/" + name + ".png");
            if (source == null) throw new System.InvalidOperationException("Missing tile sprite " + name);
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = source;
            tile.colliderType = wall ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        private static LevelObjectiveSet CreateObjectives()
        {
            var set = AssetDatabase.LoadAssetAtPath<LevelObjectiveSet>(ObjectivePath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<LevelObjectiveSet>();
                AssetDatabase.CreateAsset(set, ObjectivePath);
            }
            set.levelId = "prison-01";
            set.steps = new[]
            {
                new ObjectiveStepDefinition { id = "find-spoon", description = "Find the hidden spoon", rewardQuestItemId = "spoon" },
                new ObjectiveStepDefinition { id = "open-vent", description = "Pry open the vent", prerequisiteId = "find-spoon", requiredQuestItemId = "spoon", consumeRequiredItem = true },
                new ObjectiveStepDefinition { id = "find-keycard", description = "Take the guard's keycard", prerequisiteId = "open-vent", rewardQuestItemId = "keycard" },
                new ObjectiveStepDefinition { id = "escape", description = "Use the keycard at the exit", prerequisiteId = "find-keycard", requiredQuestItemId = "keycard", consumeRequiredItem = true }
            };
            EditorUtility.SetDirty(set);
            return set;
        }

        private static GameObject MakeProp(string name, string spriteName, Vector2 position, bool collider)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (collider) go.layer = LayerMask.NameToLayer("Vision");
            var renderer = go.AddComponent<SpriteRenderer>();
            var folder = spriteName == "pillow" || spriteName == "blanket_roll"
                ? "Assets/BreakoutIsle/Art/Environment/Prison/Props/Dressing/"
                : "Assets/BreakoutIsle/Art/Environment/Prison/Props/";
            renderer.sprite = SpriteAt(folder + spriteName + ".png");
            renderer.sortingLayerName = "Actors";
            go.AddComponent<YSortRenderer>();
            if (collider)
            {
                var solid = go.AddComponent<BoxCollider2D>();
                solid.size = new Vector2(0.9f, 0.7f);
                solid.offset = new Vector2(0f, 0.35f);
            }
            return go;
        }

        private static GameObject MakeIconObject(string name, Sprite sprite, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Interactable");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Actors";
            go.AddComponent<YSortRenderer>();
            go.AddComponent<CircleCollider2D>().radius = 0.35f;
            return go;
        }

        private static void AddPickup(GameObject go, string stableId, string item, string objective, string prompt)
        {
            go.layer = LayerMask.NameToLayer("Interactable");
            if (go.GetComponent<Collider2D>() == null)
                go.AddComponent<CircleCollider2D>().radius = 0.35f;
            var pickup = go.AddComponent<QuestPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("persistentId").stringValue = stableId;
            so.FindProperty("itemId").stringValue = item;
            so.FindProperty("objectiveId").stringValue = objective;
            so.FindProperty("prompt").stringValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddAction(GameObject go, string stableId, string objective, string prompt)
        {
            var action = go.AddComponent<QuestActionInteractable>();
            var so = new SerializedObject(action);
            so.FindProperty("persistentId").stringValue = stableId;
            so.FindProperty("objectiveId").stringValue = objective;
            so.FindProperty("prompt").stringValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<CircleCollider2D>().radius = 0.6f;
        }

        private static void AddHidingSpot(GameObject go, string stableId, Vector2 exitPosition)
        {
            go.layer = LayerMask.NameToLayer("Hiding");
            go.AddComponent<HidingSpot>();
            var so = new SerializedObject(go.GetComponent<HidingSpot>());
            so.FindProperty("persistentId").stringValue = stableId;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1f, 1f);
            var exit = new GameObject("Safe Hiding Exit");
            exit.transform.SetParent(go.transform.parent, true);
            exit.transform.position = exitPosition;
            so.FindProperty("exitPoint").objectReferenceValue = exit.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GateInteractable MakeGate(string name, Vector2 position, string objectiveId, bool openOnlyOnObjective)
        {
            var go = MakeProp(name, "gate_00", position, true);
            var renderer = go.GetComponent<SpriteRenderer>();
            var collider = go.GetComponent<Collider2D>();
            collider.isTrigger = false;
            if (collider is BoxCollider2D box)
            {
                box.size = new Vector2(0.75f, 2.9f);
                // The gate spans the full walkable doorway vertically; centering it on the pivot
                // prevents a player from slipping underneath along the bottom floor row.
                box.offset = new Vector2(0f, 0.35f);
            }
            var gate = go.AddComponent<GateInteractable>();
            var serialized = new SerializedObject(gate);
            serialized.FindProperty("persistentId").stringValue = name == "Vent Route Gate" ? "vent-route-gate" : "prison-exit-gate";
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("prompt").stringValue = name == "Vent Route Gate" ? "Crawl through vent" : "Use keycard at gate";
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("gateCollider").objectReferenceValue = collider;
            serialized.FindProperty("session").objectReferenceValue = Object.FindAnyObjectByType<LevelSession>();
            serialized.FindProperty("openOnlyOnObjective").boolValue = openOnlyOnObjective;
            var frames = serialized.FindProperty("openFrames");
            frames.arraySize = 4;
            for (var i = 0; i < frames.arraySize; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = SpriteAt($"Assets/BreakoutIsle/Art/Environment/Prison/Props/gate_{i:00}.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        private static CheckpointAnchor MakeCheckpoint(string id, Transform safeSpawn, Vector2 position)
        {
            var checkpointObject = new GameObject("Checkpoint - " + id);
            checkpointObject.transform.position = position;
            if (safeSpawn == null)
            {
                safeSpawn = new GameObject("Safe Spawn - " + id).transform;
                safeSpawn.position = position;
            }
            var anchor = checkpointObject.AddComponent<CheckpointAnchor>();
            var serialized = new SerializedObject(anchor);
            serialized.FindProperty("checkpointId").stringValue = id;
            serialized.FindProperty("safeSpawn").objectReferenceValue = safeSpawn;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return anchor;
        }

        private static GuardBrain CreateGuard(string name, Vector2 position, Vector2[] route, int obstacleMask, AnimatorController controller)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Guard");
            var idle = "Assets/BreakoutIsle/Art/Characters/Guard/idle/idle.png";
            var sprite = FindSprite(idle, "idle_S_00");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Actors";
            go.AddComponent<YSortRenderer>();
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.48f, 0.62f);
            collider.offset = new Vector2(0f, 0.32f);
            var brain = go.AddComponent<GuardBrain>();
            var vision = new GameObject("Vision and detection indicator");
            vision.transform.SetParent(go.transform, false);
            vision.layer = LayerMask.NameToLayer("Ignore Raycast");
            vision.AddComponent<MeshFilter>();
            vision.AddComponent<MeshRenderer>();
            var visionIndicator = vision.AddComponent<GuardVisionIndicator>();
            visionIndicator.SetObstacleMask(obstacleMask);
            var points = new Transform[route.Length];
            for (var i = 0; i < route.Length; i++)
            {
                var p = new GameObject(name + " Patrol " + i);
                p.transform.position = route[i];
                points[i] = p.transform;
            }
            var so = new SerializedObject(brain);
            so.FindProperty("patrolPoints").arraySize = points.Length;
            for (var i = 0; i < points.Length; i++) so.FindProperty("patrolPoints").GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.FindProperty("lineOfSightObstacles").intValue = obstacleMask;
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("visionIndicator").objectReferenceValue = visionIndicator;
            so.ApplyModifiedPropertiesWithoutUndo();
            return brain;
        }

        private static void CreateDarkZone(Vector2 center, Vector2 size)
        {
            var go = new GameObject("Dark Route - reduced visibility");
            go.transform.position = center;
            var zone = go.AddComponent<DarkZone>();
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
        }

        private static Sprite SpriteAt(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var item in all) if (item is Sprite s) return s;
            return null;
        }

        private static Sprite FindSprite(string path, string name)
        {
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path)) if (item is Sprite sprite && sprite.name == name) return sprite;
            throw new System.InvalidOperationException("Missing sprite " + name + " in " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void EnsureBuildScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
                if (scene.path == path && scene.enabled) return;
            scenes.RemoveAll(scene => scene.path == path);
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
