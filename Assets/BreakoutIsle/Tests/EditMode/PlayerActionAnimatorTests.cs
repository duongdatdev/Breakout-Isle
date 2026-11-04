using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BreakoutIsle.Tests
{
    public sealed class PlayerActionAnimatorTests
    {
        [Test]
        public void DirectionQuantizationPreservesAllEightDirections()
        {
            Assert.That(PlayerActionAnimator.DirectionName(Vector2.down), Is.EqualTo("S"));
            Assert.That(PlayerActionAnimator.DirectionName(new Vector2(-1f, -1f)), Is.EqualTo("SW"));
            Assert.That(PlayerActionAnimator.DirectionName(Vector2.left), Is.EqualTo("W"));
            Assert.That(PlayerActionAnimator.DirectionName(new Vector2(-1f, 1f)), Is.EqualTo("NW"));
            Assert.That(PlayerActionAnimator.DirectionName(Vector2.up), Is.EqualTo("N"));
            Assert.That(PlayerActionAnimator.DirectionName(new Vector2(1f, 1f)), Is.EqualTo("NE"));
            Assert.That(PlayerActionAnimator.DirectionName(Vector2.right), Is.EqualTo("E"));
            Assert.That(PlayerActionAnimator.DirectionName(new Vector2(1f, -1f)), Is.EqualTo("SE"));
        }

        [Test]
        public void ActionDurationComesFromFrameCountAndFps()
        {
            var owner = new GameObject("Action Animator Test");
            try
            {
                var animator = owner.AddComponent<Animator>();
                var actionAnimator = owner.AddComponent<PlayerActionAnimator>();
                actionAnimator.Configure(animator, null, null, new[]
                {
                    new PlayerActionAnimator.ActionDefinition { action = "pry", frameCount = 6, fps = 10, loop = false },
                    new PlayerActionAnimator.ActionDefinition { action = "crawl", frameCount = 6, fps = 10, loop = true }
                });

                Assert.That(actionAnimator.TryGetActionDuration("pry", out var pryDuration), Is.True);
                Assert.That(pryDuration, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(actionAnimator.TryGetActionDuration("crawl", out var crawlDuration), Is.True);
                Assert.That(crawlDuration, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(actionAnimator.TryGetActionDuration("missing", out _), Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
