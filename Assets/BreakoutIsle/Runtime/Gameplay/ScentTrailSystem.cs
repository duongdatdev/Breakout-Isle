using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    /// <summary>Draft bounded scent memory. Runtime callers supply actual world positions and time.</summary>
    public static class ScentTrailSystem
    {
        public struct Breadcrumb
        {
            public long id;
            public Vector2 position;
            public float createdAt;
        }

        public const float LifetimeSeconds = 12f;
        public const float SmellRadius = 5f;
        public const int MaximumBreadcrumbs = 192;
        private const float MinimumSpacing = 0.4f;
        private const float MinimumInterval = 0.3f;
        private static readonly List<Breadcrumb> Trail = new List<Breadcrumb>(MaximumBreadcrumbs);
        private static long _nextId = 1;

        public static int Count => Trail.Count;

        /// <returns>New trail ID, or 0 when the player's actual displacement did not justify a sample.</returns>
        public static long RecordMovement(Vector2 position, float now)
        {
            Expire(now);
            if (Trail.Count > 0)
            {
                var latest = Trail[Trail.Count - 1];
                if ((position - latest.position).sqrMagnitude < MinimumSpacing * MinimumSpacing &&
                    now - latest.createdAt < MinimumInterval)
                    return 0;
            }

            var breadcrumb = new Breadcrumb
            {
                id = _nextId++,
                position = position,
                createdAt = now
            };
            Trail.Add(breadcrumb);
            if (Trail.Count > MaximumBreadcrumbs)
                Trail.RemoveAt(0);
            return breadcrumb.id;
        }

        /// <summary>Returns the oldest still-valid later breadcrumb reachable by smell.</summary>
        public static bool TryGetNext(long afterId, Vector2 currentPosition, float smellRadius, float now,
            out Breadcrumb breadcrumb)
        {
            Expire(now);
            var radiusSquared = Mathf.Max(0f, smellRadius) * Mathf.Max(0f, smellRadius);
            for (var i = 0; i < Trail.Count; i++)
            {
                var candidate = Trail[i];
                if (candidate.id <= afterId || (candidate.position - currentPosition).sqrMagnitude > radiusSquared)
                    continue;
                breadcrumb = candidate;
                return true;
            }
            breadcrumb = default;
            return false;
        }

        public static void ClearBounds(Bounds area)
        {
            for (var i = Trail.Count - 1; i >= 0; i--)
                if (area.Contains(Trail[i].position)) Trail.RemoveAt(i);
        }

        public static int CountAt(float now)
        {
            Expire(now);
            return Trail.Count;
        }

        public static void Reset()
        {
            Trail.Clear();
            _nextId = 1;
        }

        private static void Expire(float now)
        {
            var firstValid = 0;
            while (firstValid < Trail.Count && now - Trail[firstValid].createdAt > LifetimeSeconds)
                firstValid++;
            if (firstValid > 0) Trail.RemoveRange(0, firstValid);
        }
    }
}
