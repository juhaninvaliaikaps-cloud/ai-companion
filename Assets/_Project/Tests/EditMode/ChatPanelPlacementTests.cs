using AICompanion.UI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class ChatPanelPlacementTests
    {
        [Test]
        public void Place_PutsThePanelLeftOfACentredAvatar()
        {
            var column = ChatPanelPlacement.Column(240f, ChatPanelPlacement.BodyWidth, 480f, 720f);

            var rect = ChatPanelPlacement.Place(480f, 720f, column, 40f);

            Assert.Less(rect.xMax, 240f - ChatPanelPlacement.BodyWidth * 0.5f);
            Assert.GreaterOrEqual(rect.width, ChatPanelPlacement.MinWidth);
        }

        [Test]
        public void Place_KeepsTheRememberedSideWhileItStillFits()
        {
            var column = ChatPanelPlacement.Column(220f, ChatPanelPlacement.BodyWidth, 480f, 720f);

            var automatic = ChatPanelPlacement.Place(480f, 720f, column, 8f);
            var stickyLeft = ChatPanelPlacement.Place(480f, 720f, column, 8f, preferLeft: true);

            Assert.Greater(automatic.x, 220f, "Auto mode should pick the wider right gutter.");
            Assert.Less(stickyLeft.xMax, 220f, "The remembered left side should win while it fits.");
        }

        [Test]
        public void Place_SwitchesSideWhenTheRememberedSideDoesNotFit()
        {
            var column = new Rect(0f, 0f, ChatPanelPlacement.BodyWidth, 720f);

            var rect = ChatPanelPlacement.Place(480f, 720f, column, 8f, preferLeft: true);

            Assert.GreaterOrEqual(rect.x, ChatPanelPlacement.BodyWidth);
        }

        [Test]
        public void SmoothAnchor_IgnoresMovesInsideTheDeadZone()
        {
            var value = ChatPanelPlacement.SmoothAnchor(100f, 118f, 1f, true, out _, deadZone: 24f);

            Assert.AreEqual(100f, value, 0.001f);
        }

        [Test]
        public void SmoothAnchor_MovesTowardTheTargetOutsideTheDeadZone()
        {
            var value = ChatPanelPlacement.SmoothAnchor(100f, 200f, 1f / 60f, true, out _, deadZone: 24f);

            Assert.Greater(value, 100f);
            Assert.Less(value, 200f);
        }

        [Test]
        public void SmoothAnchor_ClampsTheStepToTheMaximumSpeed()
        {
            var value = ChatPanelPlacement.SmoothAnchor(100f, 1000f, 0.1f, true, out _, maxSpeed: 100f);

            Assert.AreEqual(110f, value, 0.01f);
        }
    }
}
