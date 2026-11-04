using System;
using BreakoutIsle.Audio;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
public sealed class TripwireHazard : MonoBehaviour, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private Collider2D trigger;
        [SerializeField, Min(0f)] private float alarmRadius = 15f;
        private bool _triggered;

        public string PersistentId => persistentId;

        private void Awake()
        {
            if (trigger == null) trigger = GetComponent<Collider2D>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                trigger.enabled = !_triggered;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (!CanTrigger(_triggered, motor != null, motor != null && motor.IsSneaking)) return;
            SetTriggered(true);
            GuardBrain.TriggerAlarm(transform.position, alarmRadius);
            if (AudioService.Instance != null) AudioService.Instance.PlayAt("alert.guard", transform.position);
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = _triggered ? "triggered" : "available"
        };

        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord { stableId = persistentId, state = "available" };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId ||
                (record.state != "available" && record.state != "triggered"))
                throw new ArgumentException("Tripwire state does not match this stable ID.", nameof(record));
            SetTriggered(record.state == "triggered");
        }

        public static bool CanTrigger(bool alreadyTriggered, bool playerHasMotor, bool playerIsSneaking) =>
            !alreadyTriggered && playerHasMotor && !playerIsSneaking;

        private void SetTriggered(bool triggered)
        {
            _triggered = triggered;
            if (trigger != null) trigger.enabled = !triggered;
        }
    }
}
