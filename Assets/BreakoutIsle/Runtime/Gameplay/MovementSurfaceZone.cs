using UnityEngine;

namespace BreakoutIsle.Gameplay
{
public sealed class MovementSurfaceZone : MonoBehaviour
    {
        private static int _nextZoneId;
        [SerializeField] private Collider2D zoneCollider;
        [SerializeField] private bool water;
        [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.68f;
        private int _zoneId;

        private void OnEnable()
        {
            if (_zoneId == 0)
                _zoneId = ++_nextZoneId;
        }

        private void Awake()
        {
            if (zoneCollider == null) zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider != null) zoneCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
        private void OnTriggerStay2D(Collider2D other)
        {
            RegisterPlayer(other);
            if (water) ClearWaterScent();
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            var modifiers = other.GetComponentInParent<PlayerMovementModifiers>();
            if (modifiers != null) modifiers.RemoveSurface(_zoneId);
        }

        public bool Contains(Vector3 position) => zoneCollider != null && zoneCollider.OverlapPoint(position);
        public void Register(PlayerMovementModifiers modifiers)
        {
            if (modifiers != null) modifiers.SetSurface(_zoneId, speedMultiplier, water);
        }

        private void RegisterPlayer(Collider2D other)
        {
            var modifiers = other.GetComponentInParent<PlayerMovementModifiers>();
            if (modifiers == null) return;
            Register(modifiers);
            if (water) ClearWaterScent();
        }

        private void ClearWaterScent()
        {
            if (zoneCollider != null) ScentTrailSystem.ClearBounds(zoneCollider.bounds);
        }
    }
}
