using System.Text;
using BreakoutIsle.Audio;
using BreakoutIsle.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    public sealed class GameHudController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerStealthState stealth;
        [SerializeField] private LevelSession session;
        [SerializeField] private CheckpointSaveCoordinator checkpointCoordinator;
        [SerializeField] private Text objectivesText;
        [SerializeField] private Text inventoryText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text alertText;
        [SerializeField] private GameObject objectivesPanel;
        [SerializeField] private GameObject mapCloseButtonObject;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject capturedPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject restartConfirmationPanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button restartConfirmButton;
        [SerializeField] private Button restartCancelButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Image[] supportSlots;
        [SerializeField] private Text[] supportLabels;
        [SerializeField] private Sprite[] supportIcons;
        [SerializeField] private Sprite selectedSlotSprite;
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private AudioSettingsPanel audioSettings;
        [SerializeField] private string menuSceneName = "Bootstrap";

        private bool _overlayOpen;
        private float _statusRemaining;

        public void Bind(PlayerInputReader playerInput, PlayerMotor playerMotor, PlayerInteractor playerInteractor,
            PlayerStealthState playerStealth, LevelSession levelSession, CheckpointSaveCoordinator checkpoints)
        {
            input = playerInput;
            motor = playerMotor;
            interactor = playerInteractor;
            stealth = playerStealth;
            session = levelSession;
            checkpointCoordinator = checkpoints;
            _retryAction = checkpoints != null ? new System.Func<bool>(checkpoints.RetryFromCheckpoint) : null;
            RefreshObjectives();
            RefreshInventory();
        }

        public void SetRetryHandler(System.Func<bool> retryHandler) => _retryAction = retryHandler;

        public void ConfigureView(Text objectives, Text inventory, Text status, Text alert, GameObject objectivesView,
            GameObject pauseView, GameObject settingsView, GameObject capturedView, GameObject victoryView,
            Button retry, Button resume, Button showSettings, Button backFromSettings, Button menuButton,
            Image[] slots, Text[] slotText, Sprite[] icons, Sprite selectedSprite, Sprite emptySprite,
            AudioSettingsPanel settingsPanelController, GameObject restartConfirmation = null,
            Button confirmRestart = null, Button cancelRestart = null, Button closeObjectives = null,
            Button capturedMenu = null, Button victoryMenu = null)
        {
            objectivesText = objectives;
            inventoryText = inventory;
            statusText = status;
            alertText = alert;
            objectivesPanel = objectivesView;
            pausePanel = pauseView;
            settingsPanel = settingsView;
            capturedPanel = capturedView;
            victoryPanel = victoryView;
            restartConfirmationPanel = restartConfirmation;
            retryButton = retry;
            restartConfirmButton = confirmRestart;
            restartCancelButton = cancelRestart;
            resumeButton = resume;
            settingsButton = showSettings;
            settingsBackButton = backFromSettings;
            returnToMenuButton = menuButton;
            supportSlots = slots;
            supportLabels = slotText;
            supportIcons = icons;
            selectedSlotSprite = selectedSprite;
            emptySlotSprite = emptySprite;
            audioSettings = settingsPanelController;
            mapCloseButtonObject = closeObjectives != null ? closeObjectives.gameObject : null;
            capturedMenuButton = capturedMenu;
            victoryMenuButton = victoryMenu;
            Bind(input, motor, interactor, stealth, session, checkpointCoordinator);
        }

        [SerializeField] private Button capturedMenuButton;
        [SerializeField] private Button victoryMenuButton;
        private System.Func<bool> _retryAction;

        private void OnEnable()
        {
            if (_retryAction == null && checkpointCoordinator != null)
                _retryAction = checkpointCoordinator.RetryFromCheckpoint;
            if (input != null)
            {
                input.PausePressed += TogglePause;
                input.ObjectivesPressed += ToggleObjectives;
                input.SlotSelected += SelectSlot;
            }
            if (interactor != null) interactor.PromptChanged += OnPromptChanged;
            if (session != null)
            {
                session.ObjectiveCompleted += OnObjectiveChanged;
                session.ObjectiveReset += OnObjectiveChanged;
                session.QuestCompleted += OnQuestCompleted;
                session.MessageShown += ShowStatus;
            }
            if (stealth != null) stealth.Captured += OnCaptured;
            if (checkpointCoordinator != null)
            {
                checkpointCoordinator.SavePendingChanged += OnSavePending;
                checkpointCoordinator.CheckpointSaved += OnCheckpointSaved;
                checkpointCoordinator.CheckpointRestored += OnCheckpointRestored;
                checkpointCoordinator.LoadStatusChanged += OnLoadStatus;
                checkpointCoordinator.RestartConfirmationRequired += OnRestartConfirmation;
                checkpointCoordinator.SaveFailed += ShowStatus;
            }
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(HideSettings);
            if (returnToMenuButton != null) returnToMenuButton.onClick.AddListener(ReturnToMenu);
            if (restartConfirmButton != null) restartConfirmButton.onClick.AddListener(ConfirmRestart);
            if (restartCancelButton != null) restartCancelButton.onClick.AddListener(CancelRestart);
            if (mapCloseButtonObject != null)
            {
                var button = mapCloseButtonObject.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(CloseObjectives);
            }
            if (capturedMenuButton != null) capturedMenuButton.onClick.AddListener(ReturnToMenu);
            if (victoryMenuButton != null) victoryMenuButton.onClick.AddListener(ReturnToMenu);
        }

        private void Start()
        {
            SetActive(objectivesPanel, false);
            SetActive(pausePanel, false);
            SetActive(settingsPanel, false);
            SetActive(capturedPanel, false);
            SetActive(victoryPanel, false);
            SetActive(restartConfirmationPanel, false);
            RefreshObjectives();
            RefreshInventory();
            OnPromptChanged(interactor != null && interactor.Current != null ? interactor.Current.Prompt : string.Empty);
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PausePressed -= TogglePause;
                input.ObjectivesPressed -= ToggleObjectives;
                input.SlotSelected -= SelectSlot;
            }
            if (interactor != null) interactor.PromptChanged -= OnPromptChanged;
            if (session != null)
            {
                session.ObjectiveCompleted -= OnObjectiveChanged;
                session.ObjectiveReset -= OnObjectiveChanged;
                session.QuestCompleted -= OnQuestCompleted;
                session.MessageShown -= ShowStatus;
            }
            if (stealth != null) stealth.Captured -= OnCaptured;
            if (checkpointCoordinator != null)
            {
                checkpointCoordinator.SavePendingChanged -= OnSavePending;
                checkpointCoordinator.CheckpointSaved -= OnCheckpointSaved;
                checkpointCoordinator.CheckpointRestored -= OnCheckpointRestored;
                checkpointCoordinator.LoadStatusChanged -= OnLoadStatus;
                checkpointCoordinator.RestartConfirmationRequired -= OnRestartConfirmation;
                checkpointCoordinator.SaveFailed -= ShowStatus;
            }
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(HideSettings);
            if (returnToMenuButton != null) returnToMenuButton.onClick.RemoveListener(ReturnToMenu);
            if (restartConfirmButton != null) restartConfirmButton.onClick.RemoveListener(ConfirmRestart);
            if (restartCancelButton != null) restartCancelButton.onClick.RemoveListener(CancelRestart);
            if (mapCloseButtonObject != null)
            {
                var button = mapCloseButtonObject.GetComponent<Button>();
                if (button != null) button.onClick.RemoveListener(CloseObjectives);
            }
            if (capturedMenuButton != null) capturedMenuButton.onClick.RemoveListener(ReturnToMenu);
            if (victoryMenuButton != null) victoryMenuButton.onClick.RemoveListener(ReturnToMenu);
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_statusRemaining > 0f)
            {
                _statusRemaining -= Time.unscaledDeltaTime;
                if (_statusRemaining <= 0f && statusText != null) statusText.text = string.Empty;
            }
            if (alertText != null && motor != null)
                alertText.text = stealth != null && stealth.IsHidden ? "ĐANG ẨN" : motor.IsSneaking ? "ĐI NHẸ" : string.Empty;
        }

        public void TogglePause()
        {
            if (capturedPanel != null && capturedPanel.activeSelf || victoryPanel != null && victoryPanel.activeSelf) return;
            if (settingsPanel != null && settingsPanel.activeSelf) { HideSettings(); return; }
            if (objectivesPanel != null && objectivesPanel.activeSelf) SetActive(objectivesPanel, false);
            _overlayOpen = pausePanel != null && !pausePanel.activeSelf;
            SetActive(pausePanel, _overlayOpen);
            Time.timeScale = _overlayOpen ? 0f : 1f;
            SetGameplayBlocked(_overlayOpen);
            if (AudioService.Instance != null) AudioService.Instance.Play(_overlayOpen ? "ui.panel.open" : "ui.cancel");
            SelectFirst(pausePanel);
        }

        public void ToggleObjectives()
        {
            if (pausePanel != null && pausePanel.activeSelf || capturedPanel != null && capturedPanel.activeSelf || victoryPanel != null && victoryPanel.activeSelf) return;
            var show = objectivesPanel != null && !objectivesPanel.activeSelf;
            SetActive(objectivesPanel, show);
            Time.timeScale = show ? 0f : 1f;
            SetGameplayBlocked(show);
            if (show) RefreshObjectives();
            if (AudioService.Instance != null) AudioService.Instance.Play(show ? "ui.panel.open" : "ui.cancel");
        }

        public void Resume()
        {
            SetActive(settingsPanel, false);
            SetActive(pausePanel, false);
            _overlayOpen = false;
            Time.timeScale = 1f;
            SetGameplayBlocked(false);
        }

        private void ShowSettings()
        {
            SetActive(settingsPanel, true);
            SetActive(pausePanel, false);
            SetGameplayBlocked(true);
            SelectFirst(settingsPanel);
        }

        private void HideSettings()
        {
            SetActive(settingsPanel, false);
            SetActive(pausePanel, true);
            SetGameplayBlocked(true);
            SelectFirst(pausePanel);
        }

        private void Retry()
        {
            if (_retryAction == null || !_retryAction())
            {
                ShowStatus("Không thể khôi phục điểm lưu. Hãy thử lại sau.");
                return;
            }

            // A successful restore normally raises CheckpointRestored synchronously. Keep
            // this path explicit as well so a successful retry cannot strand the HUD modal.
            CompleteCheckpointRestore();
        }

        private void ReturnToMenu()
        {
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(menuSceneName)) SceneManager.LoadScene(menuSceneName);
        }

        private void OnCaptured()
        {
            Time.timeScale = 0f;
            SetGameplayBlocked(true);
            SetActive(pausePanel, false);
            SetActive(objectivesPanel, false);
            SetActive(capturedPanel, true);
            ShowStatus("Bạn đã bị phát hiện. Hãy thử lại từ điểm lưu gần nhất.");
            if (AudioService.Instance != null) AudioService.Instance.Play("alert.guard");
            SelectFirst(capturedPanel);
        }

        private void OnQuestCompleted()
        {
            Time.timeScale = 0f;
            SetGameplayBlocked(true);
            SetActive(pausePanel, false);
            SetActive(objectivesPanel, false);
            SetActive(victoryPanel, true);
            if (AudioService.Instance != null) AudioService.Instance.Play("ui.confirm");
            SelectFirst(victoryPanel);
        }

        private void OnObjectiveChanged(string _)
        {
            RefreshObjectives();
            RefreshInventory();
        }
        private void OnPromptChanged(string value) { }

        private void RefreshObjectives()
        {
            if (session == null || session.Objectives == null || objectivesText == null) return;
            var builder = new StringBuilder("NHIỆM VỤ\n");
            var steps = session.Objectives.steps;
            if (steps == null) return;
            foreach (var step in steps)
            {
                var complete = session.Progress != null && session.Progress.IsCompleted(step.id);
                builder.Append(complete ? "✓  " : "□  ").Append(VietnameseObjective(step)).Append('\n');
            }
            objectivesText.text = builder.ToString().TrimEnd();
        }

        private static string VietnameseObjective(ObjectiveStepDefinition step)
        {
            switch (step.id)
            {
                case "find-spoon": return "Tìm chiếc thìa dưới gối";
                case "open-vent": return "Mở lỗ thông gió";
                case "find-keycard": return "Lấy thẻ từ bàn bảo vệ";
                case "escape": return "Dùng thẻ mở lối thoát";
                default: return string.IsNullOrWhiteSpace(step.description) ? step.id : step.description;
            }
        }

        private void RefreshInventory()
        {
            if (session == null || session.Inventory == null) return;
            var supports = session.Inventory.SupportItems;
            for (var i = 0; i < 3; i++)
            {
                var selected = session.SelectedSupportSlot == i;
                if (supportSlots != null && i < supportSlots.Length && supportSlots[i] != null)
                {
                    supportSlots[i].sprite = selected ? selectedSlotSprite : emptySlotSprite;
                    var id = i < supports.Count ? supports[i] : null;
                    var icon = FindSupportIcon(id);
                    var iconImage = supportSlots[i].transform.childCount > 0 ? supportSlots[i].transform.GetChild(0).GetComponent<Image>() : null;
                    if (iconImage != null)
                    {
                        iconImage.sprite = icon;
                        iconImage.enabled = icon != null;
                    }
                }
                if (supportLabels != null && i < supportLabels.Length && supportLabels[i] != null)
                    supportLabels[i].text = (i + 1) + (selected ? " •" : string.Empty);
            }
            if (inventoryText != null)
            {
                var text = new StringBuilder("VẬT PHẨM\n");
                foreach (var item in session.Inventory.QuestItems) text.Append("•  ").Append(VietnameseItem(item)).Append('\n');
                for (var i = 0; i < supports.Count; i++)
                    if (!string.IsNullOrEmpty(supports[i])) text.Append((i + 1)).Append("  ").Append(VietnameseItem(supports[i])).Append('\n');
                inventoryText.text = text.ToString().TrimEnd();
            }
        }

        private Sprite FindSupportIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || supportIcons == null) return null;
            foreach (var icon in supportIcons)
                if (icon != null && icon.name.Equals(itemId.Replace('-', '_'), System.StringComparison.OrdinalIgnoreCase)) return icon;
            return null;
        }

        private static string VietnameseItem(string item)
        {
            switch (item)
            {
                case "spoon": return "Thìa";
                case "keycard": return "Thẻ từ";
                case "brass_key": return "Chìa đồng";
                case "wire_cutters": return "Kìm cắt";
                case "shovel": return "Xẻng";
                case "map_fragment": return "Mảnh bản đồ";
                case "cipher_note": return "Mật thư";
                case "civilian_clothes": return "Quần áo thường";
                case "stone": return "Đá";
                case "tin_can": return "Lon thiếc";
                case "glass_bottle": return "Chai thủy tinh";
                case "firecracker": return "Pháo";
                case "meat": return "Thịt";
                case "energy_drink": return "Nước tăng lực";
                case "boat_key": return "Chìa khóa thuyền";
                case "port_pass": return "Thẻ cảng";
                default: return item.Replace('_', ' ');
            }
        }

        private void SelectSlot(int value)
        {
            if (session == null) return;
            session.SetSelectedSupportSlot(value - 1);
            RefreshInventory();
        }

        private void ShowStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = TranslateFeedback(message);
            _statusRemaining = 3.5f;
        }

        private static string TranslateFeedback(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return string.Empty;
            if (message == "Requirements are not met.") return "Chưa đủ vật phẩm hoặc điều kiện.";
            if (message == "No saved game exists.") return "Chưa có tiến trình để tiếp tục.";
            if (message.StartsWith("Save failed:", System.StringComparison.Ordinal) || message.StartsWith("Cannot save:", System.StringComparison.Ordinal))
                return "Không thể lưu tại thời điểm này.";
            if (message.StartsWith("Continue failed:", System.StringComparison.Ordinal) || message.StartsWith("Saved quest state", System.StringComparison.Ordinal))
                return "Không thể khôi phục tiến trình.";
            if (message.StartsWith("Cannot start:", System.StringComparison.Ordinal)) return "Không thể bắt đầu tại điểm an toàn này.";
            return message;
        }

        private void OnSavePending(bool pending, string checkpoint)
        {
            ShowStatus(pending ? "Lưu đang chờ khi hết truy đuổi…" : "Đã lưu tiến trình.");
            if (pending && AudioService.Instance != null) AudioService.Instance.Play("alert.heartbeat", 0.7f);
        }

        private void OnCheckpointSaved(string checkpoint)
        {
            ShowStatus("Đã lưu tại điểm an toàn.");
            RefreshInventory();
            if (AudioService.Instance != null) AudioService.Instance.Play("ui.checkpoint.saved");
        }

        private void OnCheckpointRestored(string checkpoint)
        {
            CompleteCheckpointRestore();
        }

        private void CompleteCheckpointRestore()
        {
            SetActive(capturedPanel, false);
            SetActive(victoryPanel, false);
            SetActive(restartConfirmationPanel, false);
            SetActive(pausePanel, false);
            SetActive(objectivesPanel, false);
            Time.timeScale = 1f;
            SetGameplayBlocked(false);
            RefreshObjectives();
            RefreshInventory();
            ShowStatus("Đã khôi phục điểm lưu.");
        }
        private void OnLoadStatus(SaveLoadStatus status)
        {
            if (status == SaveLoadStatus.NeedsRestartConfirmation) ShowStatus("Tệp lưu bị lỗi. Xác nhận để bắt đầu lại.");
        }
        private void OnRestartConfirmation()
        {
            ShowStatus("Tệp lưu không dùng được. Vui lòng xác nhận bắt đầu lại.");
            Time.timeScale = 0f;
            SetGameplayBlocked(true);
            SetActive(restartConfirmationPanel, true);
            SelectFirst(restartConfirmationPanel);
        }

        public void SelectSupportSlotFromUi(int slot)
        {
            if (session == null || !session.SetSelectedSupportSlot(slot)) return;
            RefreshInventory();
        }

        private void ConfirmRestart()
        {
            if (checkpointCoordinator == null) return;
            if (!checkpointCoordinator.ConfirmRestartAndStartNewGame("cell-start")) return;
            SetActive(restartConfirmationPanel, false);
            Time.timeScale = 1f;
            SetGameplayBlocked(false);
        }

        private void CancelRestart()
        {
            ReturnToMenu();
        }

        public void CloseObjectives()
        {
            SetActive(objectivesPanel, false);
            Time.timeScale = 1f;
            SetGameplayBlocked(false);
        }

        private void SetGameplayBlocked(bool value)
        {
            if (input != null) input.SetGameplayBlocked(value);
        }

        private static void SetActive(GameObject target, bool value) { if (target != null) target.SetActive(value); }

        private static void SelectFirst(GameObject panel)
        {
            if (panel == null) return;
            var selectable = panel.GetComponentInChildren<Selectable>(true);
            if (selectable != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
