using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-239: the chat box beside her room, and her speech bubble.</summary>
    public sealed class RoomChatDockTests
    {
        [Test]
        public void FitBeside_KeepsTheRoom_ShrinksIt_OrGivesUp()
        {
            float side;
            Assert.IsTrue(RoomChatDock.FitBeside(1400f, 720f, 8f, 560f, 10f, 480f, out side));
            Assert.AreEqual(720f, side, "wide enough: the room keeps its size");

            Assert.IsTrue(RoomChatDock.FitBeside(1150f, 720f, 8f, 560f, 10f, 480f, out side));
            Assert.AreEqual(1150f - 560f - 10f - 16f, side, 1e-3f, "shrinks to make space");

            Assert.IsFalse(RoomChatDock.FitBeside(900f, 720f, 8f, 560f, 10f, 480f, out side));
            Assert.AreEqual(720f, side, "not below the minimum: they overlap instead");

            Assert.IsFalse(RoomChatDock.FitBeside(1400f, 720f, 8f, 0f, 10f, 480f, out side), "no chat box, nothing to fit");
        }

        [Test]
        public void FrameLeft_CentresThePair_OrTheRoomAlone()
        {
            var area = new Rect(0f, 0f, 1400f, 900f);
            Assert.AreEqual(700f - 368f, RoomChatDock.FrameLeft(area, 736f, 560f, 10f, false), 1e-3f);
            Assert.AreEqual(700f - (736f + 10f + 560f) * 0.5f, RoomChatDock.FrameLeft(area, 736f, 560f, 10f, true), 1e-3f);
        }

        [Test]
        public void Center_RightOfTheRoom_ThenLeft_ThenBelow_ThenOver()
        {
            var box = new Vector2(400f, 300f);
            var visible = new Rect(0f, 0f, 1600f, 1000f);

            var room = new Rect(200f, 200f, 700f, 700f);
            var c = RoomChatDock.Center(room, box, visible, 10f, 4f);
            Assert.AreEqual(room.xMax + 10f + 200f, c.x, 1e-3f, "right of the room");
            Assert.AreEqual(room.yMin + 150f, c.y, 1e-3f, "bottoms level");

            room = new Rect(1100f, 200f, 480f, 700f);
            c = RoomChatDock.Center(room, box, visible, 10f, 4f);
            Assert.AreEqual(room.xMin - 10f - 200f, c.x, 1e-3f, "no space on the right: left");

            room = new Rect(100f, 400f, 1400f, 580f);
            c = RoomChatDock.Center(room, box, visible, 10f, 4f);
            Assert.AreEqual(room.center.x, c.x, 1e-3f);
            Assert.AreEqual(room.yMin - 10f - 150f, c.y, 1e-3f, "no side space: under the room");

            room = new Rect(50f, 50f, 1500f, 940f);
            c = RoomChatDock.Center(room, box, visible, 10f, 4f);
            Assert.AreEqual(4f + 150f, c.y, 1e-3f, "no space at all: over the room's lower edge, inside the screen");
        }

        [Test]
        public void BubbleText_KeepsShortLines_CutsLongOnesAtAWord()
        {
            Assert.AreEqual("Hi there!", RoomChatDock.BubbleText("  Hi there!  ", 140));
            Assert.AreEqual(string.Empty, RoomChatDock.BubbleText(null, 140));
            var cut = RoomChatDock.BubbleText("I was thinking about the stars tonight, and the way they look over the lake.", 40);
            Assert.LessOrEqual(cut.Length, 40);
            StringAssert.EndsWith("…", cut);
            StringAssert.StartsWith("I was thinking about the stars", cut);
            StringAssert.DoesNotEndWith(",…", cut);
        }

        [Test]
        public void HerRoomNow_Say_CountsChangesOnly_AndResetClears()
        {
            HerRoomNow.Reset();
            var start = HerRoomNow.LineRevision;
            HerRoomNow.Say("Hello.");
            HerRoomNow.Say("Hello.");
            Assert.AreEqual(start + 1, HerRoomNow.LineRevision);
            Assert.AreEqual("Hello.", HerRoomNow.Line);
            HerRoomNow.IsOpen = true;
            HerRoomNow.Window = new Rect(1f, 2f, 3f, 4f);
            HerRoomNow.Reset();
            Assert.IsFalse(HerRoomNow.IsOpen);
            Assert.AreEqual(0f, HerRoomNow.Window.width);
            Assert.AreEqual(string.Empty, HerRoomNow.Line);
        }
    }
}
