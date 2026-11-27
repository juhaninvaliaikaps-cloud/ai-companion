using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-231: her hobby, the activity slots and her routine.</summary>
    public sealed class RoomActivityTests
    {
        // The player somewhere out of the way: off the floor, never close to her or on a slot.
        static readonly RoomTile Far = new RoomTile(-10, -10);

        // ---- the hobby --------------------------------------------------------------------

        [Test]
        public void Hobby_ReadingKeyWins_ElseGuessFromDescription_ElsePiano()
        {
            var sheet = new CharacterSheet { roomHobby = "telescope" };
            Assert.AreEqual(RoomHobby.Telescope, RoomHobbies.For(sheet, "She lifts weights at the gym."));
            Assert.AreEqual(RoomHobby.SquatRack, RoomHobbies.For(new CharacterSheet(), "She lifts weights at the gym every morning."));
            Assert.AreEqual(RoomHobby.SquatRack, RoomHobbies.For(null, "A fitness-obsessed athlete."));
            Assert.AreEqual(RoomHobby.Easel, RoomHobbies.Guess("An art student who paints at night."));
            Assert.AreEqual(RoomHobby.Guitar, RoomHobbies.Guess("She plays guitar in a band."));
            Assert.AreEqual(RoomHobby.Plants, RoomHobbies.Guess("Her flat is full of plants and flowers."));
            Assert.AreEqual(RoomHobby.Telescope, RoomHobbies.Guess("She loves the stars and astronomy."));
            Assert.AreEqual(RoomHobby.Piano, RoomHobbies.Guess("She is shy and quiet."));
            Assert.AreEqual(RoomHobby.Piano, RoomHobbies.Guess("She wants a fresh start."), "start is not star");
            Assert.AreEqual(RoomHobby.Piano, RoomHobbies.Guess(null));
        }

        [Test]
        public void Hobby_KeysNormalize_AndBadOnesAreDropped()
        {
            Assert.AreEqual("squat_rack", RoomHobbies.Normalize(" Squat Rack "));
            Assert.AreEqual("plants", RoomHobbies.Normalize("plant"));
            Assert.AreEqual(string.Empty, RoomHobbies.Normalize("drums"));
            Assert.AreEqual("room_hobby_guitar_stand", RoomHobbies.PieceId(RoomHobby.Guitar));
            Assert.AreEqual("hobby_piano", RoomHobbies.Pose(RoomHobby.Piano));
            Assert.AreEqual(string.Empty, RoomHobbies.Pose(RoomHobby.Easel), "no pose: she stands and faces it");

            var sheet = new CharacterSheet { roomHobby = "drums" };
            sheet.Normalize();
            Assert.AreEqual(string.Empty, sheet.roomHobby);
        }

        [Test]
        public void Hobby_IsInTheReadingPrompt_AndParsed()
        {
            StringAssert.Contains("\"roomHobby\"", CharacterSheetPrompt.System);
            foreach (var key in RoomHobbies.Keys)
                StringAssert.Contains(key, CharacterSheetPrompt.System);
            var sheet = CharacterSheet.Parse("{\"roomHobby\": \"easel\"}");
            Assert.IsNotNull(sheet);
            Assert.AreEqual("easel", sheet.roomHobby);
        }

        [Test]
        public void Layout_OnlyThePianoHasABench()
        {
            Assert.IsTrue(RoomLayout.V1(RoomHobby.Piano).IsBlocked(new RoomTile(9, 7)));
            Assert.IsTrue(RoomLayout.V1(RoomHobby.Easel).IsFree(new RoomTile(9, 7)));
            Assert.IsTrue(RoomLayout.V1().IsBlocked(new RoomTile(9, 7)), "the default room has the piano");
        }

        // ---- the slots --------------------------------------------------------------------

        [Test]
        public void Slots_EveryActivityHasOne_OnAFreeTile_ReachableFromTheDoor()
        {
            foreach (RoomHobby hobby in System.Enum.GetValues(typeof(RoomHobby)))
            {
                var layout = RoomLayout.V1(hobby);
                var slots = RoomSlots.V1(layout, hobby);
                var paths = new RoomPaths(layout);
                var path = new System.Collections.Generic.List<RoomTile>();
                for (var a = 1; a < RoomRoutine.ActivityCount; a++)
                {
                    RoomSlot slot;
                    Assert.IsTrue(slots.TryGet((RoomActivity)a, out slot), hobby + " " + (RoomActivity)a);
                    Assert.IsTrue(layout.IsFree(slot.Stand), hobby + " " + slot.Activity + " stands on " + slot.Stand);
                    Assert.AreEqual(slot.Stand, paths.Find(layout.HerEntry, slot.Stand, path), slot.Activity + " is reachable");
                    Assert.IsTrue(slot.PosePoint.x > 0f && slot.PosePoint.x < 1f && slot.PosePoint.y > 0f && slot.PosePoint.y < 1f);
                }
            }
        }

        [Test]
        public void Slots_FurnitureClicksMeanActivities()
        {
            var slots = RoomSlots.V1(RoomLayout.V1(), RoomHobby.Piano);
            Assert.AreEqual(RoomActivity.Read, slots.ForPiece("room_pouf"));
            Assert.AreEqual(RoomActivity.Read, slots.ForPiece("room_bookshelf"));
            Assert.AreEqual(RoomActivity.UsePc, slots.ForPiece("room_desk_pc"));
            Assert.AreEqual(RoomActivity.Hobby, slots.ForPiece("room_hobby_piano"));
            Assert.AreEqual(RoomActivity.Hobby, slots.ForPiece("room_piano_bench"));
            Assert.AreEqual(RoomActivity.Sleep, slots.ForPiece("room_bed"));
            Assert.AreEqual(RoomActivity.None, slots.ForPiece("room_lamp"));

            var easel = RoomSlots.V1(RoomLayout.V1(RoomHobby.Easel), RoomHobby.Easel);
            Assert.AreEqual(RoomActivity.Hobby, easel.ForPiece("room_hobby_easel"));
            Assert.AreEqual(RoomActivity.None, easel.ForPiece("room_piano_bench"));
        }

        // ---- the weights ------------------------------------------------------------------

        [Test]
        public void Weights_FollowTheHourAndMood_NeverRepeatMuch_NeverSleep()
        {
            var w = new float[RoomRoutine.ActivityCount];
            RoomRoutine.Weights(8f, CompanionMood.Content, RoomActivity.None, RoomActivity.None, w);
            Assert.Greater(w[(int)RoomActivity.LookWindow], w[(int)RoomActivity.UsePc], "mornings at the window");
            RoomRoutine.Weights(13f, CompanionMood.Content, RoomActivity.None, RoomActivity.None, w);
            Assert.Greater(w[(int)RoomActivity.UsePc], w[(int)RoomActivity.LookWindow], "days at the desk");
            Assert.AreEqual(0f, w[(int)RoomActivity.Sleep]);

            var content = new float[RoomRoutine.ActivityCount];
            RoomRoutine.Weights(13f, CompanionMood.Content, RoomActivity.None, RoomActivity.None, content);
            RoomRoutine.Weights(13f, CompanionMood.Sad, RoomActivity.None, RoomActivity.None, w);
            Assert.Greater(w[(int)RoomActivity.LookWindow], content[(int)RoomActivity.LookWindow], "sad: the window");
            RoomRoutine.Weights(13f, CompanionMood.Intimate, RoomActivity.None, RoomActivity.None, w);
            CollectionAssert.AreEqual(content, w, "Intimate counts as Content in the room");

            RoomRoutine.Weights(13f, CompanionMood.Content, RoomActivity.UsePc, RoomActivity.Read, w);
            Assert.AreEqual(content[(int)RoomActivity.UsePc] * RoomRoutine.RepeatFactor, w[(int)RoomActivity.UsePc], 1e-5f);
            Assert.AreEqual(0f, w[(int)RoomActivity.Read], "the player is using it");
        }

        [Test]
        public void Pick_LandsInTheRightShare()
        {
            var w = new float[RoomRoutine.ActivityCount];
            w[(int)RoomActivity.Sit] = 1f;
            w[(int)RoomActivity.Read] = 3f;
            Assert.AreEqual(RoomActivity.Sit, RoomRoutine.Pick(w, 0.1));
            Assert.AreEqual(RoomActivity.Read, RoomRoutine.Pick(w, 0.5));
            Assert.AreEqual(RoomActivity.Read, RoomRoutine.Pick(w, 0.9999));
            Assert.AreEqual(RoomActivity.None, RoomRoutine.Pick(new float[RoomRoutine.ActivityCount], 0.5));
        }

        // ---- the routine ------------------------------------------------------------------

        static RoomRoutine Routine(out RoomSlots slots, int seed = 1)
        {
            var layout = RoomLayout.V1();
            slots = RoomSlots.V1(layout, RoomHobby.Piano);
            return new RoomRoutine(layout, slots, seed, 0f);
        }

        [Test]
        public void Routine_WalksToASlot_DoesIt_ThenGetsUp()
        {
            RoomSlots slots;
            var routine = Routine(out slots);
            RoomTile target;
            var her = new RoomTile(5, 5);
            Assert.IsFalse(routine.TryNext(1f, her, false, Far, RoomActivity.None, 13f, false, CompanionMood.Content, out target), "she waits first");

            // Keep asking until she picks an activity rather than a stroll.
            var now = 20f;
            var picked = false;
            for (var i = 0; i < 50 && !picked; i++, now += 20f)
            {
                if (routine.TryNext(now, her, false, Far, RoomActivity.None, 13f, false, CompanionMood.Content, out target))
                {
                    if (routine.Heading != RoomActivity.None)
                        picked = true;
                    else
                        her = target; // a stroll: she is there now
                }
            }

            Assert.IsTrue(picked, "she picks an activity sooner or later");
            var activity = routine.Heading;
            RoomSlot slot;
            Assert.IsTrue(slots.TryGet(activity, out slot));
            Assert.AreEqual(slot.Stand, target);

            Assert.IsFalse(routine.TryNext(now, target, true, Far, RoomActivity.None, 13f, false, CompanionMood.Content, out target), "walking");
            Assert.IsFalse(routine.TryNext(now, slot.Stand, false, Far, RoomActivity.None, 13f, false, CompanionMood.Content, out target));
            Assert.AreEqual(activity, routine.Doing, "she arrived and does it");
            Assert.AreEqual(RoomActivity.None, routine.Heading);

            routine.TryNext(now + slot.MaxSeconds + 1f, slot.Stand, false, Far, RoomActivity.None, 13f, false, CompanionMood.Content, out target);
            Assert.AreEqual(RoomActivity.None, routine.Doing, "and gets up when it's done");
        }

        [Test]
        public void Routine_AtNight_GoesToBed_StaysUntilMorning()
        {
            RoomSlots slots;
            var routine = Routine(out slots);
            RoomSlot bed;
            slots.TryGet(RoomActivity.Sleep, out bed);
            RoomTile target;
            Assert.IsTrue(routine.TryNext(1f, new RoomTile(5, 5), false, Far, RoomActivity.None, 23f, true, CompanionMood.Content, out target));
            Assert.AreEqual(bed.Stand, target);
            Assert.IsFalse(routine.TryNext(2f, bed.Stand, false, Far, RoomActivity.None, 23f, true, CompanionMood.Content, out target));
            Assert.AreEqual(RoomActivity.Sleep, routine.Doing);

            RoomTile asked;
            Assert.IsFalse(routine.Request(RoomActivity.Hobby, Far, true, out asked), "no piano at night");
            Assert.IsFalse(routine.TryNext(5000f, bed.Stand, false, Far, RoomActivity.None, 3f, true, CompanionMood.Content, out target), "still asleep");
            Assert.AreEqual(RoomActivity.Sleep, routine.Doing);

            routine.TryNext(9000f, bed.Stand, false, Far, RoomActivity.None, 7f, false, CompanionMood.Content, out target);
            Assert.AreEqual(RoomActivity.None, routine.Doing, "morning: she gets up");
        }

        [Test]
        public void Routine_Requests_AndThePlayersSpot()
        {
            RoomSlots slots;
            var routine = Routine(out slots);
            RoomSlot pouf;
            slots.TryGet(RoomActivity.Read, out pouf);

            RoomTile target;
            Assert.IsFalse(routine.Request(RoomActivity.Read, pouf.Stand, false, out target), "the player is on that spot");
            Assert.IsTrue(routine.Request(RoomActivity.Read, Far, false, out target));
            Assert.AreEqual(pouf.Stand, target);
            Assert.AreEqual(RoomActivity.Read, routine.Heading);
            Assert.IsFalse(routine.Request(RoomActivity.Sleep, Far, false, out target), "no napping on request in v1");

            // The walk ended short of the spot: she gives up on it.
            routine.TryNext(1f, new RoomTile(5, 5), false, Far, RoomActivity.None, 13f, false, CompanionMood.Content, out target);
            Assert.AreEqual(RoomActivity.None, routine.Heading);
            Assert.AreEqual(RoomActivity.None, routine.Doing);
        }

        [Test]
        public void Routine_WithThePlayerBesideHer_SheStays()
        {
            RoomSlots slots;
            var routine = Routine(out slots);
            RoomTile target;
            var her = new RoomTile(5, 5);
            for (var t = 20f; t < 400f; t += 20f)
                Assert.IsFalse(routine.TryNext(t, her, false, new RoomTile(5, 6), RoomActivity.None, 13f, false, CompanionMood.Content, out target));
        }
    }
}
