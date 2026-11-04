using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class QuestActionInteractable : MonoBehaviour, IPlayerInteractable, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private string objectiveId;
        [SerializeField] private string prompt = "Interact";
        [SerializeField] private Animator animator;
        [SerializeField] private string animationTrigger = "Open";
        [SerializeField] private string completedAnimatorState;
        [SerializeField] private LevelSession session;
        [SerializeField] private Collider2D interactionCollider;
        private bool _completed;

        public string StableId => persistentId;
        public string PersistentId => persistentId;
        public string Prompt => prompt;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (interactionCollider == null) interactionCollider = GetComponent<Collider2D>();
            if (session == null) session = FindObjectOfType<LevelSession>();
        }

        private void OnEnable() { if (session != null) session.ObjectiveReset += OnObjectiveReset; }
        private void OnDisable() { if (session != null) session.ObjectiveReset -= OnObjectiveReset; }

        public bool CanInteract(PlayerInteractor player) => !_completed && player != null && player.Session != null;

        public void Interact(PlayerInteractor player)
        {
            if (!player.Session.CompleteObjective(objectiveId)) return;
            SetCompleted(true, true);
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord { stableId = persistentId, state = _completed ? "completed" : "available" };
        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord { stableId = persistentId, state = "available" };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId || (record.state != "completed" && record.state != "available"))
                throw new ArgumentException("Interactable world state does not match this stable ID.", nameof(record));
            SetCompleted(record.state == "completed", false);
        }

        private void SetCompleted(bool completed, bool playTransition)
        {
            _completed = completed;
            if (interactionCollider != null) interactionCollider.enabled = !completed;
            if (animator == null) return;
            if (completed && playTransition && !string.IsNullOrEmpty(animationTrigger))
                animator.SetTrigger(animationTrigger);
            else if (completed && !string.IsNullOrWhiteSpace(completedAnimatorState))
            {
                animator.Play(completedAnimatorState, 0, 1f);
                animator.Update(0f);
            }
            else if (!completed)
                animator.Rebind();
        }

        private void OnObjectiveReset(string id)
        {
            if (id == objectiveId)
                SetCompleted(false, false);
        }
    }
}
