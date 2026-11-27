using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonChatOptionsLayoutTests
    {
        [Test]
        public void WideBoxKeepsOneColumnAlignedWithTheGear()
        {
            var layout = new RibbonChatOptionsLayout(new Vector2(560f, 392f), 64f, 423f, 1f);
            Assert.That(layout.Columns, Is.EqualTo(1));
            Assert.That(layout.Bounds.xMax, Is.EqualTo(423f));
            Assert.That(layout.Bounds.yMin, Is.GreaterThan(64f));
            Assert.That(layout.Neighbor(0, 0, 1), Is.EqualTo(1));
            Assert.That(layout.Neighbor(0, 1, 0), Is.EqualTo(-1));
        }

        [TestCase(1f)]
        [TestCase(1.5f)]
        public void MinimumBoxFitsAllOptionsBelowTheHeader(float scale)
        {
            var layout = new RibbonChatOptionsLayout(new Vector2(260f, 160f) * scale, 46f * scale,
                249f * scale, scale);
            Assert.That(layout.Columns, Is.EqualTo(2));
            Assert.That(layout.Bounds.yMin, Is.GreaterThan(46f * scale));
            Assert.That(layout.Bounds.yMax, Is.LessThan(160f * scale));
            Assert.That(layout.Bounds.xMax, Is.LessThan(260f * scale));
            for (var i = 0; i < RibbonChatOptionsLayout.OptionCount; i++)
            {
                var option = layout.Option(i);
                Assert.That(option.width, Is.GreaterThan(100f * scale));
                Assert.That(option.height, Is.GreaterThanOrEqualTo(23f * scale));
                Assert.That(option.xMax, Is.LessThan(layout.Bounds.width));
                Assert.That(option.yMax, Is.LessThan(layout.Bounds.height));
            }
        }

        [Test]
        public void CompactNavigationCanReachTheLastUnpairedButton()
        {
            var layout = new RibbonChatOptionsLayout(new Vector2(260f, 160f), 46f, 249f, 1f);
            Assert.That(layout.Neighbor(0, 1, 0), Is.EqualTo(1));
            Assert.That(layout.Neighbor(1, 0, 1), Is.EqualTo(3));
            Assert.That(layout.Neighbor(3, 0, 1), Is.EqualTo(4));
            Assert.That(layout.Neighbor(4, 0, -1), Is.EqualTo(2));
            Assert.That(layout.Neighbor(4, 0, 1), Is.EqualTo(-1));
        }
    }
}
