using System.Collections.Generic;
using System.Reflection;
using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BreakoutIsle.FeatureDrafts.ForestGameplay
{
    public sealed class ForestGameplayTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp() => ScentTrailSystem.Reset();

        [TearDown]
        public void TearDown()
        {
            ScentTrailSystem.Reset();
            MeatDistractionSource.ResetAllTransientSources();
            PoliceDogBrain.ResetAllTransientState();
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
            foreach (var asset in _assets)
                if (asset != null) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void ScentBreadcrumbsExpireAfterTheirBoundedLifetime()
        {
            var id = ScentTrailSystem.RecordMovement(Vector2.zero, now: 2f);
            Assert.That(id, Is.GreaterThan(0));
            Assert.That(ScentTrailSystem.CountAt(2f + ScentTrailSystem.LifetimeSeconds - 0.01f), Is.EqualTo(1));
            Assert.That(ScentTrailSystem.CountAt(2f + ScentTrailSystem.LifetimeSeconds + 0.01f), Is.Zero);
            Assert.That(ScentTrailSystem.TryGetNext(0, Vector2.zero, ScentTrailSystem.SmellRadius,
                2f + ScentTrailSystem.LifetimeSeconds + 0.01f, out _), Is.False);
        }

        [Test]
        public void ScentTrailStorageHasAHardCapacityLimit()
        {
            for (var i = 0; i < ScentTrailSystem.MaximumBreadcrumbs * 2; i++)
                ScentTrailSystem.RecordMovement(new Vector2(i, 0f), i * 0.01f);

            Assert.That(ScentTrailSystem.Count, Is.EqualTo(ScentTrailSystem.MaximumBreadcrumbs));
        }

        [Test]
        public void WaterBoundsRemoveOnlyBreadcrumbsInsideTheWater()
        {
            ScentTrailSystem.RecordMovement(new Vector2(0f, 0f), 1f);
            ScentTrailSystem.RecordMovement(new Vector2(8f, 0f), 2f);
            ScentTrailSystem.ClearBounds(new Bounds(Vector3.zero, new Vector3(4f, 4f, 1f)));

            Assert.That(ScentTrailSystem.Count, Is.EqualTo(1));
            Assert.That(ScentTrailSystem.TryGetNext(0, new Vector2(8f, 0f), 1f, 2f, out var retained), Is.True);
            Assert.That(retained.position, Is.EqualTo(new Vector2(8f, 0f)));
        }

        [Test]
        public void CheckpointResetClearsTrailAndResetsItsCursorIds()
        {
            ScentTrailSystem.RecordMovement(Vector2.zero, 1f);
            ScentTrailSystem.RecordMovement(Vector2.right, 2f);

            ScentTrailSystem.Reset();
            var firstAfterReset = ScentTrailSystem.RecordMovement(Vector2.up, 3f);

            Assert.That(ScentTrailSystem.Count, Is.EqualTo(1));
            Assert.That(firstAfterReset, Is.EqualTo(1));
            Assert.That(ScentTrailSystem.TryGetNext(0, Vector2.up, 1f, 3f, out _), Is.True);
        }

        [Test]
        public void LandedMeatHasOneDogClaimAndEatingGetsItsOwnEightSecondTimer()
        {
            var window = new MeatDistractionWindow();
            window.Land(now: 5f, availabilitySeconds: MeatDistractionSource.AvailabilitySeconds);
            Assert.That(window.CanClaim(12.99f), Is.True);
            Assert.That(window.CanClaim(13f), Is.False);

            var firstDog = new object();
            var secondDog = new object();
            Assert.That(window.TryClaim(firstDog, 12f), Is.True);
            Assert.That(window.TryClaim(secondDog, 12f), Is.False);
            Assert.That(window.TryStartEating(firstDog, now: 13f), Is.True);
            Assert.That(window.IsEating(firstDog, 20.99f), Is.True);
            Assert.That(window.IsEating(firstDog, 21f), Is.False);
            Assert.That(window.IsExpired(21f), Is.True);
        }

        [Test]
        public void LandedMeatRemainsVisibleAndDiscoverableUntilOneNearbyDogClaimsIt()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            _assets.Add(sprite);
            _assets.Add(texture);
            var sourceObject = NewObject("landed-meat");
            var renderer = sourceObject.AddComponent<SpriteRenderer>();
            var source = sourceObject.AddComponent<MeatDistractionSource>();
            const float now = 100f;
            source.Land(Vector2.zero, sprite, now);
            Assert.That(renderer.sprite, Is.EqualTo(sprite));
            Assert.That(renderer.enabled, Is.True);

            var firstDog = CreateDog("first-dog");
            var secondDog = CreateDog("second-dog");
            Assert.That(MeatDistractionSource.TryClaimNearest(firstDog, new Vector2(6f, 0f), 5f, now, out _), Is.False);
            Assert.That(MeatDistractionSource.TryClaimNearest(firstDog, Vector2.zero, 5f, now, out var claimed), Is.True);
            Assert.That(claimed, Is.EqualTo(source));
            Assert.That(MeatDistractionSource.TryClaimNearest(secondDog, Vector2.zero, 5f, now, out _), Is.False);
        }

        [Test]
        public void TrapAndTripwireWorldRecordsRestoreDerivedColliderState()
        {
            var trapRoot = NewObject("persisted-bear-trap", false);
            var visualObject = NewObject("trap-visual", false);
            visualObject.transform.SetParent(trapRoot.transform, false);
            var sprite = visualObject.AddComponent<SpriteRenderer>();
            var triggerObject = NewObject("trap-trigger", false);
            triggerObject.transform.SetParent(trapRoot.transform, false);
            var trapCollider = triggerObject.AddComponent<BoxCollider2D>();
            var trap = trapRoot.AddComponent<BearTrapHazard>();
            SetField(trap, "persistentId", "bear-trap-east");
            SetField(trap, "hiddenTrapSprite", sprite);
            SetField(trap, "trapTrigger", trapCollider);
            trapRoot.SetActive(true);
            var trapEntities = new IPersistedWorldState[] { trap };

            WorldStateRegistry.Restore(trapEntities, new[] { new WorldEntityRecord { stableId = "bear-trap-east", state = "revealed" } });
            Assert.That(sprite.enabled, Is.True);
            Assert.That(trapCollider.enabled, Is.True);
            WorldStateRegistry.Restore(trapEntities, new[] { new WorldEntityRecord { stableId = "bear-trap-east", state = "triggered" } });
            Assert.That(trap.CaptureWorldState().state, Is.EqualTo("triggered"));
            Assert.That(trapCollider.enabled, Is.False);
            WorldStateRegistry.RestoreBaseline(trapEntities);
            Assert.That(trap.CaptureWorldState().state, Is.EqualTo("hidden"));

            var wireObject = NewObject("persisted-tripwire", false);
            var wireCollider = wireObject.AddComponent<BoxCollider2D>();
            var wire = wireObject.AddComponent<TripwireHazard>();
            SetField(wire, "persistentId", "tripwire-west");
            SetField(wire, "trigger", wireCollider);
            wireObject.SetActive(true);
            var wireEntities = new IPersistedWorldState[] { wire };
            WorldStateRegistry.Restore(wireEntities, new[] { new WorldEntityRecord { stableId = "tripwire-west", state = "triggered" } });
            Assert.That(wireCollider.enabled, Is.False);
            Assert.That(TripwireHazard.CanTrigger(false, playerHasMotor: true, playerIsSneaking: true), Is.False);
            Assert.That(TripwireHazard.CanTrigger(false, playerHasMotor: true, playerIsSneaking: false), Is.True);
            WorldStateRegistry.RestoreBaseline(wireEntities);
            Assert.That(wireCollider.enabled, Is.True);
        }

        private GameObject NewObject(string name, bool active = true)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(active);
            _objects.Add(gameObject);
            return gameObject;
        }

        private PoliceDogBrain CreateDog(string name)
        {
            var gameObject = NewObject(name);
            gameObject.AddComponent<Rigidbody2D>();
            return gameObject.AddComponent<PoliceDogBrain>();
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing serialized field " + name);
            field.SetValue(target, value);
        }
    }
}
