using System.Collections.Generic;
using BreakoutIsle.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace BreakoutIsle.Editor
{
    public static class Level02SceneBuilder
    {
        private const string Root = "Assets/BreakoutIsle";
        private const string ScenePath = Root + "/Scenes/Level02_DiningLaundry.unity";
        private const string TileRoot = Root + "/Tiles/Prison";
        private const string ObjectivePath = Root + "/Data/Level02_Objectives.asset";
        private const string CampaignPath = Root + "/Data/CampaignLevel02.asset";
        private static readonly Vector3Int[] Cardinal = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        [MenuItem("Breakout Isle/Scenes/Build Level 02 Dining and Laundry")]
        public static void Build()
        {
            EnsureFolder(Root + "/Tiles");
            EnsureFolder(TileRoot);
            EnsureFolder(Root + "/Prefabs/Gameplay");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floorClean = GetTile("floor_clean");
            var floorScuffed = GetTile("floor_scuffed");
            var floorDamp = GetTile("floor_damp");
            var floorDrain = GetTile("floor_drain");
            var floorWarning = GetTile("floor_warning");
            var wall = GetTile("wall_clean", true);

            var globalLight = new GameObject("Global 2D Light", typeof(Light2D)).GetComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 0.58f;
            globalLight.color = new Color(0.78f, 0.83f, 0.94f);
            var gridObject = new GameObject("Level 02 Tile Grid", typeof(Grid));
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

            var walkable = BuildFloor(floorMap, floorClean, floorScuffed, floorDamp, floorDrain, floorWarning);
            foreach (var cell in walkable)
            foreach (var offset in Cardinal)
            {
                var neighbour = cell + offset;
                if (!walkable.Contains(neighbour) && neighbour.x >= 0 && neighbour.x <= 43 && neighbour.y >= 0 && neighbour.y <= 29)
                    wallMap.SetTile(neighbour, wall);
            }
            floorMap.RefreshAllTiles();
            wallMap.RefreshAllTiles();
            EditorUtility.SetDirty(floorMap);
            EditorUtility.SetDirty(wallMap);

            var objectives = PrepareObjectives();
            RookLocomotionBuilder.Build();
            var guardController = GuardAnimationBuilder.Build();
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Characters/RookPlayer.prefab");
            if (playerPrefab == null) throw new System.InvalidOperationException("Build Rook locomotion prefab first.");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Rook - Player";
            player.transform.position = new Vector3(4.5f, 6f, 0f);
            var stealth = GetOrAdd<PlayerStealthState>(player);
            GetOrAdd<PlayerStealthZone>(player);

            var sessionObject = new GameObject("Level Session");
            var session = sessionObject.AddComponent<LevelSession>();
            Set(session, "objectives", objectives);
            var interactor = player.AddComponent<PlayerInteractor>();
            Set(interactor, "session", session);
            Set(interactor, "interactionMask", ~0);
            var actionAnimator = player.GetComponent<PlayerActionAnimator>();
            if (actionAnimator == null) throw new System.InvalidOperationException("Rook player action animator prefab was not rebuilt.");

            var safeSpawn = new GameObject("Safe Spawn - Level 02 Intake");
            safeSpawn.transform.position = player.transform.position;
            var anchors = new[]
            {
                MakeCheckpoint("prison-02-start", safeSpawn.transform, player.transform.position),
                MakeCheckpoint("after-power", null, new Vector2(5f, 17f)),
                MakeCheckpoint("after-chest", null, new Vector2(33f, 17f))
            };
            var coordinator = sessionObject.AddComponent<CheckpointSaveCoordinator>();
            Set(coordinator, "levelId", "prison-02");
            Set(coordinator, "initialCheckpointId", "prison-02-start");
            Set(coordinator, "session", session);
            Set(coordinator, "playerAdapterComponent", player.GetComponent<PlayerMotor>());
            SetArray(coordinator, "checkpointAnchors", anchors);

            var campaignLevel = AssetDatabase.LoadAssetAtPath<CampaignLevelDefinition>(CampaignPath);
            if (campaignLevel == null) throw new System.InvalidOperationException("Missing CampaignLevel02 definition.");
            var transition = sessionObject.AddComponent<CampaignTransitionService>();
            Set(transition, "session", session);
            Set(transition, "saveCoordinator", coordinator);
            Set(transition, "currentLevel", campaignLevel);
            var checkpointRelay = sessionObject.AddComponent<ObjectiveCheckpointRelay>();
            Set(checkpointRelay, "session", session);
            Set(checkpointRelay, "coordinator", coordinator);
            SetCheckpoints(checkpointRelay, new[]
            {
                new ObjectiveCheckpoint { objectiveId = "power_cut", checkpointId = "after-power" },
                new ObjectiveCheckpoint { objectiveId = "yard_gate_key_found", checkpointId = "after-chest" }
            });

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(PixelCameraFollow));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(4.5f, 6f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            Set(cameraObject.GetComponent<PixelCameraFollow>(), "target", player.transform);

            var guards = BuildCafeteriaAndLaundry(session, transition, guardController);
            BuildPowerAndCameras(session, player);
            var chaseRelay = sessionObject.AddComponent<GuardChaseSaveRelay>();
            Set(chaseRelay, "coordinator", coordinator);
            SetArray(chaseRelay, "guards", guards);

            var projectile = SupportItemSceneBuilder.BuildProjectilePrefab();
            var supportItems = SupportItemSceneBuilder.ConfigurePlayer(player, session, camera,
                (1 << LayerMask.NameToLayer("Vision")) | (1 << LayerMask.NameToLayer("Hiding")), projectile);
            actionAnimator.BindGameplayEvents(interactor, session, stealth, supportItems);

            BreakoutUiBuilder.BuildLevelHud(player, session, coordinator,
                BreakoutUiBuilder.CreateLevel02MapLayout(new Vector2(41f, 13.5f)));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Built Level 02 with cafeteria, engineering power switch, revealed chest, laundry key, security cameras, hiding cart, patrols, checkpoints, and yard gate.");
        }

        private static GuardBrain[] BuildCafeteriaAndLaundry(LevelSession session,
            CampaignTransitionService transition, AnimatorController guardController)
        {
            MakeProp("Cafeteria Table A", "desk", new Vector2(7f, 9.5f), true);
            MakeProp("Cafeteria Table B", "desk", new Vector2(11.5f, 9.5f), true);
            MakeProp("Cafeteria Table C", "desk", new Vector2(16f, 9.5f), true);
            MakeProp("Cafeteria Storage", "locker_closed", new Vector2(4f, 11f), true);

            var chestRoot = MakeProp("Power Revealed Chest", "chest_00", new Vector2(14f, 6.5f), false);
            chestRoot.AddComponent<CircleCollider2D>().radius = 0.6f;
            var chest = chestRoot.AddComponent<AnimatedQuestChest>();
            Set(chest, "persistentId", "laundry-yard-key-chest");
            Set(chest, "objectiveId", "yard_gate_key_found");
            Set(chest, "prompt", "Mở hòm bằng chìa khóa giặt");
            Set(chest, "session", session);
            Set(chest, "spriteRenderer", chestRoot.GetComponent<SpriteRenderer>());
            SetArray(chest, "openFrames", LoadPropFrames("chest_"));
            var chestReveal = chestRoot.AddComponent<ChestPowerReveal>();
            SetArray(chestReveal, "objectsRevealedWhenUnpowered", new[] { chestRoot });

            var laundryCart = MakeProp("Laundry Hiding Cart", "laundry_cart_00", new Vector2(29.5f, 8.7f), true);
            AddHidingSpot(laundryCart, "laundry-cart-hide", new Vector2(30.8f, 9f));
            var laundryKey = MakeIconObject("Laundry Chest Key", SpriteAt(Root + "/Art/UI/Icons/brass_key.png"), new Vector2(27f, 5f));
            AddQuestPickup(laundryKey, "laundry-chest-key", "laundry_chest_key", "laundry_chest_key_found", "Nhặt chìa khóa hòm");

            AddSupportPickup(MakeIconObject("Cafeteria Stone", SpriteAt(Root + "/Art/UI/Icons/stone.png"), new Vector2(5f, 5f)), "cafeteria-stone", "stone");
            AddSupportPickup(MakeIconObject("Laundry Tin Can", SpriteAt(Root + "/Art/UI/Icons/tin_can.png"), new Vector2(34f, 4f)), "laundry-tin-can", "tin_can");

            var gate = MakeGate("Yard Gate", new Vector2(37.7f, 13.5f), "yard_gate_opened", false, session);
            var exit = new GameObject("Exit Passage - Level 03 Yard");
            exit.layer = LayerMask.NameToLayer("Interactable");
            exit.transform.position = new Vector2(41f, 13.5f);
            var exitCollider = exit.AddComponent<BoxCollider2D>();
            exitCollider.isTrigger = true;
            exitCollider.size = new Vector2(0.8f, 2.5f);
            var exitTrigger = exit.AddComponent<CampaignExitTrigger>();
            Set(exitTrigger, "transitionService", transition);
            gate.name = "Yard Gate - Key Required";

            var obstacleMask = (1 << LayerMask.NameToLayer("Vision")) | (1 << LayerMask.NameToLayer("Hiding"));
            var guards = new[]
            {
                CreateGuard("Cafeteria Guard", new Vector2(15f, 11.5f), new[] { new Vector2(13f, 11.5f), new Vector2(17f, 11.5f) }, obstacleMask, guardController),
                CreateGuard("Laundry Guard", new Vector2(25f, 5.5f), new[] { new Vector2(24f, 5.5f), new Vector2(34f, 5.5f) }, obstacleMask, guardController),
                CreateGuard("Yard Watch", new Vector2(40f, 15f), new[] { new Vector2(39f, 15f), new Vector2(42f, 15f) }, obstacleMask, guardController)
            };
            var exitMarker = new GameObject("Yard Exit Marker");
            exitMarker.transform.position = new Vector2(41f, 13.5f);
            var marker = exitMarker.AddComponent<SpriteRenderer>();
            marker.sprite = SpriteAt(Root + "/Art/UI/Icons/boat_key.png");
            marker.sortingLayerName = "Actors";
            marker.sortingOrder = 20;
            return guards;
        }

        private static void BuildPowerAndCameras(LevelSession session, GameObject player)
        {
            var obstacleMask = (1 << LayerMask.NameToLayer("Vision")) | (1 << LayerMask.NameToLayer("Hiding"));
            var mainLightObject = new GameObject("Cafeteria Power Light", typeof(Light2D));
            mainLightObject.transform.position = new Vector3(12f, 7.5f, 0f);
            var roomLight = mainLightObject.GetComponent<Light2D>();
            roomLight.lightType = Light2D.LightType.Point;
            roomLight.intensity = 0.85f;
            roomLight.pointLightOuterRadius = 8f;
            roomLight.color = new Color(1f, 0.83f, 0.52f);
            var lightConsumer = mainLightObject.AddComponent<LightPowerConsumer>();
            Set(lightConsumer, "targetLight", roomLight);

            var breaker = MakeProp("Engineering Main Breaker", "breaker_00", new Vector2(5f, 21f), false);
            breaker.layer = LayerMask.NameToLayer("Interactable");
            breaker.AddComponent<CircleCollider2D>().radius = 0.6f;
            var breakerVisual = breaker.AddComponent<PowerSwitchVisual>();
            Set(breakerVisual, "spriteRenderer", breaker.GetComponent<SpriteRenderer>());
            Set(breakerVisual, "poweredSprite", SpriteAt(PropPath("breaker_00")));
            Set(breakerVisual, "unpoweredSprite", SpriteAt(PropPath("breaker_03")));
            var powerSwitch = breaker.AddComponent<PowerSwitchInteractable>();
            Set(powerSwitch, "persistentId", "engineering-breaker");
            Set(powerSwitch, "objectiveId", "power_cut");
            Set(powerSwitch, "prompt", "Gạt cần điện");
            Set(powerSwitch, "session", session);
            SetArray(powerSwitch, "linkedConsumers", new MonoBehaviour[] { breakerVisual, lightConsumer, Object.FindAnyObjectByType<ChestPowerReveal>() });
            Set(powerSwitch, "baselinePowered", true);

            var cameraA = CreateSecurityCamera("Cafeteria Security Camera", new Vector2(20f, 9f), 90f, obstacleMask, player.GetComponent<PlayerStealthState>());
            var cameraB = CreateSecurityCamera("Laundry Security Camera", new Vector2(33.5f, 10.5f), 180f, obstacleMask, player.GetComponent<PlayerStealthState>());
            var cameraBreaker = MakeProp("Security Camera Cutoff", "breaker_00", new Vector2(32f, 3.5f), false);
            cameraBreaker.layer = LayerMask.NameToLayer("Interactable");
            cameraBreaker.AddComponent<CircleCollider2D>().radius = 0.55f;
            var cameraBreakerVisual = cameraBreaker.AddComponent<PowerSwitchVisual>();
            Set(cameraBreakerVisual, "spriteRenderer", cameraBreaker.GetComponent<SpriteRenderer>());
            Set(cameraBreakerVisual, "poweredSprite", SpriteAt(PropPath("breaker_00")));
            Set(cameraBreakerVisual, "unpoweredSprite", SpriteAt(PropPath("breaker_03")));
            var cameraSwitch = cameraBreaker.AddComponent<PowerSwitchInteractable>();
            Set(cameraSwitch, "persistentId", "camera-cutoff");
            Set(cameraSwitch, "prompt", "Ngắt camera an ninh");
            Set(cameraSwitch, "session", session);
            SetArray(cameraSwitch, "linkedConsumers", new MonoBehaviour[] { cameraBreakerVisual, cameraA, cameraB });
            Set(cameraSwitch, "baselinePowered", true);
        }

        private static SecurityCamera CreateSecurityCamera(string name, Vector2 position, float zRotation,
            LayerMask obstacleMask, PlayerStealthState player)
        {
            var go = MakeProp(name, "camera_00", position, false);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
            var camera = go.AddComponent<SecurityCamera>();
            Set(camera, "player", player);
            Set(camera, "obstacleMask", obstacleMask);
            Set(camera, "fieldOfViewDegrees", 60f);
            Set(camera, "detectionRange", 6f);
            Set(camera, "guardReportRadius", 15f);
            var cone = new GameObject("Clipped view cone", typeof(MeshFilter), typeof(MeshRenderer), typeof(SecurityCameraCone));
            cone.transform.SetParent(go.transform, false);
            cone.transform.localPosition = Vector3.zero;
            cone.GetComponent<SecurityCameraCone>().Configure(camera, obstacleMask);
            return camera;
        }

        private static LevelObjectiveSet PrepareObjectives()
        {
            var set = AssetDatabase.LoadAssetAtPath<LevelObjectiveSet>(ObjectivePath);
            if (set == null) throw new System.InvalidOperationException("Missing Level02_Objectives.asset.");
            set.levelId = "prison-02";
            if (set.steps == null || set.steps.Length != 4) throw new System.InvalidOperationException("Level 02 objective chain must contain four steps.");
            set.steps[0].description = "Tắt điện ở phòng kỹ thuật";
            set.steps[1].description = "Tìm chìa khóa hòm trong phòng giặt";
            set.steps[2].description = "Mở hòm lấy chìa khóa cổng sân";
            set.steps[3].description = "Dùng chìa khóa mở cổng sân tù";
            EditorUtility.SetDirty(set);
            return set;
        }

        private static HashSet<Vector3Int> BuildFloor(Tilemap map, Tile clean, Tile scuffed, Tile damp, Tile drain, Tile warning)
        {
            var cells = new HashSet<Vector3Int>();
            AddRoom(2, 2, 18, 13);
            AddRoom(18, 6, 22, 9);
            AddRoom(22, 2, 36, 13);
            AddRoom(8, 13, 12, 16);
            AddRoom(2, 16, 12, 27);
            AddRoom(36, 10, 38, 17);
            AddRoom(38, 10, 42, 17);
            foreach (var cell in cells)
            {
                var x = cell.x;
                var y = cell.y;
                var tile = ((x * 11 + y * 5) % 13 == 0) ? scuffed : clean;
                if (x >= 22 && x <= 36 && y <= 6) tile = damp;
                if ((x == 25 || x == 34) && y == 7) tile = drain;
                if (x == 5 && y == 20) tile = warning;
                map.SetTile(cell, tile);
            }
            map.CompressBounds();
            return cells;

            void AddRoom(int left, int bottom, int right, int top)
            {
                for (var y = bottom; y <= top; y++)
                    for (var x = left; x <= right; x++)
                        cells.Add(new Vector3Int(x, y, 0));
            }
        }

        private static GameObject MakeProp(string name, string spriteName, Vector2 position, bool collider)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (collider) go.layer = LayerMask.NameToLayer("Vision");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteAt(PropPath(spriteName));
            renderer.sortingLayerName = "Actors";
            go.AddComponent<YSortRenderer>();
            if (collider)
            {
                var solid = go.AddComponent<BoxCollider2D>();
                solid.size = new Vector2(0.8f, 0.65f);
                solid.offset = new Vector2(0f, 0.32f);
            }
            return go;
        }

        private static GameObject MakeIconObject(string name, Sprite sprite, Vector2 position)
        {
            if (sprite == null) throw new System.InvalidOperationException("Missing icon for " + name);
            var go = new GameObject(name);
            go.layer = LayerMask.NameToLayer("Interactable");
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Actors";
            go.AddComponent<YSortRenderer>();
            go.AddComponent<CircleCollider2D>().radius = 0.35f;
            return go;
        }

        private static void AddQuestPickup(GameObject go, string stableId, string itemId, string objectiveId, string prompt)
        {
            var pickup = go.AddComponent<QuestPickup>();
            Set(pickup, "persistentId", stableId);
            Set(pickup, "itemId", itemId);
            Set(pickup, "objectiveId", objectiveId);
            Set(pickup, "prompt", prompt);
        }

        private static void AddSupportPickup(GameObject go, string stableId, string itemId)
        {
            var pickup = go.AddComponent<SupportItemPickup>();
            Set(pickup, "persistentId", stableId);
            Set(pickup, "itemId", itemId);
            Set(pickup, "prompt", "Nhặt " + (itemId == "stone" ? "hòn đá" : "lon rỗng"));
        }

        private static void AddHidingSpot(GameObject go, string stableId, Vector2 exitPosition)
        {
            go.layer = LayerMask.NameToLayer("Hiding");
            var hiding = go.AddComponent<HidingSpot>();
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1f, 1f);
            var exit = new GameObject("Safe Hiding Exit");
            exit.transform.position = exitPosition;
            var serialized = new SerializedObject(hiding);
            serialized.FindProperty("persistentId").stringValue = stableId;
            serialized.FindProperty("exitPoint").objectReferenceValue = exit.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GateInteractable MakeGate(string name, Vector2 position, string objectiveId, bool objectiveControlled, LevelSession session)
        {
            var go = MakeProp(name, "gate_00", position, true);
            var renderer = go.GetComponent<SpriteRenderer>();
            var collider = go.GetComponent<Collider2D>();
            var box = (BoxCollider2D)collider;
            box.size = new Vector2(0.75f, 3.2f);
            box.offset = new Vector2(0f, 0.35f);
            var gate = go.AddComponent<GateInteractable>();
            Set(gate, "persistentId", "prison-02-yard-gate");
            Set(gate, "objectiveId", objectiveId);
            Set(gate, "prompt", "Dùng chìa khóa mở cổng sân");
            Set(gate, "spriteRenderer", renderer);
            Set(gate, "gateCollider", collider);
            Set(gate, "session", session);
            Set(gate, "openOnlyOnObjective", objectiveControlled);
            SetArray(gate, "openFrames", LoadPropFrames("gate_"));
            return gate;
        }

        private static GuardBrain CreateGuard(string name, Vector2 position, Vector2[] route, int obstacleMask, AnimatorController controller)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Guard");
            var spritePath = Root + "/Art/Characters/Guard/idle/idle.png";
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = FindSprite(spritePath, "idle_S_00");
            renderer.sortingLayerName = "Actors";
            go.AddComponent<YSortRenderer>();
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.48f, 0.62f);
            capsule.offset = new Vector2(0f, 0.32f);
            var brain = go.AddComponent<GuardBrain>();
            var vision = new GameObject("Vision and detection indicator", typeof(MeshFilter), typeof(MeshRenderer));
            vision.transform.SetParent(go.transform, false);
            vision.layer = LayerMask.NameToLayer("Ignore Raycast");
            var indicator = vision.AddComponent<GuardVisionIndicator>();
            indicator.SetObstacleMask(obstacleMask);
            var points = new Transform[route.Length];
            for (var i = 0; i < route.Length; i++)
            {
                var point = new GameObject(name + " Patrol " + i);
                point.transform.position = route[i];
                points[i] = point.transform;
            }
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("patrolPoints").arraySize = points.Length;
            for (var i = 0; i < points.Length; i++) serialized.FindProperty("patrolPoints").GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            serialized.FindProperty("lineOfSightObstacles").intValue = obstacleMask;
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("visionIndicator").objectReferenceValue = indicator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return brain;
        }

        private static CheckpointAnchor MakeCheckpoint(string id, Transform safeSpawn, Vector2 position)
        {
            var go = new GameObject("Checkpoint - " + id);
            go.transform.position = position;
            if (safeSpawn == null)
            {
                safeSpawn = new GameObject("Safe Spawn - " + id).transform;
                safeSpawn.position = position;
            }
            var anchor = go.AddComponent<CheckpointAnchor>();
            Set(anchor, "checkpointId", id);
            Set(anchor, "safeSpawn", safeSpawn);
            return anchor;
        }

        private static Tile GetTile(string name, bool solid = false)
        {
            var tilePath = TileRoot + "/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile != null) return tile;
            var sprite = SpriteAt(Root + "/Art/Environment/Prison/Tiles/" + name + ".png");
            if (sprite == null) throw new System.InvalidOperationException("Missing prison tile sprite: " + name);
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);
            return tile;
        }

        private static Sprite[] LoadPropFrames(string prefix)
        {
            var frames = new List<Sprite>();
            for (var i = 0; i < 4; i++)
            {
                var sprite = SpriteAt(PropPath(prefix + i.ToString("00")));
                if (sprite == null) throw new System.InvalidOperationException("Missing generated prop frame: " + prefix + i.ToString("00"));
                frames.Add(sprite);
            }
            return frames.ToArray();
        }

        private static string PropPath(string name) => Root + "/Art/Environment/Prison/Props/" + name + ".png";

        private static Sprite SpriteAt(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path))
                if (item is Sprite loaded) return loaded;
            return null;
        }

        private static Sprite FindSprite(string path, string name)
        {
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path))
                if (item is Sprite sprite && sprite.name == name) return sprite;
            throw new System.InvalidOperationException("Missing directional guard sprite " + name + " at " + path);
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void Set(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, string value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, float value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, bool value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string property, T[] values) where T : Object
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(property);
            if (array == null) throw new System.InvalidOperationException("Missing serialized array " + property + " on " + target.GetType().Name);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCheckpoints(ObjectiveCheckpointRelay relay, ObjectiveCheckpoint[] values)
        {
            var serialized = new SerializedObject(relay);
            var array = serialized.FindProperty("checkpoints");
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).FindPropertyRelative("objectiveId").stringValue = values[i].objectiveId;
                array.GetArrayElementAtIndex(i).FindPropertyRelative("checkpointId").stringValue = values[i].checkpointId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
