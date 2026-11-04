using System;
using System.Collections.Generic;

namespace BreakoutIsle.Gameplay
{
    [Serializable]
    public sealed class CampaignCarryoverData
    {
        public string targetLevelId;
        public string outfitId;
        public int selectedSupportSlot;
        public string[] supportItemIds;
        public List<string> questItemIds;
        public List<StoryFlagRecord> storyFlags;

        public bool TryGetValidationError(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(targetLevelId) || string.IsNullOrWhiteSpace(outfitId))
                error = "Campaign carryover destination or outfit is missing.";
            else if (selectedSupportSlot < 0 || selectedSupportSlot > 2)
                error = "Campaign carryover selected support slot is invalid.";
            else if (supportItemIds == null || supportItemIds.Length != 3)
                error = "Campaign carryover must contain exactly three support slots.";
            else if (questItemIds == null || storyFlags == null)
                error = "Campaign carryover collections are missing.";
            else if (!UniqueNonempty(questItemIds))
                error = "Campaign carryover quest items must be nonempty and unique.";
            else if (!UniqueNonempty(storyFlags, flag => flag != null ? flag.id : null))
                error = "Campaign carryover story flags must have unique IDs.";
            return error != null;
        }

        private static bool UniqueNonempty(IEnumerable<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) return false;
            return true;
        }

        private static bool UniqueNonempty<T>(IEnumerable<T> values, Func<T, string> getId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var id = getId(value);
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return false;
            }
            return true;
        }
    }
}
