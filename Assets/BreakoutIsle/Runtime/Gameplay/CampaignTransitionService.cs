using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreakoutIsle.Gameplay
{
    public sealed class CampaignTransitionService : MonoBehaviour
    {
        [SerializeField] private LevelSession session;
        [SerializeField] private CheckpointSaveCoordinator saveCoordinator;
        [SerializeField] private CampaignLevelDefinition currentLevel;

        private bool _exitReady;
        private bool _transitionRequested;

        public bool ExitReady => _exitReady || (session != null && session.Progress != null && session.Progress.Current == null);
        public event Action<bool> ExitReadyChanged;
        public event Action<string> TransitionFailed;
        public event Action<string> LevelAdvanced;
        public event Action CampaignCompleted;

        private void Awake()
        {
            if (session == null) session = FindObjectOfType<LevelSession>();
            if (saveCoordinator == null) saveCoordinator = FindObjectOfType<CheckpointSaveCoordinator>();
        }

        private void OnEnable()
        {
            if (session != null) session.QuestCompleted += OnQuestCompleted;
        }

        private void OnDisable()
        {
            if (session != null) session.QuestCompleted -= OnQuestCompleted;
        }

        private void Start()
        {
            if (session == null || saveCoordinator == null || currentLevel == null)
                return;
            if (!CampaignTransitionBuffer.TryConsume(currentLevel.levelId, out var carryover))
                return;
            if (!saveCoordinator.StartCampaignLevel(currentLevel.initialCheckpointId, carryover))
            {
                TransitionFailed?.Invoke("Could not initialize the destination level. The previous profile remains available.");
                return;
            }
            LevelAdvanced?.Invoke(currentLevel.levelId);
        }

        public bool TryAdvance()
        {
            if (_transitionRequested || !ExitReady)
                return false;
            if (session == null || session.Objectives == null || currentLevel == null ||
                session.Objectives.levelId != currentLevel.levelId)
                return Fail("Campaign level definition does not match the loaded session.");

            if (currentLevel.nextLevel == null)
            {
                if (!currentLevel.isFinalLevel)
                    return Fail("This campaign level has no next level configured.");
                _transitionRequested = true;
                CampaignCompleted?.Invoke();
                return true;
            }

            var destination = currentLevel.nextLevel;
            if (string.IsNullOrWhiteSpace(destination.levelId) || string.IsNullOrWhiteSpace(destination.sceneName) ||
                string.IsNullOrWhiteSpace(destination.initialCheckpointId))
                return Fail("Next campaign level is missing its scene or authored checkpoint.");
            if (!Application.CanStreamedLevelBeLoaded(destination.sceneName))
                return Fail("Next campaign scene is not included in the build: " + destination.sceneName);

            var carryover = CaptureCarryover(destination.levelId);
            if (carryover.TryGetValidationError(out var error))
                return Fail("Campaign carryover is invalid: " + error);

            _transitionRequested = true;
            CampaignTransitionBuffer.Set(destination.sceneName, carryover);
            try
            {
                SceneManager.LoadScene(destination.sceneName, LoadSceneMode.Single);
                return true;
            }
            catch (Exception exception)
            {
                CampaignTransitionBuffer.Clear();
                _transitionRequested = false;
                return Fail("Could not load the next campaign scene: " + exception.Message);
            }
        }

        private CampaignCarryoverData CaptureCarryover(string targetLevelId)
        {
            var questItemIds = new List<string>();
            if (currentLevel.carryQuestItemIdsToNextLevel != null && session.Inventory != null)
                foreach (var itemId in currentLevel.carryQuestItemIdsToNextLevel)
                    if (!string.IsNullOrWhiteSpace(itemId) && session.Inventory.HasQuestItem(itemId) && !questItemIds.Contains(itemId))
                        questItemIds.Add(itemId);

            var flags = new List<StoryFlagRecord>();
            if (session.PersistentStoryFlags != null)
                foreach (var flag in session.PersistentStoryFlags)
                    if (flag != null) flags.Add(new StoryFlagRecord { id = flag.id, value = flag.value });

            return new CampaignCarryoverData
            {
                targetLevelId = targetLevelId,
                outfitId = session.OutfitId,
                selectedSupportSlot = session.SelectedSupportSlot,
                supportItemIds = session.Inventory != null ? session.Inventory.CopySupportItems() : null,
                questItemIds = questItemIds,
                storyFlags = flags
            };
        }

        private void OnQuestCompleted()
        {
            _exitReady = true;
            ExitReadyChanged?.Invoke(true);
        }

        private bool Fail(string message)
        {
            TransitionFailed?.Invoke(message);
            if (session != null) session.ShowMessage(message);
            return false;
        }
    }

    internal static class CampaignTransitionBuffer
    {
        private static string _expectedSceneName;
        private static CampaignCarryoverData _pending;

        static CampaignTransitionBuffer()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void Set(string expectedSceneName, CampaignCarryoverData carryover)
        {
            _expectedSceneName = expectedSceneName;
            _pending = carryover;
        }

        public static bool TryConsume(string destinationLevelId, out CampaignCarryoverData carryover)
        {
            carryover = null;
            if (_pending == null || _pending.targetLevelId != destinationLevelId)
                return false;
            carryover = _pending;
            _pending = null;
            _expectedSceneName = null;
            return true;
        }

        public static void Clear()
        {
            _pending = null;
            _expectedSceneName = null;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pending != null && !string.Equals(scene.name, _expectedSceneName, StringComparison.Ordinal))
                Clear();
        }
    }
}
