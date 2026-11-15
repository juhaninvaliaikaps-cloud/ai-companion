using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-132: the portrait frame resizing and its source-image cap.</summary>
    public sealed class PortraitResizeTests
    {
        static readonly Rect Start = new Rect(100f, 80f, 160f, 180f);
        static readonly Vector2 Min = new Vector2(64f, 72f);
        static readonly Vector2 Max = new Vector2(448f, 504f);
        static readonly Rect Bounds = new Rect(0f, 0f, 1920f, 1080f);

        [Test]
        public void RightEdge_MovesOnlyItself()
        {
            var right = PortraitResize.Apply(Start, new Vector2(80f, 0f), ResizeEdges.Right, Min, Max, Bounds);

            Assert.AreEqual(100f, right.xMin);
            Assert.AreEqual(340f, right.xMax);
            Assert.AreEqual(Start.yMin, right.yMin);
            Assert.AreEqual(Start.yMax, right.yMax);
        }

        [Test]
        public void CornerStaysInsideTheSourceImage()
        {
            var huge = PortraitResize.Apply(Start, new Vector2(2000f, 2000f), ResizeEdges.BottomRight, Min, Max, Bounds);

            Assert.AreEqual(Max.x, huge.width, 0.001f);
            Assert.AreEqual(Max.y, huge.height, 0.001f);
            Assert.AreEqual(Start.xMin, huge.xMin);
            Assert.AreEqual(Start.yMin, huge.yMin);
        }

        [Test]
        public void SavedSizeResolvesAtScaleAndCapsAtTheImage()
        {
            Assert.AreEqual(new Vector2(224f, 252f), PortraitResize.Resolve(224f, 252f, new Vector2(128f, 144f), 1f, Max));
            Assert.AreEqual(new Vector2(336f, 378f), PortraitResize.Resolve(224f, 252f, new Vector2(128f, 144f), 1.5f, Max));
            var capped = PortraitResize.Resolve(1000f, 1000f, new Vector2(128f, 144f), 1f, Max);
            Assert.AreEqual(Max.x, capped.x);
            Assert.AreEqual(Max.y, capped.y);
        }

        [Test]
        public void EdgeAt_FindsCornersAndIgnoresTheCenter()
        {
            Assert.AreEqual(ResizeEdges.TopLeft, PortraitResize.EdgeAt(Start, new Vector2(101f, 81f), 1f));
            Assert.AreEqual(ResizeEdges.Right, PortraitResize.EdgeAt(Start, new Vector2(259f, 180f), 1f));
            Assert.AreEqual(ResizeEdges.None, PortraitResize.EdgeAt(Start, new Vector2(180f, 170f), 1f));
            Assert.AreEqual(ResizeEdges.None, PortraitResize.EdgeAt(Start, new Vector2(500f, 500f), 1f));
        }
    }
}
