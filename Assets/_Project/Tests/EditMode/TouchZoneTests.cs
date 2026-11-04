using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class TouchZoneTests
    {
        static TouchZoneProbe[] Zones()
        {
            return new[]
            {
                new TouchZoneProbe("head", new Vector2(0f, 100f), 40f),
                new TouchZoneProbe("hand_left", new Vector2(-60f, 0f), 30f),
                new TouchZoneProbe("hand_right", new Vector2(60f, 0f), 30f),
            };
        }

        [Test]
        public void ATouchSignalReadsLikeSomethingThatHappened()
        {
            Assert.AreEqual(TouchChatSignal.HeadText, TouchChatSignal.Prompt("head"));
            Assert.AreEqual(TouchChatSignal.HandText, TouchChatSignal.Prompt("hand_left"));
            Assert.AreEqual(TouchChatSignal.HandText, TouchChatSignal.Prompt("hand_right"));
            Assert.AreEqual(TouchChatSignal.ShoulderText, TouchChatSignal.Prompt("shoulder_right"));
            Assert.AreEqual(TouchChatSignal.GenericText, TouchChatSignal.Prompt("unknown"));
            Assert.AreEqual(TouchChatSignal.GenericText, TouchChatSignal.Prompt(null));
        }

        [Test]
        public void OnlyHandsAreHoldZones()
        {
            Assert.IsTrue(TouchChatSignal.IsHoldZone("hand_left"));
            Assert.IsTrue(TouchChatSignal.IsHoldZone("hand_right"));
            Assert.IsFalse(TouchChatSignal.IsHoldZone("head"));
            Assert.IsFalse(TouchChatSignal.IsHoldZone("shoulder_left"));
        }

        [Test]
        public void AClickInsideAZonePicksIt()
        {
            string id;
            Assert.IsTrue(TouchZonePicker.TryPick(Zones(), new Vector2(5f, 105f), out id));
            Assert.AreEqual("head", id);
        }

        [Test]
        public void TheNearestZoneWins()
        {
            // Between the two hands but closer to the right one.
            string id;
            Assert.IsTrue(TouchZonePicker.TryPick(new[]
            {
                new TouchZoneProbe("hand_left", new Vector2(-40f, 0f), 100f),
                new TouchZoneProbe("hand_right", new Vector2(40f, 0f), 100f),
            }, new Vector2(30f, 0f), out id));
            Assert.AreEqual("hand_right", id);
        }

        [Test]
        public void AClickOutsideEveryZoneHitsNothing()
        {
            string id;
            Assert.IsFalse(TouchZonePicker.TryPick(Zones(), new Vector2(500f, 500f), out id));
            Assert.IsNull(id);
        }

        [Test]
        public void AZoneWithoutARadiusIsIgnored()
        {
            string id;
            Assert.IsFalse(TouchZonePicker.TryPick(new[]
            {
                new TouchZoneProbe("broken", Vector2.zero, 0f),
            }, Vector2.zero, out id));
        }

        [Test]
        public void ACooldownBlocksTheSameZoneUntilItExpires()
        {
            var cooldown = new TouchCooldown(1.5f);

            Assert.IsTrue(cooldown.Ready("head", 0f));
            cooldown.Mark("head", 0f, 0f);
            Assert.IsFalse(cooldown.Ready("head", 0.5f));
            Assert.IsTrue(cooldown.Ready("head", 1.6f));

            // Another zone is unaffected.
            Assert.IsTrue(cooldown.Ready("hand_left", 0.5f));
        }

        [Test]
        public void AZoneCanOverrideTheCooldown()
        {
            var cooldown = new TouchCooldown(1.5f);
            cooldown.Mark("head", 0f, 4f);

            Assert.IsFalse(cooldown.Ready("head", 2f));
            Assert.IsTrue(cooldown.Ready("head", 4.1f));
            Assert.IsTrue(cooldown.Ready("other", 0.1f));
        }
    }
}
