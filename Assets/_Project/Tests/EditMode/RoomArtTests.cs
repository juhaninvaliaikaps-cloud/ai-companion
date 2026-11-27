using System.IO;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-229: the art catalogue, the picture-layer lighting, the frames and the drawing order.</summary>
    public sealed class RoomArtTests
    {
        static RoomCatalog Real()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/HerRoom/room_catalog.json");
            Assert.IsTrue(File.Exists(path), "the catalogue is in Resources/HerRoom");
            return RoomCatalog.Parse(File.ReadAllText(path));
        }

        static RoomPiece Find(System.Collections.Generic.IEnumerable<RoomPiece> pieces, string id)
        {
            foreach (var piece in pieces)
            {
                if (piece.id == id)
                    return piece;
            }

            return null;
        }

        [Test]
        public void Catalogue_DefaultRoom_HasItsOwnPieces_NotTheAlternates()
        {
            var room = Real().DefaultRoom();
            Assert.IsNotNull(Find(room, "room_bed"));
            Assert.IsNotNull(Find(room, "room_hobby_piano"));
            Assert.IsNotNull(Find(room, "room_piano_bench"));
            Assert.IsNull(Find(room, "room_bed_together"), "the shared bed is an alternate");
            Assert.IsNull(Find(room, "room_wardrobe_open"), "the open wardrobe is an alternate");
            Assert.IsNull(Find(room, "room_hobby_easel"), "other hobbies have no default spot");
            Assert.AreEqual("HerRoom/furniture/room_bed", Find(room, "room_bed").ResourcePath);
        }

        [Test]
        public void Catalogue_BrokenText_GivesAnEmptyRoom()
        {
            Assert.AreEqual(0, RoomCatalog.Parse("not json").DefaultRoom().Count);
            Assert.AreEqual(0, RoomCatalog.Parse(null).DefaultRoom().Count);
        }

        [Test]
        public void RectOf_PutsTheAnchorOnTheRoomSpot()
        {
            var bed = Find(Real().DefaultRoom(), "room_bed");
            var rect = RoomCatalog.RectOf(bed);
            Assert.AreEqual((202f - 98f) / 1024f, rect.xMin, 1e-5f);
            Assert.AreEqual((464f - 323f) / 1024f, rect.yMin, 1e-5f);
            Assert.AreEqual(197f / 1024f, rect.width, 1e-5f);
            Assert.AreEqual(338f / 1024f, rect.height, 1e-5f);
        }

        [Test]
        public void DepthOf_RugUnderWallBehind_PotOnTheWardrobe()
        {
            var room = Real().DefaultRoom();
            Assert.AreEqual(RoomCatalog.RugDepth, RoomCatalog.DepthOf(Find(room, "room_rug"), room));
            Assert.AreEqual(RoomCatalog.WallDepth, RoomCatalog.DepthOf(Find(room, "room_wall_shelf_left"), room));
            var wardrobe = RoomCatalog.DepthOf(Find(room, "room_wardrobe"), room);
            var pot = RoomCatalog.DepthOf(Find(room, "room_potted_plant"), room);
            Assert.Greater(pot, wardrobe, "the pot is drawn over the wardrobe it stands on");
            Assert.Less(pot, wardrobe + 0.01f);
        }

        [Test]
        public void FloorPieces_StandOnBlockedTiles()
        {
            var layout = RoomLayout.V1();
            foreach (var piece in Real().DefaultRoom())
            {
                if (piece.walkable || piece.OnWall || piece.OnSurface)
                    continue;
                // Just behind the front contact, inside the piece's footprint.
                var point = new Vector2(piece.defaultAnchorRoomPx[0] / 1024f, (piece.defaultAnchorRoomPx[1] - 30f) / 1024f);
                RoomTile tile;
                Assert.IsTrue(layout.TryTileAt(point, out tile), piece.id + " is on the floor");
                Assert.IsTrue(layout.IsBlocked(tile), piece.id + " blocks " + tile);
            }
        }

        [Test]
        public void Sky_DayIsUntinted_NightIsDark()
        {
            var day = RoomLighting.SkyTint(RoomClock.AtHour(12f).Sky);
            Assert.AreEqual(1f, day.r, 1e-3f);
            Assert.AreEqual(1f, day.b, 1e-3f);
            var night = RoomLighting.SkyTint(RoomClock.AtHour(2f).Sky);
            Assert.Less(night.r + night.g + night.b, 0.8f);
        }

        [Test]
        public void Shade_NoneByDay_DeepAtNight()
        {
            Assert.AreEqual(0f, RoomLighting.Shade(RoomClock.AtHour(12f).Tint), 1e-4f);
            Assert.AreEqual(RoomLighting.MaxShade, RoomLighting.Shade(RoomClock.AtHour(2f).Tint), 1e-3f);
            var dusk = RoomLighting.Shade(RoomClock.AtHour(19f).Tint);
            Assert.Greater(dusk, 0f);
            Assert.Less(dusk, RoomLighting.MaxShade);
        }

        [Test]
        public void Sunbeam_OnlyByDay_LeaningWithTheHour()
        {
            Assert.AreEqual(0f, RoomLighting.Sun(3f));
            Assert.AreEqual(1f, RoomLighting.Sun(12f));
            Assert.AreEqual(0f, RoomLighting.Sun(20f));
            Assert.Greater(RoomLighting.Sun(8f), 0f);
            Assert.Less(RoomLighting.Sun(8f), 1f);
            Assert.Greater(RoomLighting.Lean(8f), 0f, "morning: the far end leans right");
            Assert.Less(RoomLighting.Lean(17f), 0f, "evening: the far end leans left");

            Vector2 nearLeft, nearRight, farRight, farLeft;
            RoomLighting.Beam(12f, out nearLeft, out nearRight, out farRight, out farLeft);
            Assert.AreEqual(RoomLighting.WindowLeft, nearLeft.x, 1e-5f);
            Assert.AreEqual(RoomLighting.WindowRight, nearRight.x, 1e-5f);
            Assert.AreEqual(RoomLighting.FloorTop, nearLeft.y, 1e-5f);
            Assert.Greater(farLeft.y, nearLeft.y, "the beam falls toward the front of the room");
            Assert.Greater(farRight.x - farLeft.x, nearRight.x - nearLeft.x, "and fans out");
        }

        [Test]
        public void LampPool_ClearAtTheLamp_FullShadeFarAway()
        {
            Assert.AreEqual(0f, RoomLighting.PoolCover(RoomLighting.LampPool), 1e-4f);
            Assert.AreEqual(1f, RoomLighting.PoolCover(new Vector2(0.95f, 0.95f)), 1e-4f);
            Assert.AreEqual(0f, RoomLighting.Glow(0f));
            Assert.AreEqual(RoomLighting.MaxGlow, RoomLighting.Glow(1f), 1e-5f);
        }

        [Test]
        public void Frames_WalkCyclesAt8Fps_IdleBlinksThenHolds()
        {
            Assert.AreEqual(0, RoomFrames.Walk(0f));
            Assert.AreEqual(1, RoomFrames.Walk(0.13f));
            Assert.AreEqual(3, RoomFrames.Walk(0.4f));
            Assert.AreEqual(0, RoomFrames.Walk(0.5f));
            Assert.AreEqual(1, RoomFrames.Idle(0.13f), "the blink starts the cycle");
            Assert.AreEqual(0, RoomFrames.Idle(2f), "then frame 0 is held");
            Assert.AreEqual(1, RoomFrames.Idle(RoomFrames.BlinkEvery + 0.13f), "and it blinks again");
        }

        [Test]
        public void Depth_CharactersGoBetweenTheFurniture()
        {
            var furniture = new[] { 0.3f, 0.5f, 0.8f };
            var order = new int[5];

            RoomDepth.Order(furniture, 0.6f, 0.9f, order);
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 2, 4 }, order, "her behind the 0.8 piece, the player in front of all");

            RoomDepth.Order(furniture, 0.9f, 0.1f, order);
            CollectionAssert.AreEqual(new[] { 4, 0, 1, 2, 3 }, order, "the player at the back, her at the front");

            RoomDepth.Order(furniture, 0.5f, 0.5f, order);
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4, 2 }, order, "a tie with a piece puts the character in front of it");
        }
    }
}
