using BreakoutIsle.Gameplay;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public sealed class GuardResetTests
    {
[Test]
        public void ResetAllToBaselineRestoresPatrolPositionAndState()
        {
            var gameObject = new GameObject("Guard baseline test");
            GuardBrain guard = null;
            try
            {
                gameObject.transform.position = new Vector3(2f, 3f, 0f);
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.position = new Vector2(2f, 3f);
                guard = gameObject.AddComponent<GuardBrain>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(GuardBrain).GetMethod("Awake", flags).Invoke(guard, null);
                typeof(GuardBrain).GetMethod("OnEnable", flags).Invoke(guard, null);

                typeof(GuardBrain).GetMethod("SetState", flags).Invoke(guard, new object[] { GuardState.Suspicious });
                Assert.That(guard.State, Is.EqualTo(GuardState.Suspicious));
                body.position = new Vector2(9f, -4f);
                body.linearVelocity = Vector2.one;

                GuardBrain.ResetAllToBaseline();

                Assert.That(body.position, Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(guard.State, Is.EqualTo(GuardState.Patrol));
            }
            finally
            {
                if (guard != null)
                    typeof(GuardBrain).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guard, null);
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
