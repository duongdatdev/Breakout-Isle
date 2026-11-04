using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class LevelSession : MonoBehaviour
    {
        [SerializeField] private LevelObjectiveSet objectives;
        [SerializeField] private string outfitId = "prison";
        [SerializeField, Range(0, 2)] private int selectedSupportSlot;
        [SerializeField] private List<StoryFlagRecord> persistentStoryFlags = new List<StoryFlagRecord>();

        public QuestInventory Inventory { get; private set; }
        public ObjectiveProgress Progress { get; private set; }
        public LevelObjectiveSet Objectives => objectives;
        public string OutfitId => outfitId;
        public int SelectedSupportSlot => selectedSupportSlot;
        public IReadOnlyList<StoryFlagRecord> PersistentStoryFlags => persistentStoryFlags;
        public event Action InventoryChanged;
        public event Action<string> MessageShown;
        public event Action<string> ObjectiveCompleted;
        public event Action QuestCompleted;
        public event Action<string> ObjectiveReset;

        private void Awake() => InitializeRuntimeState();

        public void InitializeRuntimeState()
        {
            Inventory = new QuestInventory();
            if (objectives == null)
            {
                Debug.LogError("LevelSession needs a LevelObjectiveSet reference.", this);
                return;
            }
            Progress = new ObjectiveProgress(objectives, Inventory);
            Progress.Completed += step =>
            {
                InventoryChanged?.Invoke();
                ObjectiveCompleted?.Invoke(step.id);
                if (Progress.Current == null) QuestCompleted?.Invoke();
            };
            Progress.ObjectiveReset += id => ObjectiveReset?.Invoke(id);
        }

        public void ResetForNewGame()
        {
            outfitId = "prison";
            selectedSupportSlot = 0;
            persistentStoryFlags = new List<StoryFlagRecord>();
            InitializeRuntimeState();
            InventoryChanged?.Invoke();
        }

        public bool CompleteObjective(string objectiveId)
        {
            if (Progress != null && Progress.TryComplete(objectiveId))
                return true;
            ShowMessage("Requirements are not met.");
            return false;
        }

        public void ShowMessage(string message) => MessageShown?.Invoke(message);

        internal void NotifyInventoryChanged() => InventoryChanged?.Invoke();

        public bool SetSelectedSupportSlot(int slot)
        {
            if (slot < 0 || slot > 2) return false;
            selectedSupportSlot = slot;
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryAddSupportItem(string itemId, out int slot)
        {
            slot = -1;
            if (Inventory == null || !Inventory.TryAddSupportItem(itemId, out slot))
            {
                ShowMessage("Túi đồ đã đầy.");
                return false;
            }
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryConsumeSelectedSupportItem(out string itemId)
        {
            return TryConsumeSupportItemAt(selectedSupportSlot, out itemId);
        }

        public bool TryConsumeSupportItemAt(int slot, out string itemId)
        {
            itemId = null;
            if (Inventory == null || !Inventory.TryRemoveSupportItemAt(slot, out itemId))
                return false;
            InventoryChanged?.Invoke();
            return true;
        }

        public bool SetOutfit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            outfitId = id;
            return true;
        }

        public void SetStoryFlag(string id, bool value)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Story flag ID is required.", nameof(id));
            var flag = persistentStoryFlags.Find(x => x != null && x.id == id);
            if (flag == null) persistentStoryFlags.Add(new StoryFlagRecord { id = id, value = value });
            else flag.value = value;
        }

        public bool CanApplySnapshot(SaveSnapshot snapshot, out string error)
        {
            error = null;
            if (snapshot == null || !snapshot.TryGetValidationError(out error)) return false;
            if (objectives == null || snapshot.levelId != objectives.levelId)
            {
                error = "Save level does not match the loaded level objective set.";
                return false;
            }
            if (Inventory == null || Progress == null ||
                !Inventory.CanRestore(snapshot.questItemIds, snapshot.supportItemIds) ||
                !Progress.CanRestoreCompleted(snapshot.completedObjectiveIds))
            {
                error = "Quest or inventory snapshot does not match this level.";
                return false;
            }
            return true;
        }

        public bool TryApplySnapshot(SaveSnapshot snapshot, out string error)
        {
            if (!CanApplySnapshot(snapshot, out error)) return false;
            if (!Inventory.TryRestore(snapshot.questItemIds, snapshot.supportItemIds) ||
                !Progress.TryRestoreCompleted(snapshot.completedObjectiveIds))
            {
                error = "Quest or inventory state could not be restored.";
                return false;
            }
            outfitId = snapshot.outfitId;
            selectedSupportSlot = snapshot.selectedSupportSlot;
            persistentStoryFlags = new List<StoryFlagRecord>();
            foreach (var flag in snapshot.storyFlags)
                persistentStoryFlags.Add(new StoryFlagRecord { id = flag.id, value = flag.value });
            InventoryChanged?.Invoke();
            return true;
        }
    }
}
