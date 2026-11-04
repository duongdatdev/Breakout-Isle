using System;
using BreakoutIsle.Audio;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
public sealed class BearTrapHazard : MonoBehaviour, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private SpriteRenderer hiddenTrapSprite;
        [SerializeField] private Collider2D trapTrigger;
        [SerializeField] private bool startsRevealed;
        [SerializeField, Min(0f)] private float rootSeconds = 3f;
        [SerializeField, Min(0f)] private float noiseRadius = 10f;
        private bool _triggered;
        private bool _revealed;

        public string PersistentId => persistentId;

        private void Awake() => SetRevealed(startsRevealed);

        public void Reveal() => SetRevealed(true);

        public void Activate(Collider2D other)
        {
            if (_triggered || !_revealed) return;
            var player = other.GetComponentInParent<PlayerMovementModifiers>();
            if (player == null) return;
            _triggered = true;
            if (trapTrigger != null) trapTrigger.enabled = false;
            player.ApplyRoot(rootSeconds);
            NoiseSystem.Emit(transform.position, noiseRadius);
            if (AudioService.Instance != null) AudioService.Instance.PlayAt("hazard.bear_trap.snap", transform.position);
        }

        public void SetRevealed(bool revealed)
        {
            _revealed = revealed;
            if (hiddenTrapSprite != null) hiddenTrapSprite.enabled = revealed;
            if (trapTrigger != null) trapTrigger.enabled = revealed && !_triggered;
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = _triggered ? "triggered" : _revealed ? "revealed" : "hidden"
        };

        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = startsRevealed ? "revealed" : "hidden"
        };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId ||
                (record.state != "hidden" && record.state != "revealed" && record.state != "triggered"))
                throw new ArgumentException("Bear trap state does not match this stable ID.", nameof(record));
            _triggered = record.state == "triggered";
            SetRevealed(record.state != "hidden");
        }
    }
}
