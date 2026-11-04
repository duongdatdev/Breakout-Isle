using System;
using System.Collections.Generic;

namespace BreakoutIsle.Gameplay
{
    public sealed class ObjectiveProgress
    {
        private readonly LevelObjectiveSet _definition;
        private readonly QuestInventory _inventory;
        private readonly List<string> _completed = new List<string>();

        public IReadOnlyList<string> CompletedIds => _completed;
        public event Action<ObjectiveStepDefinition> Completed;
        public event Action<string> ObjectiveReset;

        public ObjectiveProgress(LevelObjectiveSet definition, QuestInventory inventory)
        {
            _definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            ValidateDefinition();
        }

        public ObjectiveStepDefinition Current
        {
            get
            {
                if (_definition.steps == null)
                    return null;
                foreach (var step in _definition.steps)
                    if (!_completed.Contains(step.id))
                        return step;
                return null;
            }
        }

        public bool IsCompleted(string objectiveId) => _completed.Contains(objectiveId);

        public bool TryComplete(string objectiveId)
        {
            var step = Find(objectiveId);
            if (step == null || _completed.Contains(step.id))
                return false;
            if (string.IsNullOrEmpty(step.sequenceGroupId) && !step.canCompleteOutOfOrder && Current != step)
                return false;
            if (!string.IsNullOrEmpty(step.sequenceGroupId) && !ReferenceEquals(step, NextSequenceStep(step.sequenceGroupId)))
            {
                ResetSequence(step.sequenceGroupId);
                return false;
            }
            if (!HasPrerequisites(step) ||
                (!string.IsNullOrEmpty(step.requiredQuestItemId) && !_inventory.HasQuestItem(step.requiredQuestItemId)))
                return false;

            if (step.consumeRequiredItem && !_inventory.RemoveQuestItem(step.requiredQuestItemId))
                return false;
            _completed.Add(step.id);
            if (!string.IsNullOrEmpty(step.rewardQuestItemId))
                _inventory.AddQuestItem(step.rewardQuestItemId);
            Completed?.Invoke(step);
            return true;
        }

        public bool TryRestoreCompleted(IReadOnlyList<string> objectiveIds)
        {
            if (!CanRestoreCompleted(objectiveIds)) return false;
            _completed.Clear();
            for (var i = 0; i < objectiveIds.Count; i++)
                _completed.Add(objectiveIds[i]);
            return true;
        }

        public bool CanRestoreCompleted(IReadOnlyList<string> objectiveIds)
        {
            if (objectiveIds == null) return false;
            var candidate = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in objectiveIds)
                if (Find(id) == null || !candidate.Add(id)) return false;
            foreach (var step in _definition.steps)
                if (candidate.Contains(step.id) && !HasPrerequisites(step, candidate)) return false;
            foreach (var group in SequenceGroups())
            {
                var missingSeen = false;
                foreach (var step in group)
                {
                    if (!candidate.Contains(step.id)) missingSeen = true;
                    else if (missingSeen) return false;
                }
            }
            if (objectiveIds.Count > 0 && !AllPriorOrderedStepsComplete(candidate)) return false;
            return true;
        }

        private bool AllPriorOrderedStepsComplete(HashSet<string> candidate)
        {
            var foundIncomplete = false;
            foreach (var step in _definition.steps)
            {
                if (step.canCompleteOutOfOrder || !string.IsNullOrEmpty(step.sequenceGroupId)) continue;
                if (!candidate.Contains(step.id)) foundIncomplete = true;
                else if (foundIncomplete) return false;
            }
            return true;
        }

        private bool HasPrerequisites(ObjectiveStepDefinition step, HashSet<string> completed = null)
        {
            bool Contains(string id) => string.IsNullOrEmpty(id) || (completed != null ? completed.Contains(id) : _completed.Contains(id));
            if (!Contains(step.prerequisiteId)) return false;
            if (step.requiredCompletedObjectiveIds == null) return true;
            foreach (var id in step.requiredCompletedObjectiveIds)
                if (!Contains(id)) return false;
            return true;
        }

