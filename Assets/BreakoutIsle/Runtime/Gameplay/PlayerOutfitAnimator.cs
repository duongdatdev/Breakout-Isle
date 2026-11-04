using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerOutfitAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private RuntimeAnimatorController prisonController;
        [SerializeField] private AnimatorOverrideController civilianController;
        [SerializeField] private LevelSession session;

        public string CurrentOutfit { get; private set; }

        public void Configure(Animator targetAnimator, RuntimeAnimatorController baseController,
            AnimatorOverrideController civilianOverride)
        {
            animator = targetAnimator;
            prisonController = baseController;
            civilianController = civilianOverride;
            if (animator != null && prisonController != null)
                animator.runtimeAnimatorController = prisonController;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (session == null) session = FindFirstObjectByType<LevelSession>();
            Subscribe();
            if (session != null) ApplyOutfit(session.OutfitId);
        }

        private void OnDisable() => Unsubscribe();

        public void Bind(LevelSession levelSession)
        {
            Unsubscribe();
            session = levelSession;
            Subscribe();
            if (session != null) ApplyOutfit(session.OutfitId);
        }

        public bool ApplyOutfit(string outfitId)
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || string.IsNullOrWhiteSpace(outfitId)) return false;

            RuntimeAnimatorController selectedController;
            if (string.Equals(outfitId, "prison", StringComparison.Ordinal))
                selectedController = prisonController;
            else if (IsCivilianOutfit(outfitId))
                selectedController = civilianController;
            else
            {
                Debug.LogError($"Unsupported Rook outfit '{outfitId}'. No animation controller was selected.", this);
                return false;
            }

            if (selectedController == null)
            {
                Debug.LogError($"The animation controller for outfit '{outfitId}' is not configured.", this);
                return false;
            }

            animator.runtimeAnimatorController = selectedController;
            CurrentOutfit = IsCivilianOutfit(outfitId) ? "civilian" : "prison";
            return true;
        }

        public static bool IsCivilianOutfit(string outfitId) =>
            string.Equals(outfitId, "civilian", StringComparison.Ordinal) ||
            string.Equals(outfitId, "civilian_clothes", StringComparison.Ordinal);

        public static bool IsPrisonOnlyAction(string action) =>
            string.Equals(action, "pry", StringComparison.Ordinal) ||
            string.Equals(action, "crawl", StringComparison.Ordinal) ||
            string.Equals(action, "climb", StringComparison.Ordinal);

        private void Subscribe()
        {
            if (isActiveAndEnabled && session != null)
                session.OutfitChanged += OnOutfitChanged;
        }

        private void Unsubscribe()
        {
            if (session != null) session.OutfitChanged -= OnOutfitChanged;
        }

        private void OnOutfitChanged(string outfitId) => ApplyOutfit(outfitId);
    }
}
