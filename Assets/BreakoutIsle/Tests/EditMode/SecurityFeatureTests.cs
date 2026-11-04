using System.Collections.Generic;
using System.Reflection;
using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public sealed class SecurityFeatureDraftTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<LevelObjectiveSet> _definitions = new List<LevelObjectiveSet>();
        private readonly List<Sprite> _sprites = new List<Sprite>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            foreach (var definition in _definitions)
                if (definition != null) Object.DestroyImmediate(definition);
            foreach (var sprite in _sprites)
                if (sprite != null) Object.DestroyImmediate(sprite);
            _objects.Clear();
            _definitions.Clear();
            _sprites.Clear();
        }

        [Test]
        public void SwitchCompletesObjectiveBeforePowerMutationAndCannotRepeatObjectiveAction()
        {
            var session = CreateSession(new ObjectiveStepDefinition
            {
                id = "power_cut",
                requiredQuestItemId = "breaker_key",
                consumeRequiredItem = true
            });
            var (switchObject, powerSwitch, revealedObject, _) = CreateSwitch(session, "power-cut", "power_cut");
            var player = CreatePlayer(session);

            powerSwitch.Interact(player);

            Assert.That(session.Progress.IsCompleted("power_cut"), Is.False);
            Assert.That(session.Inventory.HasQuestItem("breaker_key"), Is.False);
            Assert.That(powerSwitch.CaptureWorldState().state, Is.EqualTo("on"));
            Assert.That(revealedObject.activeSelf, Is.False);

            session.Inventory.AddQuestItem("breaker_key");
            powerSwitch.Interact(player);

            Assert.That(session.Progress.IsCompleted("power_cut"), Is.True);
            Assert.That(session.Inventory.HasQuestItem("breaker_key"), Is.False);
            Assert.That(powerSwitch.CaptureWorldState().state, Is.EqualTo("off"));
            Assert.That(revealedObject.activeSelf, Is.True);
            Assert.That(powerSwitch.CanInteract(player), Is.False);

            powerSwitch.Interact(player);
            Assert.That(powerSwitch.CaptureWorldState().state, Is.EqualTo("off"));
            Assert.That(session.Progress.CompletedIds, Has.Count.EqualTo(1));
            Assert.That(switchObject, Is.Not.Null);
        }

        [Test]
        public void RestoringSwitchStateReappliesDerivedConsumerStateWithoutSavingConsumerSeparately()
        {
            var session = CreateSession();
            var (_, powerSwitch, revealedObject, chestConsumer) = CreateSwitch(session, "power-cut", string.Empty);
            var entities = new IPersistedWorldState[] { powerSwitch };

            WorldStateRegistry.Restore(entities, new[] { new WorldEntityRecord { stableId = "power-cut", state = "off" } });
            Assert.That(powerSwitch.IsPowered, Is.False);
            Assert.That(revealedObject.activeSelf, Is.True);
            Assert.That(chestConsumer, Is.InstanceOf<IPowerConsumer>());
            Assert.That(chestConsumer, Is.Not.InstanceOf<IPersistedWorldState>());

            WorldStateRegistry.RestoreBaseline(entities);
            Assert.That(powerSwitch.IsPowered, Is.True);
            Assert.That(revealedObject.activeSelf, Is.False);
        }

        [Test]
        public void ChestConsumesLaundryKeyRewardsYardKeyAndRestoresItsOpenVisualState()
        {
            var session = CreateSession(new ObjectiveStepDefinition
            {
                id = "open_chest",
                requiredQuestItemId = "laundry_chest_key",
                consumeRequiredItem = true,
                rewardQuestItemId = "yard_gate_key"
            });
            var player = CreatePlayer(session);
            var chestObject = NewObject("quest-chest", false);
            var renderer = chestObject.AddComponent<SpriteRenderer>();
            var chest = chestObject.AddComponent<AnimatedQuestChest>();
            var frame = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _sprites.Add(frame);
            SetField(chest, "persistentId", "yard-key-chest");
            SetField(chest, "objectiveId", "open_chest");
            SetField(chest, "session", session);
            SetField(chest, "spriteRenderer", renderer);
            SetField(chest, "openFrames", new[] { frame });
            chestObject.SetActive(true);

            chest.Interact(player);
            Assert.That(chest.IsOpened, Is.False);
            Assert.That(session.Progress.IsCompleted("open_chest"), Is.False);

            session.Inventory.AddQuestItem("laundry_chest_key");
            chest.Interact(player);
            Assert.That(chest.IsOpened, Is.True);
            Assert.That(session.Inventory.HasQuestItem("laundry_chest_key"), Is.False);
            Assert.That(session.Inventory.HasQuestItem("yard_gate_key"), Is.True);
            Assert.That(chest.CaptureWorldState().state, Is.EqualTo("open"));

            WorldStateRegistry.RestoreBaseline(new IPersistedWorldState[] { chest });
            Assert.That(chest.IsOpened, Is.False);
            WorldStateRegistry.Restore(new IPersistedWorldState[] { chest }, new[]
            {
                new WorldEntityRecord { stableId = "yard-key-chest", state = "open" }
            });
            Assert.That(chest.IsOpened, Is.True);
        }

        [Test]
        public void CameraViewUsesArcRangeAndWallOcclusionAndIgnoresHiddenPlayer()
        {
            Assert.That(SecurityCamera.IsWithinView(Vector2.zero, Vector2.right, new Vector2(5f, 1.5f), 6f, 60f), Is.True);
            Assert.That(SecurityCamera.IsWithinView(Vector2.zero, Vector2.right, new Vector2(5f, 3.5f), 6f, 60f), Is.False);
            Assert.That(SecurityCamera.IsWithinView(Vector2.zero, Vector2.right, new Vector2(6.1f, 0f), 6f, 60f), Is.False);

            var wall = new GameObject("camera-occluder") { layer = 0 };
            _objects.Add(wall);
            wall.transform.position = new Vector2(2f, 0f);
            var wallCollider = wall.AddComponent<BoxCollider2D>();
            wallCollider.size = new Vector2(0.4f, 2f);
            Physics2D.SyncTransforms();
            Assert.That(SecurityCamera.HasClearLineOfSight(Vector2.zero, new Vector2(4f, 0f), 1 << 0, null, null), Is.False);
            wall.SetActive(false);
            Physics2D.SyncTransforms();
            Assert.That(SecurityCamera.HasClearLineOfSight(Vector2.zero, new Vector2(4f, 0f), 1 << 0, null, null), Is.True);

            var playerObject = NewObject("camera-player");
            playerObject.transform.position = new Vector2(0f, 4f);
            var stealth = playerObject.AddComponent<PlayerStealthState>();
            var spotObject = NewObject("camera-hiding-spot");
            spotObject.transform.position = playerObject.transform.position;
            var spot = spotObject.AddComponent<HidingSpot>();
            var cameraObject = NewObject("security-camera", false);
            var camera = cameraObject.AddComponent<SecurityCamera>();
            SetField(camera, "player", stealth);
            SetField(camera, "obstacleMask", (LayerMask)0);
            cameraObject.SetActive(true);
            camera.SetPowered(true);
            Assert.That(camera.CanObserve(stealth), Is.True);
            stealth.EnterHidingSpot(spot, false);
            Assert.That(camera.CanObserve(stealth), Is.False);
        }

        private LevelSession CreateSession(params ObjectiveStepDefinition[] steps)
        {
            var definition = ScriptableObject.CreateInstance<LevelObjectiveSet>();
            definition.levelId = "security-draft";
            definition.steps = steps;
            _definitions.Add(definition);

            var sessionObject = NewObject("security-session", false);
            var session = sessionObject.AddComponent<LevelSession>();
            SetField(session, "objectives", definition);
            sessionObject.SetActive(true);
            session.InitializeRuntimeState();
            return session;
        }

        private (GameObject, PowerSwitchInteractable, GameObject, ChestPowerReveal) CreateSwitch(
            LevelSession session, string persistentId, string objectiveId)
        {
            var revealedObject = NewObject("revealed-chest", false);
            var consumerObject = NewObject("chest-power-consumer", false);
            var consumer = consumerObject.AddComponent<ChestPowerReveal>();
            SetField(consumer, "objectsRevealedWhenUnpowered", new[] { revealedObject });
            consumerObject.SetActive(true);

            var switchObject = NewObject("power-switch", false);
            var powerSwitch = switchObject.AddComponent<PowerSwitchInteractable>();
            SetField(powerSwitch, "persistentId", persistentId);
            SetField(powerSwitch, "objectiveId", objectiveId);
            SetField(powerSwitch, "session", session);
            SetField(powerSwitch, "linkedConsumers", new MonoBehaviour[] { consumer });
            switchObject.SetActive(true);
            InvokePrivate(powerSwitch, "Awake");
            return (switchObject, powerSwitch, revealedObject, consumer);
        }

        private PlayerInteractor CreatePlayer(LevelSession session)
        {
            var playerObject = NewObject("security-player", false);
            var interactor = playerObject.AddComponent<PlayerInteractor>();
            SetField(interactor, "session", session);
            return interactor;
        }

        private GameObject NewObject(string name, bool active = true)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(active);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing serialized field " + name);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing lifecycle method " + methodName);
            method.Invoke(target, null);
        }
    }
}
