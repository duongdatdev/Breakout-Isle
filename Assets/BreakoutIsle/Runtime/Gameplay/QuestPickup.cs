using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class QuestPickup : MonoBehaviour, IPlayerInteractable, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private string itemId;
        [SerializeField] private string objectiveId;
        [SerializeField] private string prompt = "Pick up";
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Collider2D pickupCollider;
        private bool _collected;

        public string StableId => persistentId;
        public string Prompt => prompt;
        public string PersistentId => persistentId;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (pickupCollider == null) pickupCollider = GetComponent<Collider2D>();
        }

        public bool CanInteract(PlayerInteractor player) => !_collected && player != null && player.Session != null;

        public void Interact(PlayerInteractor player)
        {
            var inventory = player.Session.Inventory;
            if (inventory == null || !inventory.AddQuestItem(itemId)) return;
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                SetCollected(true);
                player.Session.NotifyInventoryChanged();
                return;
            }
            if (!player.Session.CompleteObjective(objectiveId))
            {
                inventory.RemoveQuestItem(itemId);
                return;
            }
            SetCollected(true);
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord { stableId = persistentId, state = _collected ? "collected" : "available" };
        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord { stableId = persistentId, state = "available" };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId || (record.state != "collected" && record.state != "available"))
                throw new ArgumentException("Pickup world state does not match this stable ID.", nameof(record));
            SetCollected(record.state == "collected");
        }

        private void SetCollected(bool collected)
        {
            _collected = collected;
            if (spriteRenderer != null) spriteRenderer.enabled = !collected;
            if (pickupCollider != null) pickupCollider.enabled = !collected;
        }
    }
}
