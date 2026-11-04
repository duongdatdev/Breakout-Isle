using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class SecurityCamera : MonoBehaviour, IPowerConsumer
    {
        [SerializeField] private PlayerStealthState player;
        [SerializeField] private Vector2 localForward = Vector2.up;
        [SerializeField, Range(0f, 180f)] private float fieldOfViewDegrees = 60f;
        [SerializeField, Min(0.1f)] private float detectionRange = 6f;
        [SerializeField, Range(0f, 180f)] private float sweepArcDegrees = 60f;
        [SerializeField, Min(0.1f)] private float sweepPeriodSeconds = 4f;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Min(0f)] private float guardReportRadius = 15f;
        [SerializeField, Min(0f)] private float reportCooldownSeconds = 2f;

        private Quaternion _baselineRotation;
        private float _sweepEpoch;
        private float _nextReportTime;
        private bool _initialized;

        public bool IsPowered { get; private set; }
        public float DetectionRange => detectionRange;
        public float FieldOfViewDegrees => fieldOfViewDegrees;
        public Vector2 LocalForward => localForward.sqrMagnitude > 0f ? localForward.normalized : Vector2.up;
        public Vector2 ForwardDirection => transform.TransformDirection(LocalForward);

        private void Awake()
        {
            _baselineRotation = transform.localRotation;
            _sweepEpoch = Time.time;
            _initialized = true;
            if (player == null) player = FindObjectOfType<PlayerStealthState>();
            if (!IsPowered) transform.localRotation = _baselineRotation;
        }

        private void Update()
        {
            if (!IsPowered) return;
            var halfArc = sweepArcDegrees * 0.5f;
            var phase = Mathf.Sin((Time.time - _sweepEpoch) * Mathf.PI * 2f / sweepPeriodSeconds);
            transform.localRotation = _baselineRotation * Quaternion.Euler(0f, 0f, phase * halfArc);

            if (Time.time < _nextReportTime || !CanObserve(player)) return;
            // Direct guard-system report: do not emit an audio/noise event for a camera sighting.
            if (GuardBrain.InvestigateNearestNoise(player.transform.position, guardReportRadius))
                _nextReportTime = Time.time + reportCooldownSeconds;
        }

        public void SetPowered(bool powered)
        {
            IsPowered = powered;
            if (!powered && _initialized) transform.localRotation = _baselineRotation;
        }

        public bool CanObserve(PlayerStealthState candidate)
        {
            if (!IsPowered || candidate == null || candidate.IsHidden || candidate.IsCaptured) return false;
            var forward = ForwardDirection;
            var target = (Vector2)candidate.transform.position;
            return IsWithinView(transform.position, forward, target, detectionRange, fieldOfViewDegrees) &&
                   HasClearLineOfSight(transform.position, target, obstacleMask, transform, candidate.transform);
        }

        public static bool IsWithinView(Vector2 origin, Vector2 forward, Vector2 target, float range, float fieldOfViewDegrees)
        {
            var offset = target - origin;
            if (forward.sqrMagnitude < 0.0001f || offset.sqrMagnitude < 0.0001f || offset.magnitude > range) return false;
            return Vector2.Angle(forward, offset) <= Mathf.Clamp(fieldOfViewDegrees, 0f, 180f) * 0.5f;
        }

        public static bool HasClearLineOfSight(Vector2 origin, Vector2 target, LayerMask obstacleMask,
            Transform observer, Transform candidate)
        {
            var offset = target - origin;
            var distance = offset.magnitude;
            if (distance <= 0.001f || obstacleMask.value == 0) return true;
            foreach (var hit in Physics2D.RaycastAll(origin, offset / distance, distance, obstacleMask))
            {
                if (hit.collider == null || hit.collider.isTrigger ||
                    IsSelfOrChild(hit.collider.transform, observer) || IsSelfOrChild(hit.collider.transform, candidate)) continue;
                return false;
            }
            return true;
        }

        private static bool IsSelfOrChild(Transform value, Transform root) =>
            root != null && (value == root || value.IsChildOf(root));
    }
}
