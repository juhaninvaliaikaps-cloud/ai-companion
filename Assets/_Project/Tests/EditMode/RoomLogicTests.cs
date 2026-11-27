using System;
using System.Collections.Generic;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-226: Her Room phase 1a, the room logic.</summary>
    public sealed class RoomLogicTests
    {
        // ---- RoomLayout ---------------------------------------------------------------------

        [Test]
        public void V1_HasTheShellDraftsFloor_AndFreeDoorTiles()
        {
            var layout = RoomLayout.V1();
            Assert.AreEqual(12, layout.Columns);
            Assert.AreEqual(9, layout.Rows);
            Assert.IsTrue(layout.IsFree(layout.HerEntry));
            Assert.IsTrue(layout.IsFree(layout.PlayerEntry));
            Assert.AreEqual(layout.Rows - 1, layout.HerEntry.Y, "the door is in the front row");
            Assert.IsTrue(layout.WindowLeft < layout.WindowRight && layout.WindowBottom < layout.FloorTop, "the window is on the back wall");
        }

        [Test]
        public void V1_FurnitureIsBlocked_TheRugIsNot()
        {
            var layout = RoomLayout.V1();
            Assert.IsTrue(layout.IsBlocked(new RoomTile(1, 1)), "bed");
            Assert.IsTrue(layout.IsBlocked(new RoomTile(8, 0)), "desk");
            Assert.IsTrue(layout.IsBlocked(new RoomTile(7, 3)), "pouf");
            Assert.IsTrue(layout.IsFree(new RoomTile(5, 4)), "rug");
            Assert.IsTrue(layout.IsBlocked(new RoomTile(-1, 0)) && layout.IsBlocked(new RoomTile(12, 0)), "off the floor");
        }

        [Test]
        public void TileAndPoint_RoundTrip()
        {
            var layout = RoomLayout.V1();
            for (var y = 0; y < layout.Rows; y++)
            {
                for (var x = 0; x < layout.Columns; x++)
                {
                    RoomTile tile;
                    Assert.IsTrue(layout.TryTileAt(layout.PointOf(new RoomTile(x, y)), out tile));
                    Assert.AreEqual(new RoomTile(x, y), tile);
                }
            }

            RoomTile none;
            Assert.IsFalse(layout.TryTileAt(new Vector2(0.5f, 0.1f), out none), "the back wall is not floor");
        }

        [Test]
        public void SortOrder_FrontIsDrawnOverBack()
        {
            Assert.Greater(RoomLayout.SortOrder(0.8f), RoomLayout.SortOrder(0.4f));
        }

        // ---- RoomPaths ----------------------------------------------------------------------

        [Test]
        public void Paths_WalkAroundFurniture_InFourDirections()
        {
            var layout = new RoomLayout(5, 5, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f, new RoomTile(0, 4), new RoomTile(1, 4));
            layout.Block(2, 0, 1, 4); // a wall with a gap at the bottom
            var path = new List<RoomTile>();
            var reached = new RoomPaths(layout).Find(new RoomTile(0, 0), new RoomTile(4, 0), path);

            Assert.AreEqual(new RoomTile(4, 0), reached);
            Assert.AreEqual(new RoomTile(4, 0), path[path.Count - 1]);
            Assert.AreEqual(12, path.Count, "down 4, across 4, up 4");
            var previous = new RoomTile(0, 0);
            foreach (var step in path)
            {
                Assert.IsTrue(layout.IsFree(step), step.ToString());
                Assert.AreEqual(1, Math.Abs(step.X - previous.X) + Math.Abs(step.Y - previous.Y), "one tile at a time, no diagonals");
                previous = step;
            }
        }

        [Test]
        public void Paths_AClickOnFurniture_WalksNextToIt()
        {
            var layout = RoomLayout.V1();
            var path = new List<RoomTile>();
            var bed = new RoomTile(1, 1);
            var reached = new RoomPaths(layout).Find(layout.PlayerEntry, bed, path);
            Assert.IsTrue(layout.IsFree(reached));
            Assert.AreEqual(2, Math.Abs(reached.X - bed.X) + Math.Abs(reached.Y - bed.Y), "the closest free tile to the middle of the bed");
        }

        [Test]
        public void Paths_ToWhereYouStand_IsEmpty()
        {
            var layout = RoomLayout.V1();
            var path = new List<RoomTile> { new RoomTile(9, 9) };
            Assert.AreEqual(layout.HerEntry, new RoomPaths(layout).Find(layout.HerEntry, layout.HerEntry, path));
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void Paths_AnUnreachableTarget_GoesAsCloseAsPossible()
        {
            var layout = new RoomLayout(3, 3, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f, new RoomTile(0, 2), new RoomTile(0, 2));
            layout.Block(1, 0, 1, 3); // a full wall
            var path = new List<RoomTile>();
            var reached = new RoomPaths(layout).Find(new RoomTile(0, 2), new RoomTile(2, 0), path);
            Assert.AreEqual(new RoomTile(0, 0), reached);
        }

        // ---- RoomWalker ---------------------------------------------------------------------

        [Test]
        public void Walker_WalksAtItsSpeed_AndFacesItsWay()
        {
            var layout = RoomLayout.V1();
            var walker = new RoomWalker(layout, new RoomTile(5, 7)) { Speed = 2f };
            walker.WalkTo(new RoomTile(5, 4));
            Assert.IsTrue(walker.Walking);

            walker.Tick(0.5f);
            Assert.AreEqual(6f, walker.Y, 0.0001f, "one tile in half a second at 2 tiles per second");
            Assert.AreEqual(RoomFacing.Up, walker.Facing);

            walker.Tick(10f);
            Assert.IsFalse(walker.Walking);
            Assert.AreEqual(new RoomTile(5, 4), walker.Tile);
        }

        [Test]
        public void Walker_ArrowKeys_StepOneFreeTile_OrJustTurn()
        {
            var layout = RoomLayout.V1();
            var walker = new RoomWalker(layout, new RoomTile(6, 3));
            Assert.IsFalse(walker.Step(RoomFacing.Right), "the pouf is in the way");
            Assert.AreEqual(RoomFacing.Right, walker.Facing);

            Assert.IsTrue(walker.Step(RoomFacing.Left));
            walker.Tick(10f);
            Assert.AreEqual(new RoomTile(5, 3), walker.Tile);
        }

        [Test]
        public void Walker_ANewClickMidStep_FinishesTheStepFirst()
        {
            var layout = RoomLayout.V1();
            var walker = new RoomWalker(layout, new RoomTile(5, 7)) { Speed = 2f };
            walker.WalkTo(new RoomTile(5, 4));
            walker.Tick(0.25f); // half way to (5,6)
            walker.WalkTo(new RoomTile(8, 6));
            walker.Tick(0.25f);
            Assert.AreEqual(5f, walker.X, 0.0001f, "it finishes the step up before turning");
            Assert.AreEqual(6f, walker.Y, 0.0001f);
            walker.Tick(10f);
            Assert.AreEqual(new RoomTile(8, 6), walker.Tile);
        }

        [Test]
        public void Walker_Face_TurnsTowardsATile()
        {
            var walker = new RoomWalker(RoomLayout.V1(), new RoomTile(5, 5));
            walker.Face(new RoomTile(2, 5));
            Assert.AreEqual(RoomFacing.Left, walker.Facing);
            walker.Face(new RoomTile(5, 8));
            Assert.AreEqual(RoomFacing.Down, walker.Facing);
        }

        // ---- RoomDoor -----------------------------------------------------------------------

        [Test]
        public void Door_OpensInItsTime_AndSaysSoOnce()
        {
            var door = new RoomDoor(1.2f);
            Assert.IsTrue(door.Open());
            Assert.IsFalse(door.Open(), "already opening");
            Assert.AreEqual(RoomDoorEvent.None, door.Tick(0.6f));
            Assert.AreEqual(0.5f, door.Progress, 0.0001f);
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(RoomDoorEvent.None, door.Tick(0.1f));
            Assert.AreEqual(RoomDoorEvent.Opened, door.Tick(0.1f), "0.6 + six frames of 0.1 s is 1.2 s");
            Assert.IsTrue(door.IsOpen);
            Assert.AreEqual(RoomDoorEvent.None, door.Tick(1f));
            Assert.AreEqual(100f, door.Angle, 0.0001f);
        }

        [Test]
        public void Door_ACloseWhileOpening_TurnsAroundFromWhereItIs()
        {
            var door = new RoomDoor(1f);
            door.Open();
            door.Tick(0.4f);
            Assert.IsTrue(door.Close());
            Assert.AreEqual(RoomDoorEvent.None, door.Tick(0.2f));
            Assert.AreEqual(0.2f, door.Progress, 0.0001f);
            Assert.AreEqual(RoomDoorEvent.Closed, door.Tick(0.2f));
            Assert.IsTrue(door.IsClosed);
            Assert.AreEqual(0f, door.Angle, 0.0001f);
        }

        // ---- RoomClock ----------------------------------------------------------------------

        [Test]
        public void Clock_MiddayIsNeutral_MidnightIsBlueAndLit()
        {
            var noon = RoomClock.AtHour(12f);
            Assert.AreEqual(Color.white, noon.Tint);
            Assert.AreEqual(0f, noon.Lamp);
            Assert.IsFalse(noon.Night);

            var midnight = RoomClock.AtHour(0f);
            Assert.Less(midnight.Tint.r, 0.6f);
            Assert.Greater(midnight.Tint.b, midnight.Tint.r, "night is blue");
            Assert.AreEqual(1f, midnight.Lamp);
            Assert.IsTrue(midnight.Night);
        }

        [Test]
        public void Clock_BlendsSmoothly_WithNoJumps()
        {
            var previous = RoomClock.AtHour(0f);
            for (var minute = 1; minute <= 24 * 60; minute++)
            {
                var now = RoomClock.AtHour(minute / 60f);
                Assert.Less(Mathf.Abs(now.Tint.r - previous.Tint.r), 0.02f, "minute " + minute);
                Assert.Less(Mathf.Abs(now.Sky.b - previous.Sky.b), 0.02f, "minute " + minute);
                Assert.Less(Mathf.Abs(now.Lamp - previous.Lamp), 0.04f, "minute " + minute);
                previous = now;
            }
        }

        [TestCase(22.4f, false)]
        [TestCase(22.5f, true)]
        [TestCase(3f, true)]
        [TestCase(6.4f, true)]
        [TestCase(6.5f, false)]
        public void Clock_Bedtime(float hour, bool night)
        {
            Assert.AreEqual(night, RoomClock.AtHour(hour).Night);
        }

        [Test]
        public void Clock_UsesTheLocalTime()
        {
            var light = RoomClock.At(new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Local));
            Assert.AreEqual(Color.white, light.Tint);
        }

        // ---- RoomWindowSize -----------------------------------------------------------------

        [Test]
        public void WindowSize_IsClampedToTheWorkArea()
        {
            Assert.AreEqual(480f, RoomWindowSize.Clamp(100f, 1400f));
            Assert.AreEqual(900f, RoomWindowSize.Clamp(900f, 1400f));
            Assert.AreEqual(1400f, RoomWindowSize.Clamp(5000f, 1400f));
            Assert.AreEqual(400f, RoomWindowSize.Clamp(900f, 400f), "a tiny screen still fits");
            Assert.AreEqual(RoomWindowSize.Default, RoomWindowSize.Clamp(float.NaN, 1400f));
        }

        [Test]
        public void WindowSize_AClick_BecomesAPointOnThePicture_AtAnySize()
        {
            var layout = RoomLayout.V1();
            foreach (var side in new[] { 480f, 720f, 1300f })
            {
                // The middle of tile (5, 4) on a window at (100, 50).
                var target = layout.PointOf(new RoomTile(5, 4));
                Vector2 point;
                Assert.IsTrue(RoomWindowSize.TryToPicture(100f, 50f, side, 100f + target.x * side, 50f + target.y * side, out point));
                RoomTile tile;
                Assert.IsTrue(layout.TryTileAt(point, out tile));
                Assert.AreEqual(new RoomTile(5, 4), tile, "side " + side);
            }

            Vector2 outside;
            Assert.IsFalse(RoomWindowSize.TryToPicture(100f, 50f, 480f, 90f, 60f, out outside));
        }
    }
}
