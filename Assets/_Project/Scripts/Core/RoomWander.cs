using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-228: what she does in her room until phase 2 gives her activities. Now and then she
    /// strolls to another free tile; at bedtime she goes to the bed and stays there; and when
    /// the player is close she stays and turns to him. Plain logic: the view asks every frame and
    /// gets a tile only when she should start walking.
    /// </summary>
    public sealed class RoomWander
    {
        public const float MinPause = 6f;
        public const float MaxPause = 14f;

        /// <summary>Tiles (four-direction steps) within which the player counts as "with her".</summary>
        public const int CloseSteps = 2;

        /// <summary>Where she lies down at bedtime: the free tile at the foot of the bed in the v1 layout.</summary>
        public static readonly RoomTile BedSide = new RoomTile(1, 3);

        readonly RoomLayout _layout;
        readonly Random _random;
        float _nextAt;

        public RoomWander(RoomLayout layout, int seed, float now)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _random = new Random(seed);
            _nextAt = now + MinPause;
        }

        public static bool Close(RoomTile a, RoomTile b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= CloseSteps;
        }

        /// <summary>
        /// True with a tile when she should start walking now. Never while she is walking, never
        /// while the player is close, and at night only to the bed.
        /// </summary>
        public bool TryNext(float now, RoomTile her, bool walking, RoomTile player, bool night, out RoomTile target)
        {
            target = her;
            if (walking)
                return false;
            if (night)
            {
                if (her == BedSide || !_layout.IsFree(BedSide))
                    return false;
                target = BedSide;
                return true;
            }

            if (Close(her, player))
            {
                _nextAt = Math.Max(_nextAt, now + MinPause);
                return false;
            }

            if (now < _nextAt)
                return false;
            _nextAt = now + MinPause + (float)_random.NextDouble() * (MaxPause - MinPause);

            // A few tries for a free tile that is not where she stands.
            for (var i = 0; i < 12; i++)
            {
                var tile = new RoomTile(_random.Next(_layout.Columns), _random.Next(_layout.Rows));
                if (tile != her && _layout.IsFree(tile))
                {
                    target = tile;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// JUH-228: whether her room is open, for other assemblies (the desktop presence stops walking
    /// her across the screen while she is in it). Set by the room view on the main thread.
    /// </summary>
    public static class HerRoomNow
    {
        public static bool IsOpen { get; set; }
    }
}
