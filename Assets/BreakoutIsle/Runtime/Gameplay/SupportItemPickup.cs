using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class SupportItemPickup : MonoBehaviour, IPlayerInteractable, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private string itemId;
        [SerializeField] private string prompt = "Pick up";
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Collider2D pickupCollider;
        private bool _collected;

        public string StableId => persistentId;
        public string PersistentId => persistentId;
        public string Prompt => prompt;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (pickupCollider == null) pickupCollider = GetComponent<Collider2D>();
        }

        public bool CanInteract(PlayerInteractor player) => !_collected && player != null && player.Session != null;

        public void Interact(PlayerInteractor player)
        {
            if (player == null || player.Session == null) return;
            if (!player.Session.TryAddSupportItem(itemId, out _)) return;
            SetCollected(true);
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = _collected ? "collected" : "available"
        };

        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = "available"
        };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId ||
                (record.state != "collected" && record.state != "available"))
                throw new ArgumentException("Support pickup state does not match this stable ID.", nameof(record));
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
