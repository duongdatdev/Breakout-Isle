using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [Serializable]
    public sealed class ObjectiveStepDefinition
    {
        public string id;
        [TextArea] public string description;
        public string prerequisiteId;
        public string[] requiredCompletedObjectiveIds;
        public bool canCompleteOutOfOrder;
        public string sequenceGroupId;
        public int sequenceIndex;
        public string requiredQuestItemId;
        public bool consumeRequiredItem;
        public string rewardQuestItemId;
    }
}
