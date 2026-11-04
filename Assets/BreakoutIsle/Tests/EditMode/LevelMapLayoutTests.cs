using NUnit.Framework;
using UnityEngine;
using BreakoutIsle.Editor;

namespace BreakoutIsle.Tests
{
    public sealed class LevelMapLayoutTests
    {
        [Test]
        public void DefaultMapLayoutPreservesLevel01Bounds()
        {
            var layout = BreakoutUiBuilder.CreateLevel01MapLayout();

            Assert.That(layout.levelTitle, Is.EqualTo("NHÀ TÙ · CỤM 01"));
            Assert.That(layout.worldBounds, Is.EqualTo(new Rect(2f, 2f, 36f, 20f)));
            Assert.That(layout.regions, Has.Length.EqualTo(6));
        }

        [Test]
        public void NormalizedMarkersUseTheAuthoredLevelBounds()
        {
            var bounds = new Rect(2f, 2f, 42f, 26f);
            var gate = BreakoutUiBuilder.NormalizeMapPosition(bounds, new Vector2(38f, 13f));

            Assert.That(gate.x, Is.EqualTo(36f / 42f).Within(0.0001f));
            Assert.That(gate.y, Is.EqualTo(11f / 26f).Within(0.0001f));
            Assert.That(BreakoutUiBuilder.NormalizeMapPosition(bounds, new Vector2(2f, 2f)), Is.EqualTo(Vector2.zero));
            Assert.That(BreakoutUiBuilder.NormalizeMapPosition(bounds, new Vector2(44f, 28f)), Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Level02LayoutUsesItsAuthoredBoundsRegionsAndExitMarker()
        {
            var layout = BreakoutUiBuilder.CreateLevel02MapLayout(new Vector2(38f, 13f));

            Assert.That(layout.levelTitle, Is.EqualTo("NHÀ TÙ · MÀN 02/10"));
            Assert.That(layout.worldBounds, Is.EqualTo(new Rect(2f, 2f, 42f, 26f)));
            Assert.That(layout.regions, Has.Length.EqualTo(4));
            Assert.That(layout.markers, Has.Length.EqualTo(1));
            Assert.That(layout.markers[0].worldPosition, Is.EqualTo(new Vector2(38f, 13f)));
            Assert.That(BreakoutUiBuilder.NormalizeMapPosition(layout.worldBounds, layout.markers[0].worldPosition).x,
                Is.EqualTo(36f / 42f).Within(0.0001f));
        }

        [Test]
        public void MarkerNormalizationRejectsInvalidBounds()
        {
            Assert.Throws<System.ArgumentException>(() =>
                BreakoutUiBuilder.NormalizeMapPosition(new Rect(0f, 0f, 0f, 8f), Vector2.zero));
        }
    }
}
