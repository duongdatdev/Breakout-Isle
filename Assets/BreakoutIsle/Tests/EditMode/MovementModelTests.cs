using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public class MovementModelTests
    {
        [Test]
        public void DiagonalInputIsNormalizedWithoutChangingCardinalSpeed()
        {
            var diagonal = MovementMath.NormalizeInput(new Vector2(1f, 1f));
            var cardinal = MovementMath.NormalizeInput(Vector2.right);

            Assert.That(diagonal.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(cardinal, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void CameraPositionSnapsToTheThirtyTwoPixelWorldGrid()
        {
            var snapped = PixelCameraFollow.SnapToPixelGrid(new Vector3(1.01f, -0.49f, -10f), 32);

            Assert.That(snapped.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(snapped.y, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(snapped.z, Is.EqualTo(-10f));
        }

        [Test]
        public void StandingSpritesSortByFeetPositionOnTheSharedActorLayer()
        {
            var closerToCamera = YSortRenderer.SortingOrderForY(2f, 100);
            var fartherFromCamera = YSortRenderer.SortingOrderForY(3f, 100);

            Assert.That(closerToCamera, Is.GreaterThan(fartherFromCamera));
        }

        [Test]
        public void StaminaDrainsAtTwentyPerSecondAndExhaustionDisablesSprint()
        {
            var stamina = new StaminaMeter();

            Assert.That(stamina.Tick(true, 1f), Is.True);
            Assert.That(stamina.Current, Is.EqualTo(80f).Within(0.001f));

            Assert.That(stamina.Tick(true, 4f), Is.True);
            Assert.That(stamina.Current, Is.EqualTo(0f).Within(0.001f));
            Assert.That(stamina.Tick(true, 0.1f), Is.False);
        }

        [Test]
        public void StaminaRecoveryWaitsOneSecondThenRecoversAtFifteenPerSecond()
        {
            var stamina = new StaminaMeter();
            stamina.Tick(true, 1f);

            stamina.Tick(false, 0.75f);
            Assert.That(stamina.Current, Is.EqualTo(80f).Within(0.001f));

            stamina.Tick(false, 0.5f);
            Assert.That(stamina.Current, Is.EqualTo(83.75f).Within(0.001f));

            stamina.Tick(false, 0.25f);
            Assert.That(stamina.Current, Is.EqualTo(87.5f).Within(0.001f));
        }

        [Test]
        public void ExhaustionAllowsRecoveryButRequiresShiftReleaseBeforeSprintingAgain()
        {
            var stamina = new StaminaMeter();

            Assert.That(stamina.Tick(true, 5f), Is.True);
            Assert.That(stamina.Current, Is.EqualTo(0f));
            Assert.That(stamina.Tick(true, 1.1f), Is.False);
            Assert.That(stamina.Current, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(stamina.Tick(true, 0.1f), Is.False);

            stamina.Tick(false, 0.1f);
            Assert.That(stamina.Tick(true, 0.01f), Is.True);
        }
    }
}
