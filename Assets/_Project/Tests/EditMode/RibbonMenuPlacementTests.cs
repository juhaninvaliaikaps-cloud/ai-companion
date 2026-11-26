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
        public void BannerFlareGrowsTowardTheArtworkAndFitsTheAvailableHeight()
        {
            var full = RibbonMenuPlacement.ScrollFlare(RibbonMenuPlacement.PreferredFlyoutWidth, 1f, 100f);
            var clipped = RibbonMenuPlacement.ScrollFlare(RibbonMenuPlacement.PreferredFlyoutWidth, 1f, 20f);
            var compact = RibbonMenuPlacement.ScrollFlare(158f, 1f, 100f);

            Assert.That(full.x, Is.EqualTo(18f).Within(0.001f));
            Assert.That(full.y, Is.EqualTo(49f).Within(0.001f));
            Assert.That(clipped.x + clipped.y, Is.EqualTo(20f).Within(0.001f));
            Assert.That(clipped.y, Is.GreaterThan(clipped.x));
            Assert.That(compact, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void WidePersonaBannerRetainsTheFullPoseAtTheFarEnd()
        {
            var area = new Rect(380f, 0f, 520f, 184f);
            var (frame, uv) = RibbonMenuPlacement.PersonaPainting(area, 3f);

            Assert.That(frame.xMax, Is.EqualTo(area.xMax).Within(0.001f));
            Assert.That(frame.width / frame.height, Is.EqualTo(uv.width * 3f).Within(0.001f));
            Assert.That(uv.yMin, Is.Zero);
            Assert.That(uv.yMax, Is.EqualTo(1f));
            Assert.That(uv.xMax, Is.EqualTo(1f));
        }

        [Test]
        public void CompactPersonaBannerCropsAroundTheFace()
        {
            var area = new Rect(185f, 0f, 160f, 128f);
            var (frame, uv) = RibbonMenuPlacement.PersonaPainting(area, 3f);

            Assert.That(frame, Is.EqualTo(area));
            Assert.That(uv.center.x, Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(uv.width / uv.height * 3f, Is.EqualTo(area.width / area.height).Within(0.001f));
        }

        [Test]
        public void PersonaBannerPrefersAboveAndShrinksToKeepTheHeaderClear()
        {
            var area = new Rect(40f, 20f, 1000f, 580f);
            var header = new Rect(310f, 155f, 130f, 38f);
            var (banner, opensUp) = RibbonMenuPlacement.PersonaBanner(area, header, new Vector2(900f, 192f), 112f);

            Assert.That(opensUp, Is.True);
            Assert.That(banner.yMin, Is.EqualTo(area.yMin));
            Assert.That(banner.yMax, Is.EqualTo(header.yMin));
            Assert.That(banner.xMax, Is.EqualTo(area.xMax));
        }

        [Test]
        public void PersonaBannerOpensBelowTheEntireHeaderWhenThereIsTooLittleRoomAbove()
        {
            var area = new Rect(70f, 60f, 520f, 300f);
            var header = new Rect(110f, 80f, 130f, 66f);
            var (banner, opensUp) = RibbonMenuPlacement.PersonaBanner(area, header, new Vector2(900f, 192f), 112f);

            Assert.That(opensUp, Is.False);
            Assert.That(banner.yMin, Is.EqualTo(header.yMax));
            Assert.That(banner.width, Is.EqualTo(area.width));
            Assert.That(banner.yMax, Is.LessThanOrEqualTo(area.yMax));
        }

        [Test]
        public void IllustrationCropPreservesTheFocalPointInNarrowArtwork()
        {
            var uv = RibbonMenuPlacement.IllustrationUv(3f, 1.5f, 0.7f);

            Assert.That(uv.center.x, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(uv.size, Is.EqualTo(new Vector2(0.5f, 1f)));
        }

        [Test]
        public void WideFrameCropsHeightToKeepTheIllustrationInProportion()
        {
            var uv = RibbonMenuPlacement.IllustrationUv(2f, 4f, 0.6f);
            Assert.That(uv, Is.EqualTo(new Rect(0f, 0.25f, 1f, 0.5f)));
        }

        [Test]
        public void ArtworkCropCanTrimEmptySourcePaperWithoutStretchingThePainting()
        {
            var uv = RibbonMenuPlacement.IllustrationUv(3f, 3f, 0.64f, 0.30f);

            Assert.That(uv.xMin, Is.EqualTo(0.30f).Within(0.001f));
            Assert.That(uv.yMin, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(uv.size.x, Is.EqualTo(uv.size.y).Within(0.001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DiagonalTravelTowardTheFlyoutKeepsItsParent(bool opensRight)
        {
            var flyout = new Rect(opensRight ? 200f : 10f, 90f, 190f, 150f);
            var origin = new Vector2(opensRight ? 100f : 300f, 100f);
            var next = new Vector2(opensRight ? 145f : 255f, 130f);

            Assert.That(RibbonMenuPlacement.AimsAtFlyout(origin, next, flyout, opensRight, 8f), Is.True);
            Assert.That(RibbonMenuPlacement.AimsAtFlyout(origin, new Vector2(origin.x, 139f), flyout, opensRight, 8f), Is.False);
            Assert.That(RibbonMenuPlacement.AimsAtFlyout(origin, new Vector2(next.x, 210f), flyout, opensRight, 8f), Is.False);
        }
    }
}
