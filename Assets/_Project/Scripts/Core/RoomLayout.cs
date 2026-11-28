using System;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>One floor tile of her room: column X from the left, row Y from the back wall.</summary>
    [Serializable]
    public struct RoomTile : IEquatable<RoomTile>
    {
        public int X;
        public int Y;

        public RoomTile(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(RoomTile other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is RoomTile other && Equals(other);
        }

        public override int GetHashCode()
        {
            return X * 397 ^ Y;
        }

        public static bool operator ==(RoomTile a, RoomTile b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(RoomTile a, RoomTile b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + ")";
        }
    }

    /// <summary>
    /// JUH-226: the floor of her room. Points on the room picture are fractions of it, x to the
    /// right and y down from the top left, so a redrawn or resized shell only changes numbers here.
    /// The v1 numbers are measured from Codex's empty shell draft (JUH-225, 1254 × 1254 px).
    /// </summary>
    public sealed class RoomLayout
    {
        public readonly int Columns;
        public readonly int Rows;

        /// <summary>The floor's left, top, right and bottom edges, as fractions of the picture.</summary>
        public readonly float FloorLeft;
        public readonly float FloorTop;
        public readonly float FloorRight;
        public readonly float FloorBottom;

        /// <summary>The window opening, where the sky layer goes.</summary>
        public readonly float WindowLeft;
        public readonly float WindowTop;
        public readonly float WindowRight;
        public readonly float WindowBottom;

        /// <summary>The two door tiles in the front row, and where each figure appears.</summary>
        public readonly RoomTile HerEntry;
        public readonly RoomTile PlayerEntry;

        readonly bool[] _blocked;

        public RoomLayout(int columns, int rows, float floorLeft, float floorTop, float floorRight, float floorBottom,
            float windowLeft, float windowTop, float windowRight, float windowBottom, RoomTile herEntry, RoomTile playerEntry)
        {
            if (columns < 1 || rows < 1)
                throw new ArgumentException("A room needs at least one tile.");
            Columns = columns;
            Rows = rows;
            FloorLeft = floorLeft;
            FloorTop = floorTop;
            FloorRight = floorRight;
            FloorBottom = floorBottom;
            WindowLeft = windowLeft;
            WindowTop = windowTop;
            WindowRight = windowRight;
            WindowBottom = windowBottom;
            HerEntry = herEntry;
            PlayerEntry = playerEntry;
            _blocked = new bool[columns * rows];
        }

        /// <summary>
        /// The v1 room: the shell draft's 12 × 9 floor, the door in columns 5 and 6 of the front row,
        /// and the tiles the v1 furniture stands on (positions as in layout draft 01). The rug is
        /// walkable; the pouf, bed, desk, lamp, shelf, wardrobe and hobby item are not.
        /// </summary>
        public static RoomLayout V1()
        {
            return V1(RoomHobby.Piano);
        }

        /// <summary>JUH-231: the v1 room with her hobby in the hobby slot. Only the piano has a bench.</summary>
        public static RoomLayout V1(RoomHobby hobby)
        {
            var layout = new RoomLayout(12, 9,
                94f / 1254f, 296f / 1254f, 1166f / 1254f, 1139f / 1254f,
                454f / 1254f, 66f / 1254f, 800f / 1254f, 258f / 1254f,
                new RoomTile(5, 8), new RoomTile(6, 8));
            layout.Block(0, 0, 3, 3);   // bed
            layout.Block(3, 0, 1, 1);   // lamp
            layout.Block(8, 0, 3, 1);   // desk (JUH-229: matched to the v1 art)
            layout.Block(9, 1, 1, 1);   // desk chair
            layout.Block(11, 1, 1, 3);  // bookshelf
            layout.Block(8, 3, 1, 1);   // pouf
            layout.Block(0, 4, 2, 4);   // wardrobe
            layout.Block(10, 5, 2, 3);  // hobby (piano in the draft)
            if (hobby == RoomHobby.Piano)
                layout.Block(9, 7, 1, 1);   // piano bench
            return layout;
        }

        public float TileWidth => (FloorRight - FloorLeft) / Columns;
        public float TileHeight => (FloorBottom - FloorTop) / Rows;

        public bool Inside(RoomTile tile)
        {
            return tile.X >= 0 && tile.Y >= 0 && tile.X < Columns && tile.Y < Rows;
        }

        public bool IsBlocked(RoomTile tile)
        {
            return !Inside(tile) || _blocked[tile.Y * Columns + tile.X];
        }

        public bool IsFree(RoomTile tile)
        {
            return !IsBlocked(tile);
        }

        /// <summary>Marks a rectangle of tiles as furniture. Tiles outside the floor are ignored.</summary>
        public void Block(int x, int y, int width, int height, bool blocked = true)
        {
            for (var row = y; row < y + height; row++)
            {
                for (var column = x; column < x + width; column++)
                {
                    if (column >= 0 && row >= 0 && column < Columns && row < Rows)
                        _blocked[row * Columns + column] = blocked;
                }
            }
        }

        /// <summary>The centre of a tile (fractional tiles allowed, for a figure between two) on the picture.</summary>
        public Vector2 PointOf(float x, float y)
        {
            return new Vector2(FloorLeft + (x + 0.5f) * TileWidth, FloorTop + (y + 0.5f) * TileHeight);
        }

        public Vector2 PointOf(RoomTile tile)
        {
            return PointOf(tile.X, tile.Y);
        }

        /// <summary>Where a figure on this tile stands: the front edge of the tile, for the feet baseline.</summary>
        public Vector2 FeetOf(float x, float y)
        {
            return new Vector2(FloorLeft + (x + 0.5f) * TileWidth, FloorTop + (y + 0.85f) * TileHeight);
        }

        /// <summary>The tile under a point on the picture. False for a point off the floor.</summary>
        public bool TryTileAt(Vector2 point, out RoomTile tile)
        {
            tile = default(RoomTile);
            if (point.x < FloorLeft || point.x >= FloorRight || point.y < FloorTop || point.y >= FloorBottom)
                return false;
            tile = new RoomTile(
                Mathf.Clamp(Mathf.FloorToInt((point.x - FloorLeft) / TileWidth), 0, Columns - 1),
                Mathf.Clamp(Mathf.FloorToInt((point.y - FloorTop) / TileHeight), 0, Rows - 1));
            return true;
        }

        /// <summary>Back to front: a figure lower on the floor is drawn over one behind it.</summary>
        public static int SortOrder(float y)
        {
            return Mathf.RoundToInt(y * 100f);
        }
    }
}
