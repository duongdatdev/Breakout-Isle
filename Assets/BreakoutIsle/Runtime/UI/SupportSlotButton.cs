using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    public sealed class SupportSlotButton : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private GameHudController hud;
        private Button _button;
        private void Awake() => _button = GetComponent<Button>();
        private void OnEnable() { if (_button != null) _button.onClick.AddListener(Select); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Select); }
        public void Configure(GameHudController controller, int index) { hud = controller; slotIndex = index; }
        private void Select() { if (hud != null) hud.SelectSupportSlotFromUi(slotIndex); }
    }
}
