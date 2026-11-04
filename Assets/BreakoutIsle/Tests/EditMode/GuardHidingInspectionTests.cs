using System.Reflection;
using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public sealed class GuardHidingInspectionTests
    {
        [Test]
        public void InspectionIgnoresTargetLockerButHonorsOtherBlockers()
        {
            var guardObject = new GameObject("Inspection guard");
            var locker = new GameObject("Target locker");
            var blocker = new GameObject("Other sight blocker");
            try
            {
                var visionLayer = LayerMask.NameToLayer("Vision");
                var hidingLayer = LayerMask.NameToLayer("Hiding");
                Assert.That(visionLayer, Is.GreaterThanOrEqualTo(0));
                Assert.That(hidingLayer, Is.GreaterThanOrEqualTo(0));

                var body = guardObject.AddComponent<Rigidbody2D>();
                body.position = Vector2.zero;
                var brain = guardObject.AddComponent<GuardBrain>();
                typeof(GuardBrain).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(brain, null);
                typeof(GuardBrain).GetField("lineOfSightObstacles", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(brain, (LayerMask)((1 << visionLayer) | (1 << hidingLayer)));

                locker.layer = hidingLayer;
                locker.transform.position = new Vector2(0.5f, 0f);
                locker.AddComponent<BoxCollider2D>().size = Vector2.one;
                var hidingSpot = locker.AddComponent<HidingSpot>();
                Physics2D.SyncTransforms();

                var inspect = typeof(GuardBrain).GetMethod("CanInspect", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((bool)inspect.Invoke(brain, new object[] { hidingSpot }), Is.True,
                    "The guard must be able to reach and inspect the locker it is searching.");

                blocker.layer = visionLayer;
                blocker.transform.position = new Vector2(0.25f, 0f);
                blocker.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 1f);
                Physics2D.SyncTransforms();

                Assert.That((bool)inspect.Invoke(brain, new object[] { hidingSpot }), Is.False,
                    "A separate wall or prop must still block inspection.");
            }
            finally
            {
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(locker);
                Object.DestroyImmediate(guardObject);
            }
        }

        [Test]
        public void InteractionRayIgnoresSiblingTargetCollidersButHonorsOtherBlockers()
        {
            var player = new GameObject("Interactor");
            var target = new GameObject("Multi-collider hiding spot");
            var blocker = new GameObject("Unrelated blocker");
            try
            {
                player.transform.position = new Vector2(0.5f, 1f);
                player.AddComponent<CircleCollider2D>().radius = 0.2f;
                var interactor = player.AddComponent<PlayerInteractor>();

                var hidingLayer = LayerMask.NameToLayer("Hiding");
                var visionLayer = LayerMask.NameToLayer("Vision");
                target.layer = hidingLayer;
                target.transform.position = new Vector2(0.5f, 0f);
                target.AddComponent<BoxCollider2D>().size = new Vector2(1f, 0.8f);
                var trigger = target.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = Vector2.one;
                target.AddComponent<HidingSpot>();
                Physics2D.SyncTransforms();

                var isOccluded = typeof(PlayerInteractor).GetMethod("IsOccluded", BindingFlags.Instance | BindingFlags.NonPublic);
                var ownColliders = player.GetComponentsInChildren<Collider2D>();
                Assert.That((bool)isOccluded.Invoke(interactor, new object[] { (Vector2)target.transform.position, trigger, target.transform, ownColliders }), Is.False,
                    "The target's solid and trigger colliders must not hide the target from interaction.");

                blocker.layer = visionLayer;
                blocker.transform.position = new Vector2(0.5f, 0.45f);
                blocker.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 0.2f);
                Physics2D.SyncTransforms();
                Assert.That((bool)isOccluded.Invoke(interactor, new object[] { (Vector2)target.transform.position, trigger, target.transform, ownColliders }), Is.True,
                    "A separate blocker must still prevent interaction through walls.");
            }
            finally
            {
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(player);
            }
        }
    }
}
