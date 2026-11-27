using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-228: the part of the wallpaper behind the door.</summary>
    public sealed class WallpaperCropTests
    {
        static void Near(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f, "x");
            Assert.AreEqual(expected.y, actual.y, 0.0001f, "y");
            Assert.AreEqual(expected.width, actual.width, 0.0001f, "width");
            Assert.AreEqual(expected.height, actual.height, 0.0001f, "height");
        }

        [Test]
        public void SameShape_TheWholeMonitorIsTheWholePicture()
        {
            var monitor = new Rect(0f, 0f, 1920f, 1080f);
            Near(new Rect(0f, 0f, 1f, 1f), WallpaperCrop.Uv(3840, 2160, monitor, monitor));
        }

        [Test]
        public void TheTopLeftQuarter_IsTheTopOfTheTexture()
        {
            var monitor = new Rect(0f, 0f, 1920f, 1080f);
            Near(new Rect(0f, 0.5f, 0.5f, 0.5f), WallpaperCrop.Uv(1920, 1080, monitor, new Rect(0f, 0f, 960f, 540f)));
        }

        [Test]
        public void AWiderPicture_IsCroppedAtTheSides()
        {
            // A 2:1 picture on a 16:9 monitor: the middle 88.9 % of its width shows.
            var monitor = new Rect(0f, 0f, 1600f, 900f);
            var uv = WallpaperCrop.Uv(2000, 1000, monitor, monitor);
            Assert.AreEqual(0f, uv.y, 0.0001f);
            Assert.AreEqual(1f, uv.height, 0.0001f);
            Assert.AreEqual(0.8889f, uv.width, 0.0001f);
            Assert.AreEqual((1f - 0.8889f) / 2f, uv.x, 0.0001f);
        }

        [Test]
        public void ASecondMonitor_UsesItsOwnOrigin()
        {
            var monitor = new Rect(1920f, 0f, 1920f, 1080f);
            Near(new Rect(0.5f, 0.5f, 0.5f, 0.5f), WallpaperCrop.Uv(1920, 1080, monitor, new Rect(2880f, 0f, 960f, 540f)));
        }

        [Test]
        public void NoPicture_GivesTheWholeRect()
        {
            Near(new Rect(0f, 0f, 1f, 1f), WallpaperCrop.Uv(0, 0, new Rect(0f, 0f, 10f, 10f), new Rect(0f, 0f, 1f, 1f)));
        }
    }
}
