using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>JUH-231: what she can do in her room. None is standing or strolling.</summary>
    public enum RoomActivity
    {
        None = 0,
        Sit = 1,
        Read = 2,
        UsePc = 3,
        LookWindow = 4,
        Wardrobe = 5,
        Hobby = 6,
        Sleep = 7,
    }

    /// <summary>
    /// JUH-231: where an activity happens. She walks to <see cref="Stand"/>, turns to
    /// <see cref="Facing"/>, and the view draws <see cref="Pose"/> (the art pack's activity name;
    /// empty means she just stands there facing it) with its feet at <see cref="PosePoint"/>
    /// (room fraction, y down). <see cref="Piece"/> is the furniture it uses, by catalogue id.
    /// </summary>
    public struct RoomSlot
    {
        public RoomActivity Activity;
        public RoomTile Stand;
        public RoomFacing Facing;
        public Vector2 PosePoint;
        public string Pose;
        public string Piece;
        public float MinSeconds;
        public float MaxSeconds;

        /// <summary>The player can use it too (he has a sit pose; no other).</summary>
        public bool PlayerSits;
    }

    /// <summary>
    /// JUH-231: the activity spots in the v1 room. The pose points are first values from the
    /// art catalogue's anchors; phase 2b lines the poses up on the furniture by eye.
    /// </summary>
    public sealed class RoomSlots
    {
        readonly RoomSlot[] _slots;
        public readonly RoomHobby Hobby;

        RoomSlots(RoomHobby hobby, RoomSlot[] slots)
        {
            Hobby = hobby;
            _slots = slots;
        }

        public int Count => _slots.Length;
        public RoomSlot this[int index] => _slots[index];

        public static RoomSlots V1(RoomLayout layout, RoomHobby hobby)
        {
            var hobbyPose = RoomHobbies.Pose(hobby);
            var piano = hobby == RoomHobby.Piano;
            var slots = new List<RoomSlot>
            {
                Slot(RoomActivity.Sit, new RoomTile(6, 4), RoomFacing.Down, layout.FeetOf(6f, 4f), "sit", "room_rug", 20f, 45f, true),
                Slot(RoomActivity.Read, new RoomTile(8, 4), RoomFacing.Up, Px(686f, 525f), "read", "room_pouf", 40f, 90f, true),
                Slot(RoomActivity.UsePc, new RoomTile(9, 2), RoomFacing.Up, Px(756f, 410f), "use_pc", "room_desk_chair", 40f, 90f, true),
                Slot(RoomActivity.LookWindow, new RoomTile(6, 0), RoomFacing.Up, layout.FeetOf(6f, 0f), "look_window", string.Empty, 12f, 25f, false),
                Slot(RoomActivity.Wardrobe, new RoomTile(2, 5), RoomFacing.Left, layout.FeetOf(2f, 5f), "wardrobe", "room_wardrobe", 6f, 10f, false),
                piano
                    ? Slot(RoomActivity.Hobby, new RoomTile(8, 7), RoomFacing.Right, Px(795f, 805f), hobbyPose, RoomHobbies.PieceId(hobby), 30f, 70f, false)
                    : Slot(RoomActivity.Hobby, new RoomTile(9, 6), RoomFacing.Right,
                        hobby == RoomHobby.SquatRack ? Px(880f, 830f) : layout.FeetOf(9f, 6f),
                        hobbyPose, RoomHobbies.PieceId(hobby), 30f, 70f, false),
                Slot(RoomActivity.Sleep, RoomWander.BedSide, RoomFacing.Up, Px(202f, 430f), "sleep", "room_bed", 0f, 0f, false),
            };
            return new RoomSlots(hobby, slots.ToArray());
        }

        static RoomSlot Slot(RoomActivity activity, RoomTile stand, RoomFacing facing, Vector2 pose, string poseName,
            string piece, float min, float max, bool playerSits)
        {
            return new RoomSlot
            {
                Activity = activity, Stand = stand, Facing = facing, PosePoint = pose, Pose = poseName ?? string.Empty,
                Piece = piece ?? string.Empty, MinSeconds = min, MaxSeconds = max, PlayerSits = playerSits,
            };
        }

        static Vector2 Px(float x, float y)
        {
            return new Vector2(x / RoomCatalog.RoomPixels, y / RoomCatalog.RoomPixels);
        }

        public bool TryGet(RoomActivity activity, out RoomSlot slot)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Activity == activity)
                {
                    slot = _slots[i];
                    return true;
                }
            }

            slot = default(RoomSlot);
            return false;
        }

        /// <summary>
        /// The activity a click on a piece of furniture means (phase 2b): the bookshelf means
        /// reading, the desk means the PC, any hobby piece or the bench means the hobby.
        /// </summary>
        public RoomActivity ForPiece(string pieceId)
        {
            if (string.IsNullOrEmpty(pieceId))
                return RoomActivity.None;
            switch (pieceId)
            {
                case "room_bookshelf":
                    return RoomActivity.Read;
                case "room_desk_pc":
                    return RoomActivity.UsePc;
                case "room_piano_bench":
                    return Hobby == RoomHobby.Piano ? RoomActivity.Hobby : RoomActivity.None;
                case "room_wardrobe_open":
                    return RoomActivity.Wardrobe;
                case "room_bed_together":
                    return RoomActivity.Sleep;
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Piece == pieceId)
                    return _slots[i].Activity;
            }

            return RoomActivity.None;
        }
    }
}
