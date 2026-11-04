using System;
using BreakoutIsle.Audio;
using BreakoutIsle.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject restartConfirmPanel;
        [SerializeField] private Button restartConfirmButton;
        [SerializeField] private Button restartCancelButton;
        [SerializeField] private Text saveStatusText;
        [SerializeField] private CheckpointSaveCoordinator checkpointCoordinator;
        [SerializeField] private string levelSceneName = "Level01_Prison";
        [SerializeField] private string initialCheckpointId = "cell-start";
        [SerializeField] private LevelSceneRoute[] levelRoutes =
        {
            new LevelSceneRoute { levelId = "prison-01", sceneName = "Level01_Prison" }
        };

        [Serializable]
        private struct LevelSceneRoute
        {
            public string levelId;
            public string sceneName;
        }

        private void OnEnable()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(HideSettings);
            if (restartConfirmButton != null) restartConfirmButton.onClick.AddListener(ConfirmRestart);
            if (restartCancelButton != null) restartCancelButton.onClick.AddListener(CancelRestart);
        }

        private void Start()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (restartConfirmPanel != null) restartConfirmPanel.SetActive(false);
            if (newGameButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
            if (checkpointCoordinator == null) checkpointCoordinator = FindObjectOfType<CheckpointSaveCoordinator>();
            var availability = checkpointCoordinator != null ? checkpointCoordinator.CheckContinueAvailability() : SaveLoadStatus.Missing;
            if (continueButton != null) continueButton.interactable = availability == SaveLoadStatus.LoadedPrimary || availability == SaveLoadStatus.RecoveredBackup;
            if (saveStatusText != null)
                saveStatusText.text = availability == SaveLoadStatus.NeedsRestartConfirmation
                    ? "Tệp lưu cần được xác nhận trước khi bắt đầu lại."
                    : availability == SaveLoadStatus.Missing ? "Chưa có tiến trình để tiếp tục." : string.Empty;
            if (AudioService.Instance != null)
            {
                AudioService.Instance.PlayLoop("ambience.prison", true);
                AudioService.Instance.CrossfadeMusic("music.stealth");
            }
        }

        private void OnDisable()
        {
            if (newGameButton != null) newGameButton.onClick.RemoveListener(NewGame);
            if (continueButton != null) continueButton.onClick.RemoveListener(ContinueGame);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
            if (quitButton != null) quitButton.onClick.RemoveListener(Quit);
            if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(HideSettings);
            if (restartConfirmButton != null) restartConfirmButton.onClick.RemoveListener(ConfirmRestart);
            if (restartCancelButton != null) restartCancelButton.onClick.RemoveListener(CancelRestart);
        }

        public void Configure(Button newGame, Button continueGame, Button settings, Button quit,
            Button settingsBack, GameObject settingsView, GameObject confirmView, Button confirm, Button cancel,
            string targetScene, string checkpointId, CheckpointSaveCoordinator coordinator, Text saveStatus)
        {
            newGameButton = newGame;
            continueButton = continueGame;
            settingsButton = settings;
            quitButton = quit;
            settingsBackButton = settingsBack;
            settingsPanel = settingsView;
            restartConfirmPanel = confirmView;
            restartConfirmButton = confirm;
            restartCancelButton = cancel;
            checkpointCoordinator = coordinator;
            saveStatusText = saveStatus;
            levelSceneName = targetScene;
            initialCheckpointId = checkpointId;
        }

        private void NewGame()
        {
            Play("ui.confirm");
            if (checkpointCoordinator != null && checkpointCoordinator.CheckContinueAvailability() == SaveLoadStatus.NeedsRestartConfirmation)
            {
                if (restartConfirmPanel != null) restartConfirmPanel.SetActive(true);
                return;
            }
            LoadLevel(false, false);
        }

        private void ContinueGame()
        {
            if (checkpointCoordinator == null || !checkpointCoordinator.CanContinue)
            {
                if (saveStatusText != null) saveStatusText.text = "Không có tệp lưu hợp lệ để tiếp tục.";
                Play("ui.error");
                return;
            }
            if (!checkpointCoordinator.TryGetContinueLevelId(out var levelId) || !TryResolveScene(levelId, out var savedScene))
            {
                if (saveStatusText != null) saveStatusText.text = "Bản lưu thuộc khu vực chưa được cài đặt.";
                Play("ui.error");
                return;
            }
            Play("ui.confirm");
            LoadLevel(true, false, savedScene);
        }

        private bool TryResolveScene(string levelId, out string sceneName)
        {
            if (levelRoutes != null)
                foreach (var route in levelRoutes)
                    if (string.Equals(route.levelId, levelId, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(route.sceneName))
                    {
                        sceneName = route.sceneName;
                        return true;
                    }
            sceneName = null;
            return false;
        }

        private void LoadLevel(bool continueGame, bool confirmedRestart, string scene = null)
        {
            var transition = new GameObject("Breakout Scene Launch").AddComponent<SceneLaunchRequest>();
            DontDestroyOnLoad(transition.gameObject);
            transition.Configure(continueGame, confirmedRestart, string.IsNullOrWhiteSpace(scene) ? levelSceneName : scene, initialCheckpointId);
        }

        private void ShowSettings() { Play("ui.panel.open"); if (settingsPanel != null) settingsPanel.SetActive(true); }
        private void HideSettings() { Play("ui.cancel"); if (settingsPanel != null) settingsPanel.SetActive(false); }
        private void Quit() { Play("ui.confirm"); Application.Quit(); }

        private void ConfirmRestart()
        {
            Play("ui.confirm");
            if (restartConfirmPanel != null) restartConfirmPanel.SetActive(false);
            LoadLevel(false, true);
        }

        private void CancelRestart()
        {
            Play("ui.cancel");
            if (restartConfirmPanel != null) restartConfirmPanel.SetActive(false);
            if (saveStatusText != null) saveStatusText.text = "Tiến trình lỗi vẫn được giữ nguyên.";
        }
        private static void Play(string eventId) { if (AudioService.Instance != null) AudioService.Instance.Play(eventId); }
    }
}
