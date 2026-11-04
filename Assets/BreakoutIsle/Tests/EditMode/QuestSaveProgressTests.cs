using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public sealed class QuestSaveProgressTests
    {
        private readonly List<LevelObjectiveSet> _definitions = new List<LevelObjectiveSet>();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<string> _tempDirectories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
            foreach (var definition in _definitions)
                Object.DestroyImmediate(definition);
            _definitions.Clear();
            foreach (var directory in _tempDirectories)
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            _tempDirectories.Clear();
        }

        [Test]
        public void IndependentMapFragmentsAndSwitchesCanBeCompletedInAnyOrder()
        {
            var definition = CreateSet(
                Step("fragment-a", outOfOrder: true),
                Step("fragment-b", outOfOrder: true),
                Step("fragment-c", outOfOrder: true),
                Step("switch-a", outOfOrder: true),
                Step("switch-b", outOfOrder: true),
                new ObjectiveStepDefinition { id = "read-map", requiredCompletedObjectiveIds = new[] { "fragment-a", "fragment-b", "fragment-c" } },
                new ObjectiveStepDefinition { id = "leave-yard", prerequisiteId = "read-map", requiredCompletedObjectiveIds = new[] { "switch-a", "switch-b" } });
            var inventory = new QuestInventory();
            var progress = new ObjectiveProgress(definition, inventory);

            Assert.That(progress.TryComplete("fragment-c"), Is.True);
            Assert.That(progress.TryComplete("switch-b"), Is.True);
            Assert.That(progress.TryComplete("fragment-a"), Is.True);
            Assert.That(progress.TryComplete("switch-a"), Is.True);
            Assert.That(progress.TryComplete("fragment-b"), Is.True);
            Assert.That(progress.TryComplete("read-map"), Is.True);
            Assert.That(progress.TryComplete("leave-yard"), Is.True);
        }

        [Test]
        public void WrongValveResetsSequenceWithoutConsumingInventory()
        {
            var definition = CreateSet(
                new ObjectiveStepDefinition { id = "valve-a", sequenceGroupId = "valves", sequenceIndex = 0 },
                new ObjectiveStepDefinition { id = "valve-b", sequenceGroupId = "valves", sequenceIndex = 1 },
                new ObjectiveStepDefinition { id = "valve-c", sequenceGroupId = "valves", sequenceIndex = 2 });
            var inventory = new QuestInventory();
            Assert.That(inventory.AddQuestItem("map-fragment"), Is.True);
            var progress = new ObjectiveProgress(definition, inventory);

            Assert.That(progress.TryComplete("valve-a"), Is.True);
            Assert.That(progress.TryComplete("valve-c"), Is.False);
            Assert.That(progress.CompletedIds, Is.Empty);
            Assert.That(inventory.HasQuestItem("map-fragment"), Is.True);
            Assert.That(progress.TryComplete("valve-a"), Is.True);
            Assert.That(progress.TryComplete("valve-b"), Is.True);
            Assert.That(progress.TryComplete("valve-c"), Is.True);
        }

        [Test]
        public void ObjectiveCannotConsumeItemUntilAllRequirementsPass()
        {
            var definition = CreateSet(
                new ObjectiveStepDefinition { id = "step-1", requiredQuestItemId = "key", consumeRequiredItem = true },
                new ObjectiveStepDefinition { id = "step-2", prerequisiteId = "step-1" });
            var inventory = new QuestInventory();
            Assert.That(inventory.AddQuestItem("key"), Is.True);
            var progress = new ObjectiveProgress(definition, inventory);

            Assert.That(progress.TryComplete("step-2"), Is.False);
            Assert.That(inventory.HasQuestItem("key"), Is.True);
            Assert.That(progress.TryComplete("step-1"), Is.True);
            Assert.That(inventory.HasQuestItem("key"), Is.False);
        }

        [Test]
        public void FailedInventoryRestoreLeavesCurrentInventoryUntouched()
        {
            var inventory = new QuestInventory();
            Assert.That(inventory.AddQuestItem("existing"), Is.True);
            Assert.That(inventory.TryRestore(new[] { "new", "new" }, new string[3]), Is.False);
            Assert.That(inventory.QuestItems, Is.EquivalentTo(new[] { "existing" }));
        }

        [Test]
        public void DuplicateSupportItemTypesRoundTripAsSeparateSlotsAndFourthItemIsRefused()
        {
            var inventory = new QuestInventory();
            Assert.That(inventory.TryAddSupportItem("stone", out var firstSlot), Is.True);
            Assert.That(inventory.TryAddSupportItem("stone", out var secondSlot), Is.True);
            Assert.That(inventory.TryAddSupportItem("can", out var thirdSlot), Is.True);
            Assert.That(inventory.TryAddSupportItem("bottle", out _), Is.False);
            Assert.That(new[] { firstSlot, secondSlot, thirdSlot }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(inventory.AddQuestItem("cipher"), Is.True);

            var restored = new QuestInventory();
            Assert.That(restored.TryRestore(inventory.CopyQuestItems(), inventory.CopySupportItems()), Is.True);
            Assert.That(restored.TryRestore(inventory.CopyQuestItems(), inventory.CopySupportItems()), Is.True);
            Assert.That(restored.SupportItems, Is.EqualTo(new[] { "stone", "stone", "can" }));
            Assert.That(restored.QuestItems, Is.EqualTo(new[] { "cipher" }));
        }

        [Test]
        public void WorldStateRestoreIsIdempotentAndRejectsDuplicateSceneIdsBeforeApplying()
        {
            var door = new TestWorldState("door-east", "open");
            var entities = new IPersistedWorldState[] { door };
            var saved = WorldStateRegistry.Capture(entities);
            door.State = "closed";

            WorldStateRegistry.Restore(entities, saved);
            WorldStateRegistry.Restore(entities, saved);
            Assert.That(door.State, Is.EqualTo("open"));

            var duplicate = new IPersistedWorldState[] { door, new TestWorldState("door-east", "closed") };
            Assert.Throws<System.InvalidOperationException>(() => WorldStateRegistry.Capture(duplicate));
            Assert.Throws<System.ArgumentException>(() => WorldStateRegistry.Restore(duplicate, saved));
            Assert.That(door.State, Is.EqualTo("open"));

            var baselineDoor = new TestWorldState("door-west", "closed") { State = "open" };
            WorldStateRegistry.RestoreBaseline(new IPersistedWorldState[] { baselineDoor });
            Assert.That(baselineDoor.State, Is.EqualTo("closed"));
        }

        [Test]
        public void FailedPickupPrerequisiteRollsBackTheAddedQuestItem()
        {
            var definition = CreateSet(new ObjectiveStepDefinition
            {
                id = "open-vent",
                requiredQuestItemId = "keycard",
                consumeRequiredItem = true
            });
            var sessionObject = new GameObject("session-test");
            sessionObject.SetActive(false);
            _objects.Add(sessionObject);
            var session = sessionObject.AddComponent<LevelSession>();
            var sessionSerialized = new SerializedObject(session);
            sessionSerialized.FindProperty("objectives").objectReferenceValue = definition;
            sessionSerialized.ApplyModifiedPropertiesWithoutUndo();
            sessionObject.SetActive(true);
            session.InitializeRuntimeState();

            var playerObject = new GameObject("player-test");
            playerObject.SetActive(false);
            _objects.Add(playerObject);
            playerObject.AddComponent<PlayerInputReader>();
            var player = playerObject.AddComponent<PlayerInteractor>();
            var playerSerialized = new SerializedObject(player);
            playerSerialized.FindProperty("session").objectReferenceValue = session;
            playerSerialized.ApplyModifiedPropertiesWithoutUndo();
            playerObject.SetActive(true);

            var pickupObject = new GameObject("pickup-test");
            pickupObject.SetActive(false);
            _objects.Add(pickupObject);
            var pickup = pickupObject.AddComponent<QuestPickup>();
            var pickupSerialized = new SerializedObject(pickup);
            pickupSerialized.FindProperty("persistentId").stringValue = "map-fragment-a";
            pickupSerialized.FindProperty("itemId").stringValue = "fragment-a";
            pickupSerialized.FindProperty("objectiveId").stringValue = "open-vent";
            pickupSerialized.ApplyModifiedPropertiesWithoutUndo();
            pickupObject.SetActive(true);

            pickup.Interact(player);

            Assert.That(session.Inventory.HasQuestItem("fragment-a"), Is.False);
            Assert.That(pickup.CaptureWorldState().state, Is.EqualTo("available"));
        }

        [Test]
        public void ObjectiveSaveRequestCapturesPickupAfterWorldCommitAtEndOfFrame()
        {
            var definition = CreateSet(new ObjectiveStepDefinition { id = "collect-fragment", canCompleteOutOfOrder = true });
            definition.levelId = "test-level";
            var sessionObject = new GameObject("session-test");
            sessionObject.SetActive(false);
            _objects.Add(sessionObject);
            var session = sessionObject.AddComponent<LevelSession>();
            SetReference(session, "objectives", definition);
            sessionObject.SetActive(true);
            session.InitializeRuntimeState();

            var playerObject = new GameObject("player-test");
            playerObject.SetActive(false);
            _objects.Add(playerObject);
            playerObject.AddComponent<PlayerInputReader>();
            var player = playerObject.AddComponent<PlayerInteractor>();
            SetReference(player, "session", session);
            playerObject.SetActive(true);

            var anchorObject = new GameObject("checkpoint-test");
            anchorObject.SetActive(false);
            _objects.Add(anchorObject);
            var anchor = anchorObject.AddComponent<CheckpointAnchor>();
            anchorObject.transform.position = new Vector3(7f, 3f, 0f);
            SetString(anchor, "checkpointId", "checkpoint-safe");
            anchorObject.SetActive(true);

            var adapter = new TestCheckpointPlayerAdapter();

            var coordinatorObject = new GameObject("coordinator-test");
            coordinatorObject.SetActive(false);
            _objects.Add(coordinatorObject);
            var coordinator = coordinatorObject.AddComponent<CheckpointSaveCoordinator>();
            SetString(coordinator, "levelId", "test-level");
            SetReference(coordinator, "session", session);
            SetArrayElement(coordinator, "checkpointAnchors", 0, anchor);
            coordinatorObject.SetActive(true);
            typeof(CheckpointSaveCoordinator).GetField("_playerAdapterOverride", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(coordinator, adapter);
            var path = Path.Combine(Path.GetTempPath(), "BreakoutIsleSaveTests", System.Guid.NewGuid().ToString("N"), "profile.json");
            _tempDirectories.Add(Path.GetDirectoryName(path));
            typeof(CheckpointSaveCoordinator).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(coordinator, new AtomicJsonSaveStore(path));

            var pickupObject = new GameObject("pickup-test");
            pickupObject.SetActive(false);
            _objects.Add(pickupObject);
            var pickup = pickupObject.AddComponent<QuestPickup>();
            SetString(pickup, "persistentId", "map-fragment-a");
            SetString(pickup, "itemId", "fragment-a");
            SetString(pickup, "objectiveId", "collect-fragment");
            pickupObject.SetActive(true);
            session.ObjectiveCompleted += _ => coordinator.RequestSave("checkpoint-safe");

            pickup.Interact(player);
            Assert.That(new AtomicJsonSaveStore(path).Load().status, Is.EqualTo(SaveLoadStatus.Missing));

            typeof(CheckpointSaveCoordinator).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(coordinator, null);

            var result = new AtomicJsonSaveStore(path).Load();
            Assert.That(result.status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(result.snapshot.completedObjectiveIds, Does.Contain("collect-fragment"));
            Assert.That(result.snapshot.questItemIds, Does.Contain("fragment-a"));
            Assert.That(result.snapshot.worldEntities.Exists(r => r.stableId == "map-fragment-a" && r.state == "collected"), Is.True);
            Assert.That(coordinator.SavePending, Is.False);

            var persistedEntities = new IPersistedWorldState[] { pickup };
            WorldStateRegistry.RestoreBaseline(persistedEntities);
            session.ResetForNewGame();
            Assert.That(pickup.CaptureWorldState().state, Is.EqualTo("available"));
            Assert.That(coordinator.LoadContinue(), Is.True);
            Assert.That(pickup.CaptureWorldState().state, Is.EqualTo("collected"));
            Assert.That(session.Progress.IsCompleted("collect-fragment"), Is.True);
            Assert.That(session.Inventory.QuestItems, Is.EqualTo(new[] { "fragment-a" }));
            Assert.That(adapter.RestoreCount, Is.EqualTo(1));
            Assert.That(adapter.LastRestoredPosition, Is.EqualTo(new Vector3(7f, 3f, 0f)));
            var directory = Path.GetDirectoryName(path);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            _tempDirectories.Remove(directory);
        }

        [Test]
        public void CorruptProfileBlocksCheckpointSavesUntilExplicitRestartConfirmation()
        {
            var directory = Path.Combine(Path.GetTempPath(), "BreakoutIsleSaveTests", System.Guid.NewGuid().ToString("N"));
            _tempDirectories.Add(directory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "profile.json");
            File.WriteAllText(path, "corrupt-primary");
            File.WriteAllText(path + ".bak", "corrupt-backup");

            var coordinatorObject = new GameObject("coordinator-corrupt-save-test");
            coordinatorObject.SetActive(false);
            _objects.Add(coordinatorObject);
            var coordinator = coordinatorObject.AddComponent<CheckpointSaveCoordinator>();
            coordinatorObject.SetActive(true);
            var store = new AtomicJsonSaveStore(path);
            typeof(CheckpointSaveCoordinator).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(coordinator, store);
            var primaryBefore = File.ReadAllText(path);
            var backupBefore = File.ReadAllText(path + ".bak");

            coordinator.RequestSave("checkpoint-unsafe");

            Assert.That(coordinator.RestartConfirmationIsRequired, Is.True);
            Assert.That(coordinator.SavePending, Is.False);
            Assert.That(store.Load().status, Is.EqualTo(SaveLoadStatus.NeedsRestartConfirmation));
            Assert.That(File.ReadAllText(path), Is.EqualTo(primaryBefore));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backupBefore));
        }

        [Test]
        public void ContinueLevelQueryReadsRecoverableSaveWithoutApplyingIt()
        {
            var directory = Path.Combine(Path.GetTempPath(), "BreakoutIsleSaveTests", System.Guid.NewGuid().ToString("N"));
            _tempDirectories.Add(directory);
            var path = Path.Combine(directory, "profile.json");
            var store = new AtomicJsonSaveStore(path);
            store.Save(SaveSnapshot.Create("level-7", "checkpoint-7"));
            store.Save(SaveSnapshot.Create("level-8", "checkpoint-8"));
            File.WriteAllText(path, "damaged-primary");

            var coordinatorObject = new GameObject("coordinator-level-query-test");
            coordinatorObject.SetActive(false);
            _objects.Add(coordinatorObject);
            var coordinator = coordinatorObject.AddComponent<CheckpointSaveCoordinator>();
            coordinatorObject.SetActive(true);
            typeof(CheckpointSaveCoordinator).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(coordinator, store);

            Assert.That(coordinator.TryGetContinueLevelId(out var levelId), Is.True);
            Assert.That(levelId, Is.EqualTo("level-7"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("damaged-primary"));
        }

        private LevelObjectiveSet CreateSet(params ObjectiveStepDefinition[] steps)
        {
            var definition = ScriptableObject.CreateInstance<LevelObjectiveSet>();
            definition.steps = steps;
            _definitions.Add(definition);
            return definition;
        }

        private static ObjectiveStepDefinition Step(string id, bool outOfOrder) => new ObjectiveStepDefinition { id = id, canCompleteOutOfOrder = outOfOrder };

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArrayElement(UnityEngine.Object target, string propertyName, int index, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(propertyName);
            array.arraySize = index + 1;
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class TestWorldState : IPersistedWorldState
        {
            public string PersistentId { get; }
            public string State { get; set; }
            private readonly string _baseline;
            public TestWorldState(string id, string state) { PersistentId = id; State = state; _baseline = state; }
            public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord { stableId = PersistentId, state = State };
            public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord { stableId = PersistentId, state = _baseline };
            public void RestoreWorldState(WorldEntityRecord record) => State = record.state;
        }

        private sealed class TestCheckpointPlayerAdapter : ICheckpointPlayerAdapter
        {
            public Vector3 LastRestoredPosition { get; private set; }
            public int RestoreCount { get; private set; }
            public void RestoreAt(Vector3 position) { LastRestoredPosition = position; RestoreCount++; }
        }
    }
}
