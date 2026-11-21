using System.IO;
using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-166: her picture opens at full size while the mouse rests on the small one.</summary>
    public sealed class PicturePreviewTests
    {
        static readonly Rect Window = new Rect(-675f, -423f, 1350f, 846f);

        [Test]
        public void Fit_ShowsItsOwnSize_OrSmallerToFit()
        {
            Assert.AreEqual(new Vector2(256f, 256f), PicturePreviewMath.Fit(256f, 256f, new Vector2(1200f, 760f)), "never larger than the picture");
            var big = PicturePreviewMath.Fit(2000f, 1000f, new Vector2(1000f, 760f));
            Assert.AreEqual(1000f, big.x, 0.01f);
            Assert.AreEqual(500f, big.y, 0.01f, "keeps its shape");
            var tall = PicturePreviewMath.Fit(800f, 1600f, new Vector2(1200f, 800f));
            Assert.AreEqual(400f, tall.x, 0.01f);
            Assert.AreEqual(800f, tall.y, 0.01f);
            Assert.AreEqual(Vector2.zero, PicturePreviewMath.Fit(0f, 100f, new Vector2(100f, 100f)));
        }

        [Test]
        public void Place_GoesAboveTheSmallPicture_AndStaysInTheWindow()
        {
            var anchor = new Rect(-600f, -300f, 40f, 40f);
            var size = new Vector2(300f, 300f);
            var centre = PicturePreviewMath.Place(Window, anchor, size, 8f);
            Assert.Greater(centre.y - size.y * 0.5f, anchor.yMax, "above it, so the small picture stays visible");
            Assert.GreaterOrEqual(centre.x - size.x * 0.5f, Window.xMin + 8f, "not past the left edge");

            var high = new Rect(-20f, 360f, 40f, 40f);
            var below = PicturePreviewMath.Place(Window, high, size, 8f);
            Assert.Less(below.y + size.y * 0.5f, high.yMin, "near the top it opens below");

            var huge = PicturePreviewMath.Place(Window, anchor, new Vector2(2000f, 2000f), 8f);
            Assert.AreEqual(Window.center, huge, "bigger than the window: centred");
        }

        [Test]
        public void TheChat_DrivesIt_AndItNeverTakesTheMouse()
        {
            var root = Path.Combine(Application.dataPath, "_Project/Scripts/UI/NewUI/");
            StringAssert.Contains("_picturePreview.Tick(overPicture,", File.ReadAllText(root + "HappiChatView.cs"));
            var preview = File.ReadAllText(root + "PictureHoverPreview.cs");
            StringAssert.Contains("_image.raycastTarget = false;", preview);
            StringAssert.Contains("HappiUiKit.Panel(_window, \"PicturePreview\", _theme.PanelBackground, false)", preview, "the frame doesn't take clicks either");
        }
    }
}
