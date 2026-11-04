using BreakoutIsle.Gameplay;
using UnityEngine;

namespace BreakoutIsle.Audio
{
    [RequireComponent(typeof(GuardBrain))]
    public sealed class GuardAudioEvents : MonoBehaviour
    {
        private GuardBrain _brain;
        private void Awake() => _brain = GetComponent<GuardBrain>();
        private void OnEnable() { if (_brain != null) _brain.StateChanged += OnStateChanged; }
        private void OnDisable() { if (_brain != null) _brain.StateChanged -= OnStateChanged; }

        private void OnStateChanged(GuardState state)
        {
            var audio = AudioService.Instance;
            if (audio == null) return;
            if (state == GuardState.Chase)
            {
                audio.PlayAt("alert.guard", transform.position);
                audio.SetChaseActive(true);
                audio.Play("alert.heartbeat", 0.8f);
            }
            else if (state == GuardState.Suspicious)
                audio.PlayAt("alert.guard", transform.position, 0.45f);
            else if (state == GuardState.Search || state == GuardState.Patrol || state == GuardState.Alert)
                audio.SetChaseActive(AnyGuardChasing());
        }

        private static bool AnyGuardChasing()
        {
            foreach (var guard in FindObjectsOfType<GuardBrain>())
                if (guard != null && guard.State == GuardState.Chase) return true;
            return false;
        }
    }
}
