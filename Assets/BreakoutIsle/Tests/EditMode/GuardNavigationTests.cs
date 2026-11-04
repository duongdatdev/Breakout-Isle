using System.Collections.Generic;
using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public sealed class GuardNavigationTests
    {
        [Test]
        public void DetectionTimeMatchesNearAndFarGddEndpoints()
        {
            Assert.That(GuardDetectionMath.SecondsToDetect(1f, 6f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(GuardDetectionMath.SecondsToDetect(6f, 6f), Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void FindPathRoutesAroundSolidBlocker()
        {
            var wall = new GameObject("Path obstacle");
            var results = new List<Vector2>();
            try
            {
                wall.layer = LayerMask.NameToLayer("Vision");
                wall.transform.position = Vector2.right;
                wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1.8f);
                Physics2D.SyncTransforms();

                Assert.That(GuardPathfinding.FindPath(Vector2.zero, new Vector2(2f, 0f), 1 << wall.layer, results), Is.True);
                Assert.That(results, Is.Not.Empty);
                Assert.That(results.Exists(point => Mathf.Abs(point.y) > 1f), Is.True, "Path should route above or below the obstacle.");
                var prior = Vector2.zero;
                foreach (var point in results)
                {
                    Assert.That(Physics2D.Linecast(prior, point, 1 << wall.layer).collider, Is.Null);
                    prior = point;
                }
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }
    }
}
