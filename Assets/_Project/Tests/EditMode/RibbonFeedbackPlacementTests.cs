using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonFeedbackPlacementTests
    {
        [Test]
        public void FeedbackFindsNearbyRoomClearOfBothChatAndPortrait()
        {
            var work = new Rect(80f, 40f, 1200f, 640f);
            var chat = new Rect(360f, 110f, 550f, 410f);
            var portrait = new Rect(920f, 100f, 285f, 440f);
            var size = new Vector2(330f, 112f);
            var preferred = new Vector2(670f, 485f);
            Assert.IsTrue(RibbonFeedbackPlacement.TryPlace(work, chat, portrait, size, preferred, 14f, out var position));
            var stack = new Rect(position, size);
            Assert.That(stack.yMin, Is.GreaterThanOrEqualTo(chat.yMax + 14f));
            Assert.IsFalse(stack.Overlaps(chat));
            Assert.IsFalse(stack.Overlaps(portrait));
            Assert.That(stack.yMax, Is.LessThanOrEqualTo(work.yMax - 14f));
        }

        [Test]
        public void FeedbackFallsBackToChatNotesWhenTheStackCannotFit()
        {
            var work = new Rect(40f, 80f, 640f, 400f);
            var chat = new Rect(50f, 95f, 610f, 370f);
            Assert.IsFalse(RibbonFeedbackPlacement.TryPlace(work, chat, default,
                new Vector2(325f, 100f), chat.center, 14f, out _));
        }
    }
}
