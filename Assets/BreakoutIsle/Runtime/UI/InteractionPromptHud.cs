using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.Gameplay
{
    public sealed class InteractionPromptHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject promptPanel;
        private bool _subscribed;
        private void OnEnable() { Subscribe(); }
        private void Start() => OnPromptChanged(interactor != null && interactor.Current != null ? interactor.Current.Prompt : string.Empty);
        private void OnDisable() => Unsubscribe();

        public void Bind(PlayerInteractor source, Text label, GameObject panel)
        {
            Unsubscribe();
            interactor = source;
            promptText = label;
            promptPanel = panel;
            Subscribe();
            OnPromptChanged(interactor != null && interactor.Current != null ? interactor.Current.Prompt : string.Empty);
        }

        private void Subscribe()
        {
            if (_subscribed || interactor == null) return;
            interactor.PromptChanged += OnPromptChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || interactor == null) return;
            interactor.PromptChanged -= OnPromptChanged;
            _subscribed = false;
        }
        private void OnPromptChanged(string value)
        {
            var visible = !string.IsNullOrWhiteSpace(value);
            if (promptText != null) promptText.text = visible ? "E   " + Translate(value) : string.Empty;
            if (promptPanel != null) promptPanel.SetActive(visible);
        }

        private static string Translate(string value)
        {
            switch (value)
            {
                case "Inspect pillow": return "Kiểm tra gối";
                case "Take keycard": return "Lấy thẻ từ";
                case "Use exit": return "Mở lối thoát";
                case "Pry open the vent": return "Nạy lỗ thông gió";
                case "Pick up": return "Nhặt vật phẩm";
                case "Interact": return "Tương tác";
                default: return value;
            }
        }
    }
}
