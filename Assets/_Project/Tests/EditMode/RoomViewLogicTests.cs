using System.IO;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-237: the logic behind phase 2b (the hobby piece, furniture clicks, the player's spot, poses).</summary>
    public sealed class RoomViewLogicTests
    {
        static RoomCatalog Real()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/HerRoom/room_catalog.json");
            return RoomCatalog.Parse(File.ReadAllText(path));
        }

        static RoomPiece In(System.Collections.Generic.List<RoomPiece> room, string id)
        {
            return room.Find(p => p.id == id);
        }

        static Vector2 Px(float x, float y)
        {
            return new Vector2(x / 1024f, y / 1024f);
        }

        [Test]
        public void RoomFor_PutsTheHobbyOnThePianosSpot_AndOnlyThePianoHasABench()
        {
            var catalog = Real();
            var piano = catalog.RoomFor(RoomHobby.Piano);
            Assert.IsNotNull(In(piano, RoomCatalog.PianoId));
            Assert.IsNotNull(In(piano, RoomCatalog.BenchId));

            var easel = catalog.RoomFor(RoomHobby.Easel);
            Assert.IsNull(In(easel, RoomCatalog.PianoId));
            Assert.IsNull(In(easel, RoomCatalog.BenchId));
            var piece = In(easel, "room_hobby_easel");
            Assert.IsNotNull(piece);
            CollectionAssert.AreEqual(In(piano, RoomCatalog.PianoId).defaultAnchorRoomPx, piece.defaultAnchorRoomPx);
            Assert.IsNull(catalog.Find("room_hobby_easel").defaultAnchorRoomPx, "the catalogue itself is not changed");
            Assert.AreEqual(piano.Count - 1, easel.Count);

            foreach (RoomHobby hobby in System.Enum.GetValues(typeof(RoomHobby)))
                Assert.IsNotNull(In(catalog.RoomFor(hobby), RoomHobbies.PieceId(hobby)), hobby.ToString());
        }

        [Test]
        public void VariantAt_TheOpenWardrobeStandsWhereTheWardrobeDoes()
        {
            var catalog = Real();
            var wardrobe = In(catalog.DefaultRoom(), "room_wardrobe");
            var open = catalog.VariantAt("room_wardrobe_open", wardrobe);
            Assert.IsNotNull(open);
            CollectionAssert.AreEqual(wardrobe.defaultAnchorRoomPx, open.defaultAnchorRoomPx);
            Assert.AreNotEqual(RoomCatalog.RectOf(wardrobe), RoomCatalog.RectOf(open), "its own size and anchor");
            Assert.IsNull(catalog.VariantAt("no_such_piece", wardrobe));
        }

        [Test]
        public void PieceAt_PicksTheFrontmostFurniture_NeverTheRugOrWalls()
        {
            var room = Real().DefaultRoom();
            Assert.AreEqual("room_pouf", RoomCatalog.PieceAt(room, Px(686f, 500f)));
            Assert.AreEqual("room_bed", RoomCatalog.PieceAt(room, Px(202f, 330f)));
            Assert.AreEqual("room_desk_chair", RoomCatalog.PieceAt(room, Px(756f, 350f)), "the chair is in front of the desk");
            Assert.AreEqual(string.Empty, RoomCatalog.PieceAt(room, Px(552f, 640f)), "the rug is floor");
            Assert.AreEqual(string.Empty, RoomCatalog.PieceAt(room, Px(500f, 900f)), "empty floor");
            Assert.AreEqual(string.Empty, RoomCatalog.PieceAt(null, Px(500f, 500f)));
        }

        [Test]
        public void PlayerUse_ArrivesOnce_AndGivesUpAWalkThatEndedElsewhere()
        {
            var layout = RoomLayout.V1();
            var slots = RoomSlots.V1(layout, RoomHobby.Piano);
            RoomSlot pouf;
            slots.TryGet(RoomActivity.Read, out pouf);
            var use = new RoomPlayerUse();
            RoomSlot slot;

            use.Plan(RoomActivity.Read);
            Assert.IsFalse(use.Arrive(slots, new RoomTile(5, 5), true, out slot), "still walking");
            Assert.IsTrue(use.Arrive(slots, pouf.Stand, false, out slot));
            Assert.AreEqual(RoomActivity.Read, use.Doing);
            Assert.AreEqual(pouf.Facing, slot.Facing);
            Assert.IsFalse(use.Arrive(slots, pouf.Stand, false, out slot), "only once");
            use.Cancel();
            Assert.AreEqual(RoomActivity.None, use.Doing);

            use.Plan(RoomActivity.Read);
            Assert.IsFalse(use.Arrive(slots, new RoomTile(5, 5), false, out slot));
            Assert.AreEqual(RoomActivity.None, use.Planned, "a walk that ended elsewhere drops the plan");
            Assert.AreEqual(RoomActivity.None, use.Doing);
        }

        [Test]
        public void Poses_BreatheAt2Fps_AndSitOnTheirPiece()
        {
            Assert.AreEqual(0, RoomFrames.Pose(0f));
            Assert.AreEqual(1, RoomFrames.Pose(0.6f));
            Assert.AreEqual(0, RoomFrames.Pose(1.1f));
            Assert.AreEqual(0.454f, RoomDepth.Pose(0.42f, 0.453f), 1e-5f, "on the bed: in front of it");
            Assert.AreEqual(0.8f, RoomDepth.Pose(0.8f, 0.5f), 1e-5f, "never behind her own feet");
        }
    }
}
