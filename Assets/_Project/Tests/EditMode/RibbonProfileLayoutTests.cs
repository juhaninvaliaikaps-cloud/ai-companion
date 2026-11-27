using AICompanion.NewUI.Wheel;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonProfileLayoutTests
    {
        [Test]
        public void WideLayoutShowsTheArtworkAndAllFourTabsInOneRow()
        {
            var layout = new RibbonProfileLayout(new Rect(0, 0, 1180, 820), new Vector2(600, 400), 1f);
            Assert.That(layout.HeaderHeight, Is.EqualTo(layout.Frame.width / 3f));
            Assert.That(layout.Close.xMax, Is.LessThan(layout.Frame.width));
            Assert.That(layout.Close.yMax, Is.LessThan(layout.HeaderHeight));
            Assert.That(layout.TabColumns, Is.EqualTo(4));
            Assert.That(layout.Tab(0).y, Is.EqualTo(layout.Tab(3).y));
            Assert.That(layout.Tab(3).yMax, Is.LessThan(layout.BodyTop));
        }

        [TestCase(960f, 300f)]
        [TestCase(330f, 250f)]
        public void ShortProfileKeepsScrollableSpaceBelowTheFullWidthBanner(float width, float height)
        {
            var area = new Rect(0f, 0f, width, height);
            var layout = new RibbonProfileLayout(area, area.center, 1.5f);
            Assert.That(layout.HeaderHeight, Is.EqualTo(layout.Frame.width / 3f));
            Assert.That(layout.Close.yMax, Is.LessThan(layout.HeaderHeight));
            Assert.That(layout.Frame.height - layout.BodyTop - layout.Padding, Is.GreaterThan(110f * layout.Scale));
        }

        [Test]
        public void NarrowHighDpiProfileFitsTheVisibleAreaAndStacksTabs()
        {
            var area = new Rect(200, 140, 400, 450);
            var layout = new RibbonProfileLayout(area, new Vector2(200, 600), 1.5f);
            Assert.That(layout.HeaderHeight, Is.EqualTo(layout.Frame.width / 3f));
            Assert.That(layout.Close.yMax, Is.LessThan(layout.HeaderHeight));
            Assert.That(layout.TabColumns, Is.EqualTo(2));
            Assert.That(layout.Tab(2).yMin, Is.GreaterThan(layout.Tab(0).yMax));
            Assert.That(layout.Frame.xMin, Is.GreaterThan(area.xMin));
            Assert.That(layout.Frame.yMax, Is.LessThan(area.yMax));
            Assert.That(layout.BodyTop, Is.LessThan(layout.Frame.height - 120f * layout.Scale));
        }
    }
}
