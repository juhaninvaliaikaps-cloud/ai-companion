using AICompanion.NewUI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonChatHeaderLayoutTests
    {
        [TestCase(1f)]
        [TestCase(1.5f)]
        public void WideHeaderSharesTheFrameEdgeAndKeepsControlsClearOfTheArt(float scale)
        {
            var width = 560f * scale;
            var header = new RibbonChatHeaderLayout(width, 12f * scale, scale, wide: true, canStack: true,
                showAvatar: true, hasArt: true);

            Assert.That(header.Art.yMin, Is.Zero);
            Assert.That(header.Art.xMax, Is.EqualTo(width).Within(0.01f));
            Assert.That(header.Art.yMax, Is.EqualTo(header.Bottom));
            Assert.That(header.Art.width, Is.GreaterThan(100f * scale));
            Assert.That(header.Avatar.xMax, Is.LessThan(header.Title.xMin));
            Assert.That(header.Title.xMax, Is.LessThan(header.Privacy.xMin));
            Assert.That(header.Voice.xMax, Is.LessThan(header.Divider.xMin));
            Assert.That(header.Divider.xMax, Is.LessThan(header.Hide.xMin));
            Assert.That(header.Hide.xMax, Is.LessThan(header.Options.xMin));
            Assert.That(header.Options.xMax, Is.LessThan(header.Art.xMin));
        }

        [Test]
        public void NarrowHeaderKeepsArtAndPlacesControlsUnderTheTitleWhenItCanStack()
        {
            var header = new RibbonChatHeaderLayout(260f, 12f, 1f, wide: false, canStack: true,
                showAvatar: true, hasArt: true);

            Assert.That(header.Title.yMax, Is.LessThan(header.Privacy.yMin));
            Assert.That(header.Art.width, Is.Positive);
            Assert.That(header.Title.xMax, Is.LessThan(header.Art.xMin));
            Assert.That(header.Options.xMax, Is.LessThan(header.Art.xMin));
            Assert.That(header.Privacy.xMin, Is.GreaterThanOrEqualTo(12f));
        }

        [Test]
        public void ShortNarrowHeaderGivesItsSpaceToTheTitleAndControls()
        {
            var header = new RibbonChatHeaderLayout(260f, 12f, 1f, wide: false, canStack: false,
                showAvatar: true, hasArt: true);

            Assert.That(header.Art.width, Is.Zero);
            Assert.That(header.Avatar.width, Is.Positive);
            Assert.That(header.Title.width, Is.GreaterThanOrEqualTo(57f));
            Assert.That(header.Title.xMax, Is.LessThan(header.Privacy.xMin));
            Assert.That(header.Options.xMax, Is.EqualTo(248f));
        }
    }
}
