using BreakoutIsle.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Toggle muteToggle;
        private bool _updating;

        private void OnEnable()
        {
            if (masterVolume != null) masterVolume.onValueChanged.AddListener(OnVolumeChanged);
            if (muteToggle != null) muteToggle.onValueChanged.AddListener(OnMuteChanged);
            SyncFromService();
        }

        private void OnDisable()
        {
            if (masterVolume != null) masterVolume.onValueChanged.RemoveListener(OnVolumeChanged);
            if (muteToggle != null) muteToggle.onValueChanged.RemoveListener(OnMuteChanged);
        }

        public void Bind(Slider volume, Toggle mute)
        {
            masterVolume = volume;
            muteToggle = mute;
            if (isActiveAndEnabled) SyncFromService();
        }

        private void SyncFromService()
        {
            var audio = AudioService.Instance;
            if (audio == null) return;
            _updating = true;
            if (masterVolume != null) masterVolume.SetValueWithoutNotify(audio.MasterVolume);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(audio.Muted);
            _updating = false;
        }

        private void OnVolumeChanged(float value)
        {
            if (!_updating && AudioService.Instance != null) AudioService.Instance.SetMasterVolume(value);
        }

        private void OnMuteChanged(bool value)
        {
            if (!_updating && AudioService.Instance != null) AudioService.Instance.SetMuted(value);
        }
    }
}
