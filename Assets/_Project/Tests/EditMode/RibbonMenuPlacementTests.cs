using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonMenuPlacementTests
    {
        [Test]
        public void ShortWorkAreaScalesTheStackAndKeepsTheTriggerOutsideTheRows()
        {
            var area = new Rect(20f, 70f, 620f, 200f);
            var scale = RibbonMenuPlacement.FitScale(area.size, new Vector2(328f, 267f), 1.5f);
            var triggerHeight = 30f * scale;
            var menuHeight = 237f * scale;
            var (trigger, menu) = RibbonMenuPlacement.Stack(area.center.y + 35f, area, triggerHeight, menuHeight);

            Assert.That(scale, Is.LessThan(1f));
            Assert.That(menu, Is.EqualTo(area.yMin).Within(0.001f));
            Assert.That(trigger, Is.EqualTo(menu + menuHeight).Within(0.001f));
            Assert.That(trigger + triggerHeight, Is.EqualTo(area.yMax).Within(0.001f));
        }

        [TestCase(30f, true)]
        [TestCase(660f, false)]
        public void FlyoutStaysOnTheRoomierSideAndAboveTheBottomEdge(float triggerX, bool expectedRight)
        {
            var area = new Rect(30f, 50f, 800f, 400f);
            var menuX = RibbonMenuPlacement.MenuX(area, triggerX, 170f, 158f);
            var menu = new Rect(menuX, 210f, 170f, 237f);
            var right = RibbonMenuPlacement.OpensRight(area, menu, 440f);
            var flyout = RibbonMenuPlacement.Flyout(area, menu, new Vector2(440f, 223f), 117f, right);

            Assert.That(right, Is.EqualTo(expectedRight));
            Assert.That(flyout.width, Is.EqualTo(440f));
            Assert.That(right ? flyout.xMin : flyout.xMax, Is.EqualTo(right ? menu.xMax : menu.xMin));
            Assert.That(flyout.yMax, Is.EqualTo(area.yMax));
        }

        [Test]
        public void TightWidthReservesRoomForButtonsBesideTheMenu()
        {
            var area = new Rect(40f, 30f, 328f, 500f);
            var x = RibbonMenuPlacement.MenuX(area, 110f, 170f, 158f);
            var menu = new Rect(x, 75f, 170f, 237f);
            var right = RibbonMenuPlacement.OpensRight(area, menu, 440f);
            var flyout = RibbonMenuPlacement.Flyout(area, menu, new Vector2(440f, 223f), 117f, right);

            Assert.That(flyout.width, Is.EqualTo(158f));
            Assert.That(flyout.xMin, Is.GreaterThanOrEqualTo(area.xMin));
            Assert.That(flyout.xMax, Is.LessThanOrEqualTo(area.xMax));
        }

        [Test]
        public void IllustrationRevealsTheAdjacentEdgeWithoutMirroringTheFinalImage()
        {
            var fullRight = RibbonMenuPlacement.IllustrationUv(2f, 1f, 0.6f, 1f, true);
            var fullLeft = RibbonMenuPlacement.IllustrationUv(2f, 1f, 0.6f, 1f, false);
            var partRight = RibbonMenuPlacement.IllustrationUv(2f, 1f, 0.6f, 0.25f, true);
            var partLeft = RibbonMenuPlacement.IllustrationUv(2f, 1f, 0.6f, 0.25f, false);

            Assert.That(fullLeft, Is.EqualTo(fullRight));
            Assert.That(partRight.xMin, Is.EqualTo(fullRight.xMin).Within(0.001f));
            Assert.That(partLeft.xMax, Is.EqualTo(fullLeft.xMax).Within(0.001f));
            Assert.That(partLeft.width, Is.EqualTo(0.125f));
        }

        [Test]
        public void WideFrameCropsHeightToKeepTheIllustrationInProportion()
        {
            var uv = RibbonMenuPlacement.IllustrationUv(2f, 4f, 0.6f, 1f, true);
            Assert.That(uv, Is.EqualTo(new Rect(0f, 0.25f, 1f, 0.5f)));
        }
    }
}
