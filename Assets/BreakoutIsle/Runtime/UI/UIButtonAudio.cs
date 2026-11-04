using BreakoutIsle.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonAudio : MonoBehaviour
    {
        [SerializeField] private string eventId = "ui.click";
        private Button _button;
        private void Awake() => _button = GetComponent<Button>();
        private void OnEnable() { if (_button != null) _button.onClick.AddListener(Play); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Play); }
        private void Play() { if (AudioService.Instance != null) AudioService.Instance.Play(eventId); }
    }
}
