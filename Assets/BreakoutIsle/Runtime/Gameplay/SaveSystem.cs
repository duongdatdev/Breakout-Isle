using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [Serializable]
    public sealed class StoryFlagRecord
    {
        public string id;
        public bool value;
    }

    [Serializable]
    public sealed class WorldEntityRecord
    {
        public string stableId;
        public string state;
    }

    public interface IPersistedWorldState
    {
        string PersistentId { get; }
        WorldEntityRecord CaptureWorldState();
        WorldEntityRecord CaptureBaselineWorldState();
        void RestoreWorldState(WorldEntityRecord record);
    }

    public static class WorldStateRegistry
    {
        public static List<WorldEntityRecord> Capture(IEnumerable<IPersistedWorldState> entities)
        {
            var byId = IndexEntities(entities);
            var records = new List<WorldEntityRecord>(byId.Count);
            foreach (var entity in byId.Values)
            {
                var record = entity.CaptureWorldState();
                if (record == null || record.stableId != entity.PersistentId || string.IsNullOrWhiteSpace(record.state))
                    throw new InvalidOperationException("World-state capture returned an invalid record for " + entity.PersistentId + ".");
                records.Add(record);
            }
            records.Sort((a, b) => string.CompareOrdinal(a.stableId, b.stableId));
            return records;
        }

        public static bool ValidateRestore(IEnumerable<IPersistedWorldState> entities, IReadOnlyList<WorldEntityRecord> records, out string error)
        {
            error = null;
            Dictionary<string, IPersistedWorldState> byId;
            try { byId = IndexEntities(entities != null ? entities.ToList() : null); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (records == null || records.Count != byId.Count)
            {
                error = "World-state record count does not match the scene entity set.";
                return false;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.stableId) || string.IsNullOrWhiteSpace(record.state) ||
                    !seen.Add(record.stableId) || !byId.ContainsKey(record.stableId))
                {
                    error = "World-state records contain an unknown, duplicate, or incomplete stable ID.";
                    return false;
                }
            }
            return true;
        }

        public static void Restore(IEnumerable<IPersistedWorldState> entities, IReadOnlyList<WorldEntityRecord> records)
        {
            var entityList = entities != null ? entities.ToList() : null;
            if (!ValidateRestore(entityList, records, out var validationError))
                throw new ArgumentException(validationError, nameof(records));
            var byId = IndexEntities(entityList);
            var before = Capture(byId.Values);
            try
            {
                foreach (var record in records)
                    byId[record.stableId].RestoreWorldState(record);
            }
            catch
            {
                foreach (var record in before)
                    byId[record.stableId].RestoreWorldState(record);
                throw;
            }
        }

        public static void RestoreBaseline(IEnumerable<IPersistedWorldState> entities)
        {
            var entityList = entities != null ? entities.ToList() : null;
            var byId = IndexEntities(entityList);
            var baseline = new List<WorldEntityRecord>(byId.Count);
            foreach (var entity in byId.Values)
            {
                var record = entity.CaptureBaselineWorldState();
                if (record == null || record.stableId != entity.PersistentId || string.IsNullOrWhiteSpace(record.state))
                    throw new InvalidOperationException("World-state baseline is invalid for " + entity.PersistentId + ".");
                baseline.Add(record);
            }
            baseline.Sort((a, b) => string.CompareOrdinal(a.stableId, b.stableId));
            Restore(entityList, baseline);
        }

        private static Dictionary<string, IPersistedWorldState> IndexEntities(IEnumerable<IPersistedWorldState> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var byId = new Dictionary<string, IPersistedWorldState>(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.PersistentId) || byId.ContainsKey(entity.PersistentId))
                    throw new InvalidOperationException("Scene world-state entities must have unique, nonempty stable IDs.");
                byId.Add(entity.PersistentId, entity);
            }
            return byId;
        }
    }

    [Serializable]
    public sealed class SaveSnapshot
    {
        // Zero is deliberately invalid. Missing JSON fields must never silently
        // deserialize into a valid current-version save.
        public int schemaVersion;
        public string levelId;
        public string checkpointId;
        public string outfitId;
        public int selectedSupportSlot;
        public string[] supportItemIds;
        public List<string> completedObjectiveIds;
        public List<string> questItemIds;
        public List<StoryFlagRecord> storyFlags;
        public List<WorldEntityRecord> worldEntities;

        public bool IsValid => TryGetValidationError(out _);

        public bool TryGetValidationError(out string error)
        {
            error = null;
            if (schemaVersion != CurrentSchemaVersion) error = "Unsupported or missing save schema version.";
            else if (string.IsNullOrWhiteSpace(levelId) || string.IsNullOrWhiteSpace(checkpointId)) error = "Save location is incomplete.";
            else if (string.IsNullOrWhiteSpace(outfitId)) error = "Outfit ID is missing.";
            else if (selectedSupportSlot < 0 || selectedSupportSlot > 2) error = "Selected support slot must be between 0 and 2.";
            else if (supportItemIds == null || supportItemIds.Length != 3) error = "Exactly three support slots are required.";
            else if (completedObjectiveIds == null || questItemIds == null || storyFlags == null || worldEntities == null) error = "A save collection is missing.";
            else if (!AllUniqueNonEmpty(completedObjectiveIds)) error = "Objective IDs must be nonempty and unique.";
            else if (!AllUniqueNonEmpty(questItemIds)) error = "Quest item IDs must be nonempty and unique.";
            else if (!AllUniqueNonEmpty(storyFlags, x => x != null ? x.id : null)) error = "Story flag IDs must be nonempty and unique.";
            else if (!AllUniqueNonEmpty(worldEntities, x => x != null ? x.stableId : null)) error = "World entity IDs must be nonempty and unique.";
            else if (!AllNonEmpty(worldEntities, x => x != null ? x.state : null)) error = "World entity state is missing.";
            return error == null;
        }

        public const int CurrentSchemaVersion = 1;

        public static SaveSnapshot Create(string levelId, string checkpointId)
        {
            return new SaveSnapshot
            {
                schemaVersion = CurrentSchemaVersion,
                levelId = levelId,
                checkpointId = checkpointId,
                outfitId = "prison",
                selectedSupportSlot = 0,
                supportItemIds = new string[3],
                completedObjectiveIds = new List<string>(),
                questItemIds = new List<string>(),
                storyFlags = new List<StoryFlagRecord>(),
                worldEntities = new List<WorldEntityRecord>()
            };
        }

        private static bool AllUniqueNonEmpty(IEnumerable<string> values, bool allowEmpty = false)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (allowEmpty) continue;
                    return false;
                }
                if (!seen.Add(value)) return false;
            }
            return true;
        }

        private static bool AllUniqueNonEmpty<T>(IEnumerable<T> values, Func<T, string> getId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var id = getId(value);
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return false;
            }
            return true;
        }

        private static bool AllNonEmpty<T>(IEnumerable<T> values, Func<T, string> getValue)
        {
            foreach (var value in values)
                if (string.IsNullOrWhiteSpace(getValue(value))) return false;
            return true;
        }
    }

    public enum SaveLoadStatus
    {
        Missing,
        LoadedPrimary,
        RecoveredBackup,
        NeedsRestartConfirmation
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus status;
        public SaveSnapshot snapshot;
    }

    public sealed class AtomicJsonSaveStore
    {
        private readonly string _primaryPath;
        private readonly string _backupPath;
        private readonly string _temporaryPath;

        public AtomicJsonSaveStore(string primaryPath)
        {
            _primaryPath = primaryPath ?? throw new ArgumentNullException(nameof(primaryPath));
            _backupPath = _primaryPath + ".bak";
            _temporaryPath = _primaryPath + ".tmp";
        }

        public void Save(SaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentException("Save snapshot is null.", nameof(snapshot));
            if (!snapshot.TryGetValidationError(out var error))
                throw new ArgumentException(error, nameof(snapshot));

            var directory = Path.GetDirectoryName(_primaryPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
            try
            {
                using (var stream = new FileStream(_temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(_primaryPath))
                    File.Replace(_temporaryPath, _primaryPath, TryRead(_primaryPath, out _) ? _backupPath : null, true);
                else
                    File.Move(_temporaryPath, _primaryPath);
            }
            finally
            {
                if (File.Exists(_temporaryPath))
                    File.Delete(_temporaryPath);
            }
        }

        public SaveLoadResult Load()
        {
            if (!File.Exists(_primaryPath))
            {
                if (TryRead(_backupPath, out var backup))
                    return new SaveLoadResult { status = SaveLoadStatus.RecoveredBackup, snapshot = backup };
                return new SaveLoadResult { status = File.Exists(_backupPath) ? SaveLoadStatus.NeedsRestartConfirmation : SaveLoadStatus.Missing };
            }

            if (TryRead(_primaryPath, out var primary))
                return new SaveLoadResult { status = SaveLoadStatus.LoadedPrimary, snapshot = primary };
            if (TryRead(_backupPath, out var recovered))
                return new SaveLoadResult { status = SaveLoadStatus.RecoveredBackup, snapshot = recovered };

            return new SaveLoadResult { status = SaveLoadStatus.NeedsRestartConfirmation };
        }

        private static bool TryRead(string path, out SaveSnapshot snapshot)
        {
            snapshot = null;
            if (!File.Exists(path))
                return false;

            try
            {
                snapshot = JsonUtility.FromJson<SaveSnapshot>(File.ReadAllText(path, Encoding.UTF8));
                return snapshot != null && snapshot.IsValid;
            }
            catch (Exception)
            {
                snapshot = null;
                return false;
            }
        }
    }
}
