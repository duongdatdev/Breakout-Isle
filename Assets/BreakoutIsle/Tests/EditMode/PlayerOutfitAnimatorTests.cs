using BreakoutIsle.Gameplay;
using NUnit.Framework;

namespace BreakoutIsle.Tests
{
    public sealed class PlayerOutfitAnimatorTests
    {
        [TestCase("civilian")]
        [TestCase("civilian_clothes")]
        public void RecognizesCivilianOutfitIds(string outfitId)
        {
            Assert.That(PlayerOutfitAnimator.IsCivilianOutfit(outfitId), Is.True);
        }

        [Test]
        public void KeepsPrisonMissionActionsExplicit()
        {
            Assert.That(PlayerOutfitAnimator.IsPrisonOnlyAction("pry"), Is.True);
            Assert.That(PlayerOutfitAnimator.IsPrisonOnlyAction("crawl"), Is.True);
            Assert.That(PlayerOutfitAnimator.IsPrisonOnlyAction("climb"), Is.True);
            Assert.That(PlayerOutfitAnimator.IsPrisonOnlyAction("interact"), Is.False);
            Assert.That(PlayerOutfitAnimator.IsPrisonOnlyAction("throw"), Is.False);
        }
    }
}
