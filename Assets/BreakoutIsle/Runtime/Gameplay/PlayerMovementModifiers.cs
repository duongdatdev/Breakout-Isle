using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
public sealed class PlayerMovementModifiers : MonoBehaviour
    {
        private struct Surface
        {
            public float speedMultiplier;
            public bool water;
        }

        private readonly Dictionary<int, Surface> _surfaces = new Dictionary<int, Surface>();
        private float _rootedUntil;

        public bool IsInWater
        {
            get
            {
                foreach (var surface in _surfaces.Values)
                    if (surface.water) return true;
                return false;
            }
        }

        public float SpeedMultiplier
        {
            get
            {
                if (Time.time < _rootedUntil) return 0f;
                var multiplier = 1f;
                foreach (var surface in _surfaces.Values)
                    multiplier = Mathf.Min(multiplier, surface.speedMultiplier);
                return multiplier;
            }
        }

        public void SetSurface(int zoneId, float speedMultiplier, bool water) =>
            _surfaces[zoneId] = new Surface { speedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f), water = water };

        public void RemoveSurface(int zoneId) => _surfaces.Remove(zoneId);
        public void ApplyRoot(float durationSeconds) => _rootedUntil = Mathf.Max(_rootedUntil, Time.time + Mathf.Max(0f, durationSeconds));

        public void ResetTransientState()
        {
            _surfaces.Clear();
            _rootedUntil = 0f;
            RefreshActiveSurfaces();
        }

        private void RefreshActiveSurfaces()
        {
            var zones = FindObjectsByType<MovementSurfaceZone>(FindObjectsSortMode.None);
            foreach (var zone in zones)
                if (zone != null && zone.Contains(transform.position))
                    zone.Register(this);
        }
    }
}
