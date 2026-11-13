using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.NewUI.Wheel;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    // JUH-104 PR D: the radial wheels.

    public sealed class RadialWheelTests
    {
        [Test]
        public void MainWheel_HasTheEightApprovedSlicesInFixedOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "talk", "watch_screen", "mood", "persona", "do_something", "leave_me_alone", "memories", "settings" },
                MainWheel.Order);
            var labels = new List<string>();
            foreach (var id in MainWheel.Order)
                labels.Add(MainWheel.Label(id, false));
            CollectionAssert.AreEqual(
                new[] { "Talk", "Watch my screen", "Mood", "Persona", "Do something", "Leave me alone", "Memories", "Settings" },
                labels);
            Assert.AreEqual("Come back", MainWheel.Label(MainWheel.LeaveMeAlone, true));
        }

        [Test]
        public void SliceZeroIsCentredOnTop_SettingsSitsTopLeftBeforeTalk()
        {
            const int count = 8;
            Assert.AreEqual(0, RadialMath.SliceAt(new Vector2(0f, 80f), count, 40f, 46f, 118f), "straight up is Talk");
            Assert.AreEqual(1, RadialMath.SliceAt(new Vector2(57f, 57f), count, 40f, 46f, 118f), "up-right is Watch my screen");
            Assert.AreEqual(2, RadialMath.SliceAt(new Vector2(80f, 0f), count, 40f, 46f, 118f), "right is Mood");
            Assert.AreEqual(4, RadialMath.SliceAt(new Vector2(0f, -80f), count, 40f, 46f, 118f), "down is Do something");
            Assert.AreEqual(7, RadialMath.SliceAt(new Vector2(-57f, 57f), count, 40f, 46f, 118f), "up-left is Settings");
        }

        [Test]
        public void SliceAt_CentreAndOutside()
        {
            Assert.AreEqual(RadialMath.Center, RadialMath.SliceAt(new Vector2(5f, 5f), 8, 40f, 46f, 118f));
            Assert.AreEqual(RadialMath.Outside, RadialMath.SliceAt(new Vector2(0f, 200f), 8, 40f, 46f, 118f));
            Assert.AreEqual(RadialMath.Outside, RadialMath.SliceAt(new Vector2(0f, 43f), 8, 40f, 46f, 118f), "the ring between the centre and the slices");
        }

        [Test]
        public void SliceAt_WorksForAnyCount()
        {
            Assert.AreEqual(0, RadialMath.SliceAt(new Vector2(0f, 50f), 3, 20f, 28f, 76f));
            Assert.AreEqual(1, RadialMath.SliceAt(new Vector2(50f, -20f), 3, 20f, 28f, 76f));
            Assert.AreEqual(2, RadialMath.SliceAt(new Vector2(-50f, -20f), 3, 20f, 28f, 76f));
            Assert.AreEqual(0, RadialMath.SliceAt(new Vector2(-50f, -20f), 1, 20f, 28f, 76f));
        }

        [Test]
        public void PlaceAbove_PutsTheWheelOverHerHead()
        {
            var center = RadialMath.PlaceAbove(new Vector2(240f, 300f), 20f, 120f, 480f, 720f, 6f);
            Assert.AreEqual(240f, center.x, 0.01f);
            Assert.AreEqual(440f, center.y, 0.01f);
        }

        [Test]
        public void PlaceAbove_StaysInsideTheWindowNearEdges()
        {
            var center = RadialMath.PlaceAbove(new Vector2(20f, 690f), 20f, 120f, 480f, 720f, 6f);
            Assert.AreEqual(126f, center.x, 0.01f, "pushed in from the left edge");
            Assert.AreEqual(594f, center.y, 0.01f, "pushed down from the top edge");
            var tiny = RadialMath.PlaceAbove(new Vector2(20f, 20f), 20f, 300f, 200f, 200f, 6f);
            Assert.AreEqual(new Vector2(100f, 100f), tiny, "a window smaller than the wheel centres it");
        }

        [Test]
        public void FitScale_NeverLetsTheWheelOutgrowTheWindow()
        {
            Assert.AreEqual(1f, RadialMath.FitScale(1f, 130f, 480f, 720f, 6f), 0.001f);
            Assert.AreEqual(468f / 260f, RadialMath.FitScale(2f, 130f, 480f, 720f, 6f), 0.001f);
        }

        [Test]
        public void PlaceBeside_PicksTheRoomierSideAndConnectorEndsOnTheRim()
        {
            var left = RadialMath.PlaceBeside(new Vector2(100f, 400f), 110f, 76f, 480f, 720f, 6f);
            Assert.Greater(left.x, 100f, "anchor on the left: wheel to the right");
            var right = RadialMath.PlaceBeside(new Vector2(400f, 400f), 110f, 76f, 480f, 720f, 6f);
            Assert.Less(right.x, 400f);
            var end = RadialMath.ConnectorEnd(new Vector2(0f, 0f), new Vector2(100f, 0f), 30f);
            Assert.AreEqual(new Vector2(70f, 0f), end);
        }

        [Test]
        public void AdultSlices_AreHiddenWhileThe18PlusGateIsClosed()
        {
            var slices = new List<WheelSlice>
            {
                new WheelSlice { Id = "say", Label = "Say something" },
                new WheelSlice { Id = "kiss", Label = "Kiss", Accent = WheelAccent.Intimate, RequiresIntimateGate = true },
                new WheelSlice { Id = "touch", Label = "Touch" },
            };
            Assert.AreEqual(2, WheelDefinition.Visible(slices, false).Count);
            Assert.AreEqual(3, WheelDefinition.Visible(slices, true).Count);
        }

        [Test]
        public void SliceGate_SimpleMoodRefusal()
        {
            Assert.IsFalse(WheelSliceGates.DefaultRule(MainWheel.DoSomething, CompanionMood.Annoyed, 0.8f, "").Enabled);
            Assert.AreEqual(WheelSliceGates.SleepyRefusal, WheelSliceGates.DefaultRule(MainWheel.DoSomething, CompanionMood.Sleepy, 0.9f, "").Reason);
            Assert.IsTrue(WheelSliceGates.DefaultRule(MainWheel.DoSomething, CompanionMood.Annoyed, 0.3f, "").Enabled, "a little annoyed still plays");
            Assert.IsTrue(WheelSliceGates.DefaultRule(MainWheel.Talk, CompanionMood.Annoyed, 1f, "").Enabled, "Talk is never refused");
        }

        [Test]
        public void SliceGate_HookCanBeReplacedAndFailsOpen()
        {
            var before = WheelSliceGates.Rule;
            try
            {
                WheelSliceGates.Rule = (id, mood, strength, persona) => persona == "brat" && id == MainWheel.Persona
                    ? new SliceGate(false, "Hmph. No.")
                    : SliceGate.Open;
                Assert.IsFalse(WheelSliceGates.Evaluate(MainWheel.Persona, CompanionMood.Content, 0f, "brat").Enabled);
                WheelSliceGates.Rule = (id, mood, strength, persona) => throw new System.Exception("bad rule");
                Assert.IsTrue(WheelSliceGates.Evaluate(MainWheel.Persona, CompanionMood.Content, 0f, "brat").Enabled);
            }
            finally
            {
                WheelSliceGates.Rule = before;
            }
        }

        sealed class FakeZones : IBodyHitZones
        {
            public BodyPart Part;
            public bool TryHit(Vector2 unityScreen, out BodyPart part, out Vector2 anchor)
            {
                part = Part;
                anchor = unityScreen + new Vector2(0f, 10f);
                return Part != BodyPart.None;
            }
        }

        [Test]
        public void BodyPartWheels_OnlyOpenWithAHitZoneAndARegisteredSet()
        {
            BodyPartWheels.Clear();
            try
            {
                Vector2 anchor;
                Assert.IsNull(BodyPartWheels.Resolve(new Vector2(10f, 10f), out anchor), "no JUH-101 zones yet: main wheel");
                BodyPartWheels.HitZones = new FakeZones { Part = BodyPart.Lips };
                Assert.IsNull(BodyPartWheels.Resolve(new Vector2(10f, 10f), out anchor), "a zone without a set: main wheel");
                BodyPartWheels.Register(BodyPart.Lips, () =>
                {
                    var lips = new WheelDefinition { CenterLabel = "Lips" };
                    lips.Slices.Add(new WheelSlice { Id = "kiss", Label = "Kiss", Accent = WheelAccent.Intimate });
                    return lips;
                });
                var wheel = BodyPartWheels.Resolve(new Vector2(10f, 10f), out anchor);
                Assert.IsNotNull(wheel);
                Assert.AreEqual("Lips", wheel.CenterLabel);
                Assert.AreEqual(new Vector2(10f, 20f), anchor);
            }
            finally
            {
                BodyPartWheels.Clear();
            }
        }

        [Test]
        public void SliceShape_CoversItsRingSegmentWithEvenGaps()
        {
            // Slice 0 of 8, centred on top, ring 46..118, 4 px gap.
            Assert.Greater(WheelSprites.InsideDistance(new Vector2(0f, 82f), 8, 46f, 118f, 4f), 25f, "middle of the slice");
            Assert.Less(WheelSprites.InsideDistance(new Vector2(0f, 30f), 8, 46f, 118f, 4f), 0f, "inside the centre hole");
            Assert.Less(WheelSprites.InsideDistance(new Vector2(0f, 130f), 8, 46f, 118f, 4f), 0f, "beyond the rim");
            Assert.Less(WheelSprites.InsideDistance(new Vector2(0f, -82f), 8, 46f, 118f, 4f), 0f, "the opposite slice");
            Assert.Less(WheelSprites.InsideDistance(new Vector2(57f, 57f), 8, 46f, 118f, 4f), 0f, "the next slice");
            // On the boundary to the next slice the gap is 4 px wide at any radius.
            foreach (var r in new[] { 50f, 80f, 115f })
            {
                var edge = RadialMath.Direction(22.5f) * r;
                Assert.AreEqual(-2f, WheelSprites.InsideDistance(edge, 8, 46f, 118f, 4f), 0.01f);
            }

            Assert.AreEqual(1f, WheelSprites.FillAlpha(3f));
            Assert.AreEqual(0f, WheelSprites.FillAlpha(-3f));
            Assert.AreEqual(0f, WheelSprites.BorderAlpha(10f, 1.5f), "the border is only along the edge");
            Assert.Greater(WheelSprites.BorderAlpha(0.5f, 1.5f), 0.9f);
            Assert.AreEqual(0f, WheelSprites.GlowAlpha(-20f, 10f), "the glow fades out");
            Assert.Greater(WheelSprites.GlowAlpha(-2f, 10f), 0.5f);
        }

        [Test]
        public void SingleSliceAndRing_HaveNoSeam()
        {
            Assert.Greater(WheelSprites.InsideDistance(new Vector2(0f, -40f), 1, 30f, 50f, 4f), 0f);
            Assert.Greater(WheelSprites.InsideDistance(new Vector2(40f, 0f), 1, 30f, 50f, 4f), 0f);
        }

        [Test]
        public void ContextMenuHook_IsOffWithoutAHandler()
        {
            var before = AvatarContextMenu.Handler;
            try
            {
                AvatarContextMenu.Handler = null;
                Assert.IsFalse(AvatarContextMenu.TryOpen(Vector2.zero), "new UI off: right-click still rotates");
                AvatarContextMenu.Handler = p => true;
                Assert.IsTrue(AvatarContextMenu.TryOpen(Vector2.zero));
                AvatarContextMenu.Handler = p => throw new System.Exception("boom");
                Assert.IsFalse(AvatarContextMenu.TryOpen(Vector2.zero), "a failing handler falls back to rotate");
            }
            finally
            {
                AvatarContextMenu.Handler = before;
            }
        }
    }
}
