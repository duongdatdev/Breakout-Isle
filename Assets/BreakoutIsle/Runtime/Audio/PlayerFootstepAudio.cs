using BreakoutIsle.Gameplay;
using UnityEngine;

namespace BreakoutIsle.Audio
{
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private string surfaceEvent = "player.step.prison";
        [SerializeField, Min(0.05f)] private float walkInterval = 0.42f;
        [SerializeField, Min(0.05f)] private float sneakInterval = 0.58f;
        [SerializeField, Min(0.05f)] private float runInterval = 0.28f;
        private PlayerMotor _motor;
        private Vector3 _lastPosition;
        private float _stepTimer;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            var distance = Vector2.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;
            if (distance <= 0.001f || AudioService.Instance == null)
            {
                _stepTimer = 0f;
                return;
            }
            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;
            AudioService.Instance.Play(surfaceEvent, _motor.IsSneaking ? 0.45f : 0.8f);
            var speed = distance / Mathf.Max(Time.deltaTime, 0.0001f);
            _stepTimer = _motor.IsSneaking ? sneakInterval : speed > 4.5f ? runInterval : walkInterval;
        }

        public void SetSurface(string eventId)
        {
            if (!string.IsNullOrWhiteSpace(eventId)) surfaceEvent = eventId;
        }
    }
}
