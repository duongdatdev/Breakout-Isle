using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class CheckpointSaveCoordinator : MonoBehaviour
    {
        [SerializeField] private string levelId;
        [SerializeField] private string initialCheckpointId;
        [SerializeField] private LevelSession session;
        [SerializeField] private MonoBehaviour playerAdapterComponent;
        [SerializeField] private CheckpointAnchor[] checkpointAnchors;

        private AtomicJsonSaveStore _store;
        private string _pendingCheckpointId;
        private bool _chaseActive;
        private bool _saveQueued;
        private bool _restartConfirmationRequired;
        private bool _restartGateChecked;
        private ICheckpointPlayerAdapter _playerAdapterOverride;

        public bool SavePending => !string.IsNullOrEmpty(_pendingCheckpointId);
        public bool RestartConfirmationIsRequired => _restartConfirmationRequired;
        public event Action<bool, string> SavePendingChanged;
        public event Action<string> CheckpointSaved;
        public event Action<string> CheckpointRestored;
        public event Action<SaveLoadStatus> LoadStatusChanged;
        public event Action RestartConfirmationRequired;
        public event Action<string> SaveFailed;

        private ICheckpointPlayerAdapter PlayerAdapter => _playerAdapterOverride ?? playerAdapterComponent as ICheckpointPlayerAdapter;

        public SaveLoadStatus CheckContinueAvailability()
        {
            EnsureStore();
            return _store.Load().status;
        }

        public bool TryGetContinueLevelId(out string savedLevelId)
        {
            EnsureStore();
            var result = _store.Load();
            savedLevelId = null;
            if ((result.status != SaveLoadStatus.LoadedPrimary && result.status != SaveLoadStatus.RecoveredBackup) ||
                result.snapshot == null || string.IsNullOrWhiteSpace(result.snapshot.levelId))
                return false;
            savedLevelId = result.snapshot.levelId;
            return true;
        }

        public bool CanContinue
        {
            get
            {
                var status = CheckContinueAvailability();
                return status == SaveLoadStatus.LoadedPrimary || status == SaveLoadStatus.RecoveredBackup;
            }
        }

        private void Awake()
        {
            _store = new AtomicJsonSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "breakout-isle-profile.json"));
            if (session == null) session = FindObjectOfType<LevelSession>();
            if (playerAdapterComponent == null)
            {
                var behaviours = FindObjectsOfType<MonoBehaviour>(true);
                foreach (var behaviour in behaviours)
                    if (behaviour is ICheckpointPlayerAdapter) { playerAdapterComponent = behaviour; break; }
            }
            if (string.IsNullOrWhiteSpace(levelId) && session != null && session.Objectives != null)
                levelId = session.Objectives.levelId;
        }

        public bool StartNewGame(string checkpointId)
        {
            EnsureStore();
            var existing = _store.Load();
            LoadStatusChanged?.Invoke(existing.status);
            if (existing.status == SaveLoadStatus.NeedsRestartConfirmation)
            {
                _restartConfirmationRequired = true;
                _restartGateChecked = true;
                RestartConfirmationRequired?.Invoke();
                return false;
            }
            _restartConfirmationRequired = false;
            _restartGateChecked = true;
            return StartNewGameCore(checkpointId);
        }

        public bool ConfirmRestartAndStartNewGame(string checkpointId)
        {
            EnsureStore();
            var status = _store.Load().status;
            LoadStatusChanged?.Invoke(status);
            if (!_restartConfirmationRequired && status != SaveLoadStatus.NeedsRestartConfirmation)
                return StartNewGame(checkpointId);
            _restartConfirmationRequired = false;
            _restartGateChecked = true;
            return StartNewGameCore(checkpointId);
        }

        private bool StartNewGameCore(string checkpointId)
        {
            if (session == null || session.Objectives == null || session.Objectives.levelId != levelId ||
                !TryResolveAnchor(checkpointId, out var anchor) || PlayerAdapter == null)
                return Fail("Cannot start: level session or authored safe checkpoint is missing.");
            try
            {
                var entities = FindPersistedEntities();
                WorldStateRegistry.RestoreBaseline(entities);
                session.ResetForNewGame();
                PlayerAdapter.RestoreAt(anchor.SpawnPosition);
                if (Application.isPlaying) GuardBrain.ResetAllToBaseline();
                _pendingCheckpointId = null;
                _saveQueued = false;
                return SaveNow(checkpointId);
            }
            catch (Exception exception)
            {
                return Fail("Could not initialize a new game: " + exception.Message);
            }
        }

        public bool LoadContinue()
        {
            EnsureStore();
            var result = _store.Load();
            LoadStatusChanged?.Invoke(result.status);
            _restartGateChecked = true;
            if (result.status == SaveLoadStatus.Missing) return Fail("No saved game exists.");
            if (result.status == SaveLoadStatus.NeedsRestartConfirmation)
            {
                _restartConfirmationRequired = true;
                RestartConfirmationRequired?.Invoke();
                return false;
            }
            _restartConfirmationRequired = false;
            if (result.snapshot == null || result.snapshot.levelId != levelId || !TryResolveAnchor(result.snapshot.checkpointId, out var anchor))
                return Fail("Save location is not available in the loaded scene.");
            var sessionError = session == null ? "Level session is not available." : null;
            if (session == null || !session.CanApplySnapshot(result.snapshot, out sessionError))
                return Fail("Saved quest state cannot be applied: " + sessionError);
            if (PlayerAdapter == null)
                return Fail("Checkpoint player adapter is not assigned or does not implement ICheckpointPlayerAdapter.");

            var entities = FindPersistedEntities();
            if (!WorldStateRegistry.ValidateRestore(entities, result.snapshot.worldEntities, out var worldError))
                return Fail("Saved world state cannot be applied: " + worldError);
            try
            {
                WorldStateRegistry.Restore(entities, result.snapshot.worldEntities);
                if (!session.TryApplySnapshot(result.snapshot, out sessionError))
                    return Fail("Saved quest state could not be applied: " + sessionError);
                PlayerAdapter.RestoreAt(anchor.SpawnPosition);
                if (Application.isPlaying) GuardBrain.ResetAllToBaseline();
                var hadPendingSave = SavePending;
                var pendingId = _pendingCheckpointId;
                _pendingCheckpointId = null;
                _saveQueued = false;
                _chaseActive = false;
                if (hadPendingSave) SavePendingChanged?.Invoke(false, pendingId);
                CheckpointRestored?.Invoke(anchor.CheckpointId);
                return true;
            }
            catch (Exception exception)
            {
                return Fail("Continue failed: " + exception.Message);
            }
        }

        public bool RetryFromCheckpoint() => LoadContinue();

        public void RequestSave(string checkpointId)
        {
            EnsureRestartGate();
            if (_restartConfirmationRequired)
            {
                Fail("A corrupt save requires explicit restart confirmation before saving.");
                return;
            }
            if (!TryResolveAnchor(checkpointId, out _))
            {
                Fail("Save request references an unknown authored checkpoint.");
                return;
            }
            _pendingCheckpointId = checkpointId;
            _saveQueued = !_chaseActive;
            SavePendingChanged?.Invoke(true, checkpointId);
        }

        public void SetChaseActive(bool active)
        {
            _chaseActive = active;
            if (!_chaseActive && SavePending) _saveQueued = true;
        }

        private void LateUpdate()
        {
            if (!_saveQueued || _chaseActive || !SavePending) return;
            _saveQueued = false;
            var pending = _pendingCheckpointId;
            if (!SaveNow(pending)) return;
            _pendingCheckpointId = null;
            SavePendingChanged?.Invoke(false, pending);
        }

        public bool SaveInitialCheckpoint()
        {
            var checkpointId = string.IsNullOrWhiteSpace(initialCheckpointId) ? FindFirstCheckpointId() : initialCheckpointId;
            EnsureStore();
            var existing = _store.Load();
            LoadStatusChanged?.Invoke(existing.status);
            _restartGateChecked = true;
            if (existing.status == SaveLoadStatus.Missing) return StartNewGameCore(checkpointId);
            if (existing.status == SaveLoadStatus.NeedsRestartConfirmation)
            {
                _restartConfirmationRequired = true;
                RestartConfirmationRequired?.Invoke();
            }
            else _restartConfirmationRequired = false;
            return false;
        }

        private bool SaveNow(string checkpointId)
        {
            EnsureRestartGate();
            if (_restartConfirmationRequired)
                return Fail("A corrupt save requires explicit restart confirmation before saving.");
            if (session == null || session.Objectives == null || session.Objectives.levelId != levelId ||
                session.Inventory == null || session.Progress == null || !TryResolveAnchor(checkpointId, out _))
                return Fail("Cannot save: session, progress, inventory, or authored checkpoint is unavailable.");
            try
            {
                var snapshot = SaveSnapshot.Create(levelId, checkpointId);
                snapshot.outfitId = session.OutfitId;
                snapshot.selectedSupportSlot = session.SelectedSupportSlot;
                snapshot.supportItemIds = session.Inventory.CopySupportItems();
                snapshot.questItemIds = session.Inventory.CopyQuestItems();
                snapshot.completedObjectiveIds = new List<string>(session.Progress.CompletedIds);
                foreach (var flag in session.PersistentStoryFlags)
                    snapshot.storyFlags.Add(new StoryFlagRecord { id = flag.id, value = flag.value });
                snapshot.worldEntities = WorldStateRegistry.Capture(FindPersistedEntities());
                _store.Save(snapshot);
                CheckpointSaved?.Invoke(checkpointId);
                return true;
            }
            catch (Exception exception)
            {
                return Fail("Save failed: " + exception.Message);
            }
        }

        private bool TryResolveAnchor(string checkpointId, out CheckpointAnchor anchor)
        {
            anchor = null;
            if (checkpointAnchors == null || string.IsNullOrWhiteSpace(checkpointId)) return false;
            foreach (var candidate in checkpointAnchors)
            {
                if (candidate == null || candidate.CheckpointId != checkpointId) continue;
                if (anchor != null) return false;
                anchor = candidate;
            }
            return anchor != null && !string.IsNullOrWhiteSpace(levelId);
        }

        private string FindFirstCheckpointId()
        {
            if (checkpointAnchors == null) return null;
            foreach (var anchor in checkpointAnchors)
                if (anchor != null && !string.IsNullOrWhiteSpace(anchor.CheckpointId)) return anchor.CheckpointId;
            return null;
        }

        private static List<IPersistedWorldState> FindPersistedEntities()
        {
            return FindObjectsOfType<MonoBehaviour>(true).OfType<IPersistedWorldState>().ToList();
        }

        private bool Fail(string message)
        {
            SaveFailed?.Invoke(message);
            return false;
        }

        private void EnsureStore()
        {
            if (_store == null)
                _store = new AtomicJsonSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "breakout-isle-profile.json"));
        }

        private void EnsureRestartGate()
        {
            if (_restartGateChecked) return;
            EnsureStore();
            var status = _store.Load().status;
            _restartGateChecked = true;
            if (status == SaveLoadStatus.NeedsRestartConfirmation)
            {
                _restartConfirmationRequired = true;
                RestartConfirmationRequired?.Invoke();
            }
        }
    }
}
