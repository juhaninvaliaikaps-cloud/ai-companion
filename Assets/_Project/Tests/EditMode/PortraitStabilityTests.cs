using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class PortraitStabilityTests
    {
        static readonly Rect NoChat = new Rect(0f, 0f, 0f, 0f);

        [Test]
        public void Beside_KeepsTheRememberedSideAcrossTheMiddle()
        {
            var placed = PortraitPlacement.Beside(
                new Vector2(260f, 400f),
                480f,
                720f,
                NoChat,
                PortraitPlacement.DebugLabel,
                preferLeft: true);

            Assert.Less(placed.xMax, 260f, "The remembered left side should win just past the middle.");
        }

        [Test]
        public void Beside_FlipsWhenTheHeadMovesClearlyPastTheOtherSide()
        {
            var placed = PortraitPlacement.Beside(
                new Vector2(200f, 400f),
                800f,
                720f,
                NoChat,
                PortraitPlacement.DebugLabel,
                preferLeft: true);

            Assert.Greater(placed.x, 200f, "Far past the middle on the other side it should flip.");
        }

        [Test]
        public void Beside_DefaultsToTheSideWithTheMostRoom()
        {
            var leftSide = PortraitPlacement.Beside(new Vector2(500f, 400f), 800f, 720f, NoChat, PortraitPlacement.DebugLabel);
            var rightSide = PortraitPlacement.Beside(new Vector2(120f, 400f), 480f, 720f, NoChat, PortraitPlacement.DebugLabel);

            Assert.Less(leftSide.xMax, 500f, "A head on the right half keeps the frame on the left.");
            Assert.Greater(rightSide.x, 120f, "A head on the left half keeps the frame on the right.");
        }

        [Test]
        public void Smooth_IgnoresMovesInsideTheDeadZone()
        {
            var current = new Rect(100f, 100f, PortraitPlacement.Width, PortraitPlacement.Height);
            var target = new Rect(118f, 108f, PortraitPlacement.Width, PortraitPlacement.Height);

            var eased = PortraitPlacement.Smooth(current, target, 0.1f, true, PortraitPlacement.DeadZone);

            Assert.AreEqual(100f, eased.x, 0.001f);
            Assert.AreEqual(100f, eased.y, 0.001f);
        }

        [Test]
        public void Smooth_FollowsMovesOutsideTheDeadZone()
        {
            var current = new Rect(100f, 100f, PortraitPlacement.Width, PortraitPlacement.Height);
            var target = new Rect(200f, 100f, PortraitPlacement.Width, PortraitPlacement.Height);

            var eased = PortraitPlacement.Smooth(current, target, 0.1f, true, PortraitPlacement.DeadZone);

            Assert.Greater(eased.x, 100f);
            Assert.Less(eased.x, 200f);
        }
    }
}
