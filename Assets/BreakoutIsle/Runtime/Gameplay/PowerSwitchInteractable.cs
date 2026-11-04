using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class PowerSwitchInteractable : MonoBehaviour, IPlayerInteractable, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private string objectiveId;
        [SerializeField] private string prompt = "Gạt công tắc";
        [SerializeField] private LevelSession session;
        [SerializeField] private MonoBehaviour[] linkedConsumers = Array.Empty<MonoBehaviour>();
        [SerializeField] private bool baselinePowered = true;

        private readonly List<IPowerConsumer> _consumers = new List<IPowerConsumer>();
        private bool _targetsValid;
        private bool _powered;

        public string StableId => persistentId;
        public string PersistentId => persistentId;
        public string Prompt => prompt;
        public bool IsPowered => _powered;

        private void Awake()
        {
            if (session == null) session = FindObjectOfType<LevelSession>();
            _targetsValid = TryResolveConsumers();
            if (!_targetsValid)
            {
                Debug.LogError("PowerSwitchInteractable needs valid, unique IPowerConsumer references.", this);
                return;
            }
            _powered = baselinePowered;
            ApplyTargets(_powered);
        }

        private void OnEnable()
        {
            if (session != null) session.ObjectiveReset += OnObjectiveReset;
        }

        private void OnDisable()
        {
            if (session != null) session.ObjectiveReset -= OnObjectiveReset;
        }

        public bool CanInteract(PlayerInteractor player)
        {
            if (!_targetsValid || player == null || player.Session == null) return false;
            // Objective switches are one-way: after their required power-off action, don't re-enable them.
            return string.IsNullOrWhiteSpace(objectiveId) || _powered;
        }

        public void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;
            var nextPowered = !_powered;
            try
            {
                ApplyTargets(nextPowered);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                player.Session.ShowMessage("Công tắc bị kẹt, hãy thử lại.");
                return;
            }

            if (!nextPowered && !string.IsNullOrWhiteSpace(objectiveId) &&
                (player.Session.Progress == null || !player.Session.Progress.TryComplete(objectiveId)))
            {
                ApplyTargets(_powered);
                player.Session.ShowMessage("Chưa đủ điều kiện thao tác công tắc.");
                return;
            }

            _powered = nextPowered;
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = _powered ? "on" : "off"
        };

        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = baselinePowered ? "on" : "off"
        };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId ||
                (record.state != "on" && record.state != "off"))
                throw new ArgumentException("Power switch state does not match this stable ID.", nameof(record));
            var powered = record.state == "on";
            ApplyTargets(powered);
            _powered = powered;
        }

        private bool TryResolveConsumers()
        {
            _consumers.Clear();
            if (linkedConsumers == null || linkedConsumers.Length == 0) return false;
            var seen = new HashSet<MonoBehaviour>();
            foreach (var behaviour in linkedConsumers)
            {
                if (behaviour == null || !seen.Add(behaviour) || !(behaviour is IPowerConsumer consumer))
                    return false;
                _consumers.Add(consumer);
            }
            return true;
        }

        private void ApplyTargets(bool powered)
        {
            var previous = new bool[_consumers.Count];
            for (var i = 0; i < _consumers.Count; i++) previous[i] = _consumers[i].IsPowered;
            try
            {
                foreach (var consumer in _consumers) consumer.SetPowered(powered);
            }
            catch
            {
                for (var i = _consumers.Count - 1; i >= 0; i--) _consumers[i].SetPowered(previous[i]);
                throw;
            }
        }

        private void OnObjectiveReset(string id)
        {
            if (id != objectiveId) return;
            ApplyTargets(baselinePowered);
            _powered = baselinePowered;
        }
    }
}
