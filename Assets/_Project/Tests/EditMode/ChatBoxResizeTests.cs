using AICompanion.NewUI;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-127: the chat box resized by its edges and corners.</summary>
    public sealed class ChatBoxResizeTests
    {
        [TestCase(480f, 1f, 256f, 196f)]
        [TestCase(900f, 1f, 392f, 320f)]
        [TestCase(1400f, 1.5f, 588f, 480f)]
        public void RibbonAutomaticHeightMakesRoomForConversation(float bodyHeight, float scale, float ribbon, float classic)
        {
            Assert.That(ChatBoxResize.AutomaticHeight(bodyHeight, scale, 1f, true), Is.EqualTo(ribbon));
            Assert.That(ChatBoxResize.AutomaticHeight(bodyHeight, scale, 1f, false), Is.EqualTo(classic));
        }

        [TestCase(560f, 1f, 1f, 392f)]
        [TestCase(840f, 1.5f, 1f, 588f)]
        [TestCase(400f, 1f, 1f, 280f)]
        [TestCase(230f, 1f, 0.6f, 160f)]
        public void RibbonHeightTracksWidthUntilItReachesTheControlMinimum(float width, float scale, float setting, float expected)
        {
            Assert.That(ChatBoxResize.RibbonHeight(width, scale, setting), Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void CompactWorkAreaReducesScaleToLeaveRoomForControls()
        {
            var scale = ChatBoxResize.FitRibbonScale(1.5f, new Vector2(360f, 260f), true);
            Assert.That(scale, Is.EqualTo(1.25f).Within(0.01f));
            Assert.That(ChatBoxResize.FitRibbonScale(1.5f, new Vector2(1920f, 1040f), true), Is.EqualTo(1.5f));
        }

        static readonly Rect Start = new Rect(100f, 100f, 300f, 200f); // x 100..400, y 100..300
        static readonly Vector2 Min = new Vector2(260f, 160f);
        static readonly Rect ScreenArea = new Rect(0f, 0f, 1920f, 1080f);

        // ---- Resizing ------------------------------------------------------------------------

        [Test]
        public void AnEdge_MovesOnlyItself_TheOppositeEdgeStays()
        {
            var right = ChatBoxResize.Apply(Start, new Vector2(50f, 30f), ResizeEdges.Right, Min, ScreenArea);
            Assert.AreEqual(100f, right.xMin);
            Assert.AreEqual(450f, right.xMax);
            Assert.AreEqual(Start.yMin, right.yMin, "a side edge does not change the height");
            Assert.AreEqual(Start.yMax, right.yMax);

            var left = ChatBoxResize.Apply(Start, new Vector2(-40f, 0f), ResizeEdges.Left, Min, ScreenArea);
            Assert.AreEqual(60f, left.xMin);
            Assert.AreEqual(400f, left.xMax, "the right edge stays");

            var top = ChatBoxResize.Apply(Start, new Vector2(0f, 80f), ResizeEdges.Top, Min, ScreenArea);
            Assert.AreEqual(380f, top.yMax);
            Assert.AreEqual(100f, top.yMin);

            var bottom = ChatBoxResize.Apply(Start, new Vector2(0f, -60f), ResizeEdges.Bottom, Min, ScreenArea);
            Assert.AreEqual(40f, bottom.yMin);
            Assert.AreEqual(300f, bottom.yMax);
        }

        [Test]
        public void ACorner_MovesBothItsEdges()
        {
            var corner = ChatBoxResize.Apply(Start, new Vector2(-20f, 50f), ResizeEdges.TopLeft, Min, ScreenArea);
            Assert.AreEqual(80f, corner.xMin);
            Assert.AreEqual(400f, corner.xMax);
            Assert.AreEqual(100f, corner.yMin);
            Assert.AreEqual(350f, corner.yMax);

            var other = ChatBoxResize.Apply(Start, new Vector2(30f, -30f), ResizeEdges.BottomRight, Min, ScreenArea);
            Assert.AreEqual(430f, other.xMax);
            Assert.AreEqual(70f, other.yMin);
            Assert.AreEqual(100f, other.xMin);
            Assert.AreEqual(300f, other.yMax);
        }

        [Test]
        public void ItNeverGoesBelowTheMinimum_OrOffTheScreen()
        {
            var tiny = ChatBoxResize.Apply(Start, new Vector2(-500f, 500f), ResizeEdges.BottomRight, Min, ScreenArea);
            Assert.AreEqual(Min.x, tiny.width, 0.001f);
            Assert.AreEqual(Min.y, tiny.height, 0.001f);
            Assert.AreEqual(100f, tiny.xMin, "the fixed edges stay put at the minimum");
            Assert.AreEqual(300f, tiny.yMax);

            var huge = ChatBoxResize.Apply(Start, new Vector2(5000f, 5000f), ResizeEdges.TopRight, Min, ScreenArea);
            Assert.AreEqual(1920f, huge.xMax);
            Assert.AreEqual(1080f, huge.yMax);

            var past = ChatBoxResize.Apply(Start, new Vector2(-5000f, -5000f), ResizeEdges.BottomLeft, Min, ScreenArea);
            Assert.AreEqual(0f, past.xMin);
            Assert.AreEqual(0f, past.yMin);
        }

        [Test]
        public void NoEdges_ChangesNothing()
        {
            Assert.AreEqual(Start, ChatBoxResize.Apply(Start, new Vector2(70f, 70f), ResizeEdges.None, Min, ScreenArea));
        }

        // ---- Saved size ---------------------------------------------------------------------

        [Test]
        public void SavedSize_IsIn100PercentUnits_AndFollowsTheScale()
        {
            Assert.AreEqual(400f, ChatBoxResize.ToUnits(600f, 1.5f), 0.001f);
            Assert.AreEqual(600f, ChatBoxResize.Resolve(400f, 340f, 1.5f, 100f, 2000f), 0.001f);
            Assert.AreEqual(340f, ChatBoxResize.Resolve(0f, 340f, 1.5f, 100f, 2000f), "0 is the automatic size");
            Assert.AreEqual(340f, ChatBoxResize.Resolve(float.NaN, 340f, 1f, 100f, 2000f));
            Assert.AreEqual(1000f, ChatBoxResize.Resolve(3000f, 340f, 1f, 100f, 1000f), "a size from a bigger monitor is clamped");
            Assert.AreEqual(260f, ChatBoxResize.Resolve(50f, 340f, 1f, 260f, 1000f), "and never smaller than the minimum");
        }

        [Test]
        public void SettingsFile_KeepsTheSize_AndOldFilesAreAutomatic()
        {
            var settings = CompanionSettings.CreateDefault();
            Assert.AreEqual(0f, settings.ChatBoxWidthPx);
            Assert.AreEqual(0f, settings.ChatBoxHeightPx);

            settings.ChatBoxWidthPx = 420f;
            settings.ChatBoxHeightPx = 310f;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.AreEqual(420f, back.ChatBoxWidthPx, 0.01f);
            Assert.AreEqual(310f, back.ChatBoxHeightPx, 0.01f);

            var old = CompanionSettingsJson.Parse("{\n  \"version\": 1,\n  \"chatBoxHeight\": 1.4\n}\n", SettingsLegacyPrefs.None);
            Assert.AreEqual(0f, old.ChatBoxWidthPx, "an old file has no size: automatic");
            Assert.AreEqual(1.4f, old.ChatBoxHeight, 0.01f, "the old height factor still sets the automatic height");

            var junk = CompanionSettingsJson.Parse("{\n  \"version\": 1,\n  \"chatBoxWidthPx\": -5\n}\n", SettingsLegacyPrefs.None);
            Assert.AreEqual(0f, junk.ChatBoxWidthPx);
        }

        // ---- The cursor ------------------------------------------------------------------------

        [Test]
        public void Cursor_MatchesTheEdge()
        {
            Assert.AreEqual(0, ResizeCursor.ShapeFor(ResizeEdges.Left));
            Assert.AreEqual(0, ResizeCursor.ShapeFor(ResizeEdges.Right));
            Assert.AreEqual(1, ResizeCursor.ShapeFor(ResizeEdges.Top));
            Assert.AreEqual(2, ResizeCursor.ShapeFor(ResizeEdges.TopLeft));
            Assert.AreEqual(2, ResizeCursor.ShapeFor(ResizeEdges.BottomRight));
            Assert.AreEqual(3, ResizeCursor.ShapeFor(ResizeEdges.TopRight));
            Assert.AreEqual(3, ResizeCursor.ShapeFor(ResizeEdges.BottomLeft));
            Assert.AreEqual(-1, ResizeCursor.ShapeFor(ResizeEdges.None));
        }
    }
}