        private ObjectiveStepDefinition NextSequenceStep(string groupId)
        {
            ObjectiveStepDefinition next = null;
            foreach (var step in _definition.steps)
                if (step.sequenceGroupId == groupId && !_completed.Contains(step.id) &&
                    (next == null || step.sequenceIndex < next.sequenceIndex)) next = step;
            return next;
        }

        private void ResetSequence(string groupId)
        {
            var resetIds = new List<string>();
            _completed.RemoveAll(id =>
            {
                var step = Find(id);
                var reset = step != null && step.sequenceGroupId == groupId;
                if (reset) resetIds.Add(id);
                return reset;
            });
            foreach (var id in resetIds) ObjectiveReset?.Invoke(id);
        }

        private IEnumerable<List<ObjectiveStepDefinition>> SequenceGroups()
        {
            var groups = new Dictionary<string, List<ObjectiveStepDefinition>>(StringComparer.Ordinal);
            foreach (var step in _definition.steps)
            {
                if (string.IsNullOrEmpty(step.sequenceGroupId)) continue;
                if (!groups.TryGetValue(step.sequenceGroupId, out var group))
                    groups.Add(step.sequenceGroupId, group = new List<ObjectiveStepDefinition>());
                group.Add(step);
            }
            foreach (var group in groups.Values)
                group.Sort((a, b) => a.sequenceIndex.CompareTo(b.sequenceIndex));
            return groups.Values;
        }

        private ObjectiveStepDefinition Find(string objectiveId)
        {
            if (_definition.steps == null)
                return null;
            foreach (var step in _definition.steps)
                if (step.id == objectiveId)
                    return step;
            return null;
        }

        private void ValidateDefinition()
        {
            if (_definition.steps == null)
                throw new ArgumentException("Objective definitions are missing.", nameof(_definition));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sequenceIndices = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (var step in _definition.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id))
                    throw new ArgumentException("Objective steps must have nonempty unique IDs.", nameof(_definition));
                if (step.consumeRequiredItem && string.IsNullOrWhiteSpace(step.requiredQuestItemId))
                    throw new ArgumentException("A consuming objective must require an item.", nameof(_definition));
                if (!string.IsNullOrEmpty(step.rewardQuestItemId) && string.IsNullOrWhiteSpace(step.rewardQuestItemId))
                    throw new ArgumentException("A reward item ID cannot be whitespace.", nameof(_definition));
                if (!string.IsNullOrEmpty(step.sequenceGroupId))
                {
                    if (string.IsNullOrWhiteSpace(step.sequenceGroupId))
                        throw new ArgumentException("Sequence group IDs cannot be whitespace.", nameof(_definition));
                    if (step.sequenceIndex < 0) throw new ArgumentException("Sequence indices cannot be negative.", nameof(_definition));
                    if (!sequenceIndices.TryGetValue(step.sequenceGroupId, out var indices))
                        sequenceIndices.Add(step.sequenceGroupId, indices = new HashSet<int>());
                    if (!indices.Add(step.sequenceIndex)) throw new ArgumentException("Sequence indices must be unique within a group.", nameof(_definition));
                }
            }
            foreach (var step in _definition.steps)
            {
                if (!string.IsNullOrEmpty(step.prerequisiteId) && (string.IsNullOrWhiteSpace(step.prerequisiteId) || !ids.Contains(step.prerequisiteId)))
                    throw new ArgumentException("Objective prerequisite references an unknown objective.", nameof(_definition));
                if (step.requiredCompletedObjectiveIds != null)
                    foreach (var requiredId in step.requiredCompletedObjectiveIds)
                        if (!ids.Contains(requiredId)) throw new ArgumentException("Objective requirement references an unknown objective.", nameof(_definition));
            }
        }
    }
}
