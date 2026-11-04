using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BreakoutIsle.Audio;
using BreakoutIsle.Gameplay;
using BreakoutIsle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BreakoutIsle.Editor
{
    public static class BreakoutUiBuilder
    {
        private const string Root = "Assets/BreakoutIsle";
        private const string CatalogPath = Root + "/Audio/AudioCatalog.asset";
        private const string ManifestPath = Root + "/Audio/AudioManifest.json";
        private const string SkinPath = Root + "/Art/UI/Skins/";
        private const string IconPath = Root + "/Art/UI/Icons/";
        private const string MenuPath = Root + "/Art/UI/Menu/menu_background.png";
        private static readonly Rect Level01MapBounds = new Rect(2f, 2f, 36f, 20f);

        [MenuItem("Breakout Isle/Audio/Build Audio Catalog")]
        public static AudioCatalog BuildAudioCatalog()
        {
            var sourcePath = Path.GetFullPath(ManifestPath);
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Audio event manifest is missing.", sourcePath);
            var json = File.ReadAllText(sourcePath);
            var eventsMatch = Regex.Match(json, "\\\"events\\\"\\s*:\\s*\\{(?<body>[\\s\\S]*?)\\}\\s*,\\s*\\\"assets\\\"", RegexOptions.CultureInvariant);
            if (!eventsMatch.Success) throw new InvalidDataException("AudioManifest.json does not contain the expected events object followed by assets.");

            var definitions = new List<AudioEventDefinition>();
            var missing = new List<string>();
            var eventPattern = new Regex("\\\"(?<id>[^\\\"]+)\\\"\\s*:\\s*\\[(?<clips>[^\\]]*)\\]", RegexOptions.CultureInvariant);
            var clipPattern = new Regex("\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.CultureInvariant);
            foreach (Match eventMatch in eventPattern.Matches(eventsMatch.Groups["body"].Value))
            {
                var clips = new List<AudioClip>();
                foreach (Match clipMatch in clipPattern.Matches(eventMatch.Groups["clips"].Value))
                {
                    var relative = clipMatch.Groups["path"].Value.Replace('\\', '/');
                    var assetPath = Root + "/Audio/" + relative;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (clip == null) missing.Add(eventMatch.Groups["id"].Value + " -> " + assetPath);
                    else clips.Add(clip);
                }
                definitions.Add(new AudioEventDefinition { id = eventMatch.Groups["id"].Value, clips = clips.ToArray() });
            }
            if (definitions.Count == 0) throw new InvalidDataException("Audio event manifest contains no event mappings.");
            if (missing.Count > 0) throw new InvalidDataException("Missing AudioClip references:\n" + string.Join("\n", missing));

            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.SetEvents(definitions.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Built AudioCatalog from " + definitions.Count + " manifest events.", catalog);
            return catalog;
        }

        public static GameObject BuildLevelHud(GameObject player, LevelSession session, CheckpointSaveCoordinator checkpointCoordinator = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (session == null) throw new ArgumentNullException(nameof(session));
            EnsureEventSystem();
            var canvasRoot = new GameObject("Game HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.SetActive(false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.referencePixelsPerUnit = 32f;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<VietnameseFontBinder>();

            var panelSprite = LoadSprite(SkinPath + "panel.png");
            var buttonSprite = LoadSprite(SkinPath + "button.png");
            var selectedSprite = LoadSprite(SkinPath + "slot_selected.png");
            var warningSprite = LoadSprite(SkinPath + "warning.png");
            var controller = canvasRoot.AddComponent<GameHudController>();

            var top = CreateImage(canvasRoot.transform, "HUD Panel", panelSprite, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(218, 68), Image.Type.Sliced);
            top.raycastTarget = true;
            var heading = CreateText(top.transform, "Level Heading", "NHÀ TÙ · CỤM 01", 15, new Vector2(0, 1), new Vector2(10, -4), new Vector2(188, 20));
            heading.color = new Color(1f, 0.78f, 0.36f);
            var staminaCaption = CreateText(top.transform, "Stamina Caption", "THỂ LỰC", 10, new Vector2(0, 1), new Vector2(10, -28), new Vector2(49, 14));
            var staminaTrack = CreateImage(top.transform, "Stamina Track", buttonSprite, new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -30), new Vector2(138, 10), Image.Type.Sliced);
            staminaTrack.color = new Color(0.14f, 0.18f, 0.20f, 1f);
            var staminaMeter = CreateImage(top.transform, "Stamina Fill", null, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(67, -33), new Vector2(132, 6), Image.Type.Simple);
            staminaMeter.color = new Color(0.27f, 0.78f, 0.56f, 1f);
            var alert = CreateText(top.transform, "Stealth State", "ĐI NHẸ", 11, new Vector2(0, 1), new Vector2(10, -50), new Vector2(188, 14));
            alert.color = new Color(0.58f, 0.9f, 0.75f);

            var mini = CreateImage(canvasRoot.transform, "Minimap Panel", panelSprite, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -10), new Vector2(137, 95), Image.Type.Sliced);
            mini.raycastTarget = true;
            CreateText(mini.transform, "Minimap Heading", "SƠ ĐỒ KHU VỰC", 11, new Vector2(0.5f, 1), new Vector2(0, -7), new Vector2(128, 16)).alignment = TextAnchor.MiddleCenter;
            var miniContent = BuildMiniMap(mini.transform, player.transform);
            var miniMarker = CreateMapMarker(miniContent, "Rook marker", new Color(1f, 0.82f, 0.25f));
            var miniTracker = canvasRoot.AddComponent<MinimapPlayerMarker>();
            miniTracker.Bind(miniContent, miniMarker, player.transform, Level01MapBounds);
            EditorUtility.SetDirty(miniTracker);

            var objectivePanel = CreateImage(canvasRoot.transform, "Tab Map and Objectives", panelSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(490, 260), Image.Type.Sliced).gameObject;
            objectivePanel.GetComponent<Image>().raycastTarget = true;
            var objectiveText = CreateText(objectivePanel.transform, "Objective Checklist", "", 15, new Vector2(0, 1), new Vector2(24, -38), new Vector2(275, 150));
            objectiveText.alignment = TextAnchor.UpperLeft;
            var inventoryText = CreateText(objectivePanel.transform, "Quest and Support Items", "", 10, new Vector2(0, 0), new Vector2(20, 16), new Vector2(270, 40));
            inventoryText.alignment = TextAnchor.LowerLeft;
            CreateText(objectivePanel.transform, "Map Title", "SƠ ĐỒ NHÀ TÙ", 16, new Vector2(1, 1), new Vector2(-100, -34), new Vector2(170, 24)).alignment = TextAnchor.MiddleCenter;
            var mapArea = CreateImage(objectivePanel.transform, "Map Area", panelSprite, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, -3), new Vector2(174, 158), Image.Type.Sliced);
            var fullMapContent = BuildPrisonMap(mapArea.transform, player.transform);
            var fullMapMarker = CreateMapMarker(fullMapContent, "Rook map marker", new Color(1f, 0.82f, 0.25f));
            var fullMapTracker = canvasRoot.AddComponent<MinimapPlayerMarker>();
            fullMapTracker.Bind(fullMapContent, fullMapMarker, player.transform, Level01MapBounds);
            EditorUtility.SetDirty(fullMapTracker);
            var mapClose = CreateButton(objectivePanel.transform, "ĐÓNG · TAB", buttonSprite, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 15), new Vector2(135, 30));
            objectivePanel.SetActive(false);

            var slotsPanel = CreateImage(canvasRoot.transform, "Support Inventory", panelSprite, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 10), new Vector2(180, 52), Image.Type.Sliced);
            slotsPanel.raycastTarget = true;
            var slotImages = new Image[3];
            var slotTexts = new Text[3];
            var icons = LoadSupportIcons();
            for (var index = 0; index < 3; index++)
            {
                var slot = CreateImage(slotsPanel.transform, "Support Slot " + (index + 1), selectedSprite, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6 + index * 58, 0), new Vector2(54, 38), Image.Type.Sliced);
                var icon = CreateImage(slot.transform, "Item Icon", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(20, 20), Image.Type.Simple);
                icon.raycastTarget = false;
                icon.enabled = false;
                slotImages[index] = slot;
                slotTexts[index] = CreateText(slot.transform, "Slot Label", (index + 1).ToString(), 10, new Vector2(1, 0), new Vector2(-5, 3), new Vector2(14, 12));
                slotTexts[index].alignment = TextAnchor.LowerRight;
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot;
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                slot.gameObject.AddComponent<UIButtonAudio>();
                var slotButton = slot.gameObject.AddComponent<SupportSlotButton>();
                slotButton.Configure(controller, index);
                EditorUtility.SetDirty(slotButton);
            }
            var promptPanel = CreateImage(canvasRoot.transform, "Interaction Prompt", buttonSprite, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(230, 34), Image.Type.Sliced);
            promptPanel.raycastTarget = true;
            var promptText = CreateText(promptPanel.transform, "Prompt Text", "", 15, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(212, 28));
            var status = CreateText(canvasRoot.transform, "Feedback Message", "", 13, new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(430, 24));
            status.alignment = TextAnchor.MiddleCenter;
            var promptBinding = player.GetComponent<InteractionPromptHud>() ?? player.AddComponent<InteractionPromptHud>();
            SetObjectReference(promptBinding, "interactor", player.GetComponent<PlayerInteractor>());
            SetObjectReference(promptBinding, "promptText", promptText);
            SetObjectReference(promptBinding, "promptPanel", promptPanel.gameObject);
            promptBinding.Bind(player.GetComponent<PlayerInteractor>(), promptText, promptPanel.gameObject);
            EditorUtility.SetDirty(promptBinding);
            var staminaBinding = player.GetComponent<StaminaHud>() ?? player.AddComponent<StaminaHud>();
            SetObjectReference(staminaBinding, "meter", staminaMeter);
            staminaBinding.Bind(staminaMeter, player.GetComponent<PlayerMotor>());
            EditorUtility.SetDirty(staminaBinding);
            var messageBinding = session.GetComponent<LevelMessageHud>() ?? session.gameObject.AddComponent<LevelMessageHud>();
            SetObjectReference(messageBinding, "session", session);
            SetObjectReference(messageBinding, "messageText", status);
            messageBinding.Bind(session, status);
            EditorUtility.SetDirty(messageBinding);

            var pause = BuildOverlay(canvasRoot.transform, "Pause Menu", "TẠM DỪNG", panelSprite, buttonSprite,
                new[] { "TIẾP TỤC", "CÀI ĐẶT", "VỀ MENU" }, new Vector2(350, 250));
            var settings = BuildSettings(canvasRoot.transform, panelSprite, buttonSprite);
            var captured = BuildOverlay(canvasRoot.transform, "Caught Screen", "BẠN ĐÃ BỊ BẮT", panelSprite, buttonSprite,
                new[] { "THỬ LẠI ĐIỂM LƯU", "VỀ MENU" }, new Vector2(390, 220));
            var victory = BuildOverlay(canvasRoot.transform, "Victory Screen", "VƯỢT NGỤC THÀNH CÔNG", panelSprite, buttonSprite,
                new[] { "VỀ MENU" }, new Vector2(390, 190));
            var restart = BuildOverlay(canvasRoot.transform, "Restart Confirmation", "KHÔNG ĐỌC ĐƯỢC TỆP LƯU", panelSprite, buttonSprite,
                new[] { "BẮT ĐẦU LẠI", "HỦY" }, new Vector2(420, 205));
            pause.root.SetActive(false);
            settings.root.SetActive(false);
            captured.root.SetActive(false);
            victory.root.SetActive(false);
            restart.root.SetActive(false);

            var settingsController = settings.root.AddComponent<AudioSettingsPanel>();
            settingsController.Bind(settings.slider, settings.muteToggle);
            EditorUtility.SetDirty(settingsController);
            controller.ConfigureView(objectiveText, inventoryText, status, alert, objectivePanel, pause.root, settings.root,
                captured.root, victory.root, captured.buttons[0], pause.buttons[0], pause.buttons[1], settings.buttons[0],
                pause.buttons[2], slotImages, slotTexts, icons, selectedSprite, buttonSprite, settingsController,
                restart.root, restart.buttons[0], restart.buttons[1], mapClose,
                captured.buttons[1], victory.buttons[0]);
            controller.Bind(player.GetComponent<PlayerInputReader>(), player.GetComponent<PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(), player.GetComponent<PlayerStealthState>(), session, checkpointCoordinator);
            EditorUtility.SetDirty(controller);
            var statusButton = CreateText(canvasRoot.transform, "Map Hint", "WASD di chuyển   ·   E tương tác   ·   TAB nhiệm vụ   ·   ESC tạm dừng", 10,
                new Vector2(1, 0), new Vector2(-12, 11), new Vector2(350, 18));
            statusButton.alignment = TextAnchor.LowerRight;

            BuildAudioRuntime(player);
            canvasRoot.SetActive(true);
            EditorUtility.SetDirty(canvasRoot);
            return canvasRoot;
        }

        public static GameObject BuildMainMenuUi(CheckpointSaveCoordinator coordinator = null)
        {
            EnsureEventSystem();
            var root = new GameObject("Main Menu UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.SetActive(false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.referencePixelsPerUnit = 32;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<VietnameseFontBinder>();
            var background = CreateImage(root.transform, "Menu Background", LoadSprite(MenuPath), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 360), Image.Type.Simple);
            background.preserveAspect = false;
            background.raycastTarget = false;
            var titlePanel = CreateImage(root.transform, "Title Panel", LoadSprite(SkinPath + "panel.png"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150, 4), new Vector2(270, 120), Image.Type.Sliced);
            CreateText(titlePanel.transform, "Title", "VƯỢT NGỤC", 34, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(240, 50)).alignment = TextAnchor.MiddleCenter;
            CreateText(titlePanel.transform, "Tagline", "Một lối thoát. Nhiều lựa chọn.", 14, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(240, 28)).alignment = TextAnchor.MiddleCenter;
            var buttons = new List<Button>();
            var labels = new[] { "CHƠI MỚI", "TIẾP TỤC", "CÀI ĐẶT", "THOÁT" };
            for (var i = 0; i < labels.Length; i++)
                buttons.Add(CreateButton(root.transform, labels[i], LoadSprite(SkinPath + "button.png"), new Vector2(0.77f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 65 - i * 48), new Vector2(205, 38)));
            var settings = BuildSettings(root.transform, LoadSprite(SkinPath + "panel.png"), LoadSprite(SkinPath + "button.png"));
            var confirm = BuildOverlay(root.transform, "Restart Confirmation", "KHÔNG ĐỌC ĐƯỢC TỆP LƯU", LoadSprite(SkinPath + "panel.png"), LoadSprite(SkinPath + "button.png"), new[] { "BẮT ĐẦU LẠI", "HỦY" }, new Vector2(420, 205));
            var saveStatus = CreateText(root.transform, "Save Availability", "", 11, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(510, 22));
            saveStatus.alignment = TextAnchor.MiddleCenter;
            settings.root.SetActive(false);
            confirm.root.SetActive(false);
            var settingsController = settings.root.AddComponent<AudioSettingsPanel>();
            settingsController.Bind(settings.slider, settings.muteToggle);
            EditorUtility.SetDirty(settingsController);
            var menuCoordinator = coordinator != null ? coordinator : root.AddComponent<CheckpointSaveCoordinator>();
            var controller = root.AddComponent<MainMenuController>();
            controller.Configure(buttons[0], buttons[1], buttons[2], buttons[3], settings.buttons[0], settings.root,
                confirm.root, confirm.buttons[0], confirm.buttons[1], "Level01_Prison", "cell-start", menuCoordinator, saveStatus);
            BuildAudioRuntime(null);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(menuCoordinator);
            root.SetActive(true);
            EditorUtility.SetDirty(root);
            return root;
        }

        private static void BuildAudioRuntime(GameObject player)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null) catalog = BuildAudioCatalog();
            var serviceObject = GameObject.Find("Breakout Audio Service");
            if (serviceObject == null) serviceObject = new GameObject("Breakout Audio Service");
            var service = serviceObject.GetComponent<AudioService>() ?? serviceObject.AddComponent<AudioService>();
            service.Configure(catalog);
            EditorUtility.SetDirty(serviceObject);
            EditorUtility.SetDirty(service);
            if (player != null && player.GetComponent<PlayerFootstepAudio>() == null) player.AddComponent<PlayerFootstepAudio>();
            if (player != null && player.GetComponent<PlayerInteractionAudio>() == null) player.AddComponent<PlayerInteractionAudio>();
            foreach (var guard in UnityEngine.Object.FindObjectsOfType<GuardBrain>(true))
                if (guard.GetComponent<GuardAudioEvents>() == null) guard.gameObject.AddComponent<GuardAudioEvents>();
            service.PlayLoop("ambience.prison", true);
            service.CrossfadeMusic("music.stealth");
        }

        private static void EnsureEventSystem()
        {
            var current = UnityEngine.Object.FindObjectOfType<EventSystem>();
            if (current != null)
            {
                var oldModule = current.GetComponent<StandaloneInputModule>();
                if (oldModule != null) UnityEngine.Object.DestroyImmediate(oldModule);
                if (current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    var module = current.gameObject.AddComponent<InputSystemUIInputModule>();
                    module.AssignDefaultActions();
                }
                return;
            }
            var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static RectTransform BuildMiniMap(Transform parent, Transform player)
        {
            var content = CreateMapContent(parent, "Minimap floorplan", new Vector2(8, 14), new Vector2(121, 61));
            AddMapRegion(content, "Cell", new Rect(2, 2, 6, 7), new Color(0.93f, 0.61f, 0.23f), "");
            AddMapRegion(content, "Vent route", new Rect(9, 2, 9, 5), new Color(0.42f, 0.57f, 0.67f), "");
            AddMapRegion(content, "Main corridor", new Rect(8, 7, 18, 2), new Color(0.63f, 0.70f, 0.72f), "");
            AddMapRegion(content, "West corridor", new Rect(20, 6, 4, 3), new Color(0.63f, 0.70f, 0.72f), "");
            AddMapRegion(content, "Guard office", new Rect(23, 10, 11, 7), new Color(0.48f, 0.58f, 0.63f), "");
            AddMapRegion(content, "Exit wing", new Rect(33, 12, 5, 2), new Color(0.84f, 0.68f, 0.37f), "");
            AddDiscoveredHidingPoints(content, player, 4f);
            AddExitMarker(content, false);
            return content;
        }

        private static RectTransform BuildPrisonMap(Transform parent, Transform player)
        {
            var content = CreateMapContent(parent, "Level 01 Map Content", new Vector2(10, 13), new Vector2(154, 130));
            AddMapRegion(content, "Cell block", new Rect(2, 2, 6, 7), new Color(0.95f, 0.63f, 0.25f), "BUỒNG");
            AddMapRegion(content, "Vent route", new Rect(9, 2, 9, 5), new Color(0.42f, 0.57f, 0.67f), "ỐNG GIÓ");
            AddMapRegion(content, "Main corridor", new Rect(8, 7, 18, 2), new Color(0.63f, 0.70f, 0.72f), "HÀNH LANG");
            AddMapRegion(content, "West corridor", new Rect(20, 6, 4, 3), new Color(0.63f, 0.70f, 0.72f), "");
            AddMapRegion(content, "Guard office", new Rect(23, 10, 11, 7), new Color(0.48f, 0.58f, 0.63f), "VĂN PHÒNG");
            AddMapRegion(content, "Exit wing", new Rect(33, 12, 5, 2), new Color(0.84f, 0.68f, 0.37f), "CỬA RA");
            AddDiscoveredHidingPoints(content, player, 6f);
            AddExitMarker(content, true);
            return content;
        }

        private static void AddExitMarker(RectTransform parent, bool includeLabel)
        {
            var gate = UnityEngine.Object.FindObjectOfType<GateInteractable>(true);
            var exitObject = gate != null ? gate.gameObject : GameObject.Find("Prison Exit");
            if (exitObject == null) return;

            var worldPosition = (Vector2)exitObject.transform.position;
            var normalized = new Vector2(
                (worldPosition.x - Level01MapBounds.xMin) / Level01MapBounds.width,
                (worldPosition.y - Level01MapBounds.yMin) / Level01MapBounds.height);
            var marker = CreateMapMarker(parent, "Exit marker", new Color(1f, 0.48f, 0.27f));
            marker.anchorMin = marker.anchorMax = normalized;
            marker.sizeDelta = new Vector2(parent.sizeDelta.x < 130f ? 8f : 10f, parent.sizeDelta.x < 130f ? 8f : 10f);
            if (!includeLabel) return;

            var label = CreateText(marker, "Exit label", "RA", 8, new Vector2(0, 0.5f), new Vector2(7, 0), new Vector2(23, 12));
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(1f, 0.82f, 0.55f);
        }

        private static RectTransform CreateMapContent(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void AddMapRegion(RectTransform parent, string name, Rect worldRect, Color tint, string label)
        {
            var bounds = Level01MapBounds;
            var width = parent.sizeDelta.x;
            var height = parent.sizeDelta.y;
            var min = new Vector2((worldRect.xMin - bounds.xMin) / bounds.width, (worldRect.yMin - bounds.yMin) / bounds.height);
            var size = new Vector2(worldRect.width / bounds.width * width, worldRect.height / bounds.height * height);
            var region = CreateImage(parent, name, LoadSprite(SkinPath + "button.png"), min, Vector2.zero,
                Vector2.zero, size, Image.Type.Simple);
            region.color = tint;
            region.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText(region.transform, name + " Label", label, 7, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(2, 2));
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static RectTransform CreateMapMarker(RectTransform parent, string name, Color color)
        {
            var marker = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            marker.transform.SetParent(parent, false);
            var rect = marker.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(7, 7);
            var image = marker.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void AddDiscoveredHidingPoints(RectTransform parent, Transform player, float size)
        {
            var spots = UnityEngine.Object.FindObjectsOfType<HidingSpot>(true);
            foreach (var spot in spots)
            {
                if (spot == null) continue;
                var world = (Vector2)spot.transform.position;
                var normalized = new Vector2(
                    (world.x - Level01MapBounds.xMin) / Level01MapBounds.width,
                    (world.y - Level01MapBounds.yMin) / Level01MapBounds.height);
                var point = new GameObject("Discovered " + spot.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HidingSpotMapReveal));
                point.transform.SetParent(parent, false);
                var rect = point.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = normalized;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                var image = point.GetComponent<Image>();
                image.color = new Color(0.55f, 0.9f, 0.86f);
                image.raycastTarget = false;
                var reveal = point.GetComponent<HidingSpotMapReveal>();
                reveal.Bind(spot, player);
                EditorUtility.SetDirty(reveal);
            }
        }

        private static (GameObject root, Button[] buttons) BuildOverlay(Transform parent, string name, string title, Sprite panelSprite, Sprite buttonSprite, string[] labels, Vector2 size)
        {
            var overlay = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(parent, false);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            var blocker = overlay.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.72f);
            blocker.raycastTarget = true;
            var panel = CreateImage(overlay.transform, name + " Frame", panelSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, Image.Type.Sliced).gameObject;
            CreateText(panel.transform, name + " Heading", title, 21, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(size.x - 30, 34)).alignment = TextAnchor.MiddleCenter;
            var buttons = new Button[labels.Length];
            for (var i = 0; i < labels.Length; i++)
                buttons[i] = CreateButton(panel.transform, labels[i], buttonSprite, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66 - i * 46), new Vector2(Mathf.Min(300, size.x - 50), 36));
            return (overlay, buttons);
        }

        private static (GameObject root, Button[] buttons, Slider slider, Toggle muteToggle) BuildSettings(Transform parent, Sprite panelSprite, Sprite buttonSprite)
        {
            var overlay = new GameObject("Settings Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(parent, false);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            var blocker = overlay.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.72f);
            blocker.raycastTarget = true;
            var panel = CreateImage(overlay.transform, "Settings Panel", panelSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(370, 228), Image.Type.Sliced).gameObject;
            CreateText(panel.transform, "Settings Heading", "CÀI ĐẶT ÂM THANH", 20, new Vector2(0.5f, 1), new Vector2(0, -27), new Vector2(330, 30)).alignment = TextAnchor.MiddleCenter;
            CreateText(panel.transform, "Volume Label", "ÂM LƯỢNG CHUNG", 13, new Vector2(0, 0.68f), new Vector2(26, 0), new Vector2(150, 24));
            var slider = CreateSlider(panel.transform, new Vector2(0, 0.55f), new Vector2(26, 0), new Vector2(315, 18));
            var mute = CreateToggle(panel.transform, "Tắt âm", new Vector2(0, 0.35f), new Vector2(31, 0));
            var back = CreateButton(panel.transform, "QUAY LẠI", buttonSprite, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(150, 34));
            return (overlay, new[] { back }, slider, mute);
        }

        private static Slider CreateSlider(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var root = new GameObject("Master Volume", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var background = CreateImage(root.transform, "Background", LoadSprite(SkinPath + "button.png"), Vector2.zero, Vector2.zero, Vector2.zero, size, Image.Type.Sliced);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var fillRect = fillArea.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0.2f); fillRect.anchorMax = new Vector2(1, 0.8f); fillRect.offsetMin = new Vector2(5, 0); fillRect.offsetMax = new Vector2(-5, 0);
            var fill = CreateImage(fillArea.transform, "Fill", LoadSprite(SkinPath + "warning.png"), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Image.Type.Sliced);
            var handle = CreateImage(root.transform, "Handle", LoadSprite(SkinPath + "slot_selected.png"), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 22), Image.Type.Sliced);
            handle.raycastTarget = true;
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            background.raycastTarget = false;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string label, Vector2 anchor, Vector2 position)
        {
            var root = new GameObject("Mute Toggle", typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = new Vector2(220, 30);
            var box = CreateImage(root.transform, "Toggle Box", LoadSprite(SkinPath + "button.png"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(15, 0), new Vector2(20, 20), Image.Type.Sliced);
            box.raycastTarget = true;
            var check = CreateImage(box.transform, "Checkmark", LoadSprite(SkinPath + "warning.png"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12), Image.Type.Sliced);
            CreateText(root.transform, "Toggle Label", label, 13, new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(150, 24));
            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = box; toggle.graphic = check;
            return toggle;
        }

        private static Button CreateButton(Transform parent, string label, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var image = CreateImage(parent, label, sprite, anchor, pivot, position, size, Image.Type.Sliced);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var text = CreateText(image.transform, "Label", label, 14, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(14, 6));
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            image.gameObject.AddComponent<UIButtonAudio>();
            return button;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 anchor, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite[] LoadSupportIcons()
        {
            var ids = new[] { "spoon", "keycard", "brass_key", "wire_cutters", "shovel", "map_fragment", "cipher_note", "civilian_clothes", "stone", "tin_can", "glass_bottle", "firecracker", "meat", "energy_drink", "boat_key", "port_pass" };
            var result = new List<Sprite>();
            foreach (var id in ids)
            {
                var sprite = LoadSprite(IconPath + id + ".png");
                if (sprite != null) result.Add(sprite);
            }
            return result.ToArray();
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path))
                if (item is Sprite loaded) return loaded;
            if (File.Exists(Path.GetFullPath(path))) Debug.LogWarning("UI image is not imported as a Sprite: " + path);
            return null;
        }

        private static void SetObjectReference(UnityEngine.Object target, string property, UnityEngine.Object reference)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new InvalidOperationException("Serialized field not found: " + target.GetType().Name + "." + property);
            field.objectReferenceValue = reference;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
