using System;
using System.Collections.Generic;

namespace BreakoutIsle.Gameplay
{
    public sealed class QuestInventory
    {
        private readonly List<string> _questItems = new List<string>();
        private readonly string[] _supportItems = new string[3];

        public IReadOnlyList<string> QuestItems => _questItems;
        public IReadOnlyList<string> SupportItems => _supportItems;
        public bool HasQuestItem(string itemId) => !string.IsNullOrWhiteSpace(itemId) && _questItems.Contains(itemId);

        public bool AddQuestItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || _questItems.Contains(itemId))
                return false;
            _questItems.Add(itemId);
            return true;
        }

        public bool RemoveQuestItem(string itemId) => _questItems.Remove(itemId);

        public bool TryAddSupportItem(string itemId, out int slot)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                slot = -1;
                return false;
            }

            for (var i = 0; i < _supportItems.Length; i++)
            {
                if (!string.IsNullOrEmpty(_supportItems[i]))
                    continue;
                _supportItems[i] = itemId;
                slot = i;
                return true;
            }

            slot = -1;
            return false;
        }

        public string[] CopySupportItems() => (string[])_supportItems.Clone();
        public List<string> CopyQuestItems() => new List<string>(_questItems);

        // Validate the entire replacement before changing the live inventory.
        public bool TryRestore(IEnumerable<string> questItems, IReadOnlyList<string> supportItems)
        {
            if (!TryBuildRestoredItems(questItems, supportItems, out var newQuestItems, out var newSupportItems)) return false;

            _questItems.Clear();
            _questItems.AddRange(newQuestItems);
            Array.Copy(newSupportItems, _supportItems, _supportItems.Length);
            return true;
        }

        public bool CanRestore(IEnumerable<string> questItems, IReadOnlyList<string> supportItems) =>
            TryBuildRestoredItems(questItems, supportItems, out _, out _);

        private bool TryBuildRestoredItems(IEnumerable<string> questItems, IReadOnlyList<string> supportItems,
            out List<string> newQuestItems, out string[] newSupportItems)
        {
            newQuestItems = new List<string>();
            newSupportItems = new string[_supportItems.Length];
            if (questItems == null || supportItems == null || supportItems.Count != _supportItems.Length) return false;
            var seenQuest = new HashSet<string>(StringComparer.Ordinal);
            foreach (var itemId in questItems)
            {
                if (string.IsNullOrWhiteSpace(itemId) || !seenQuest.Add(itemId)) return false;
                newQuestItems.Add(itemId);
            }
            for (var i = 0; i < newSupportItems.Length; i++)
            {
                var itemId = supportItems[i];
                if (!string.IsNullOrWhiteSpace(itemId)) newSupportItems[i] = itemId;
            }
            return true;
        }
    }
}
