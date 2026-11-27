using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>Which way a figure in her room faces: picks the walk and idle animation row.</summary>
    public enum RoomFacing
    {
        Down = 0,
        Up = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// JUH-226: one figure (her or the player) moving across her room. A click walks the shortest
    /// way; an arrow key steps one free tile. Position is in tiles (fractional while walking).
    /// <see cref="Tick"/> does not allocate.
    /// </summary>
    public sealed class RoomWalker
    {
        /// <summary>Walking speed in tiles per second.</summary>
        public const float DefaultSpeed = 2.6f;

        readonly RoomLayout _layout;
        readonly RoomPaths _paths;
        readonly List<RoomTile> _path = new List<RoomTile>(32);
        int _next;

        public float X { get; private set; }
        public float Y { get; private set; }
        public RoomFacing Facing { get; private set; }
        public float Speed { get; set; }

        public RoomWalker(RoomLayout layout, RoomTile start, RoomFacing facing = RoomFacing.Down)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _paths = new RoomPaths(layout);
            Speed = DefaultSpeed;
            Place(start, facing);
        }

        public bool Walking => _next < _path.Count;

        /// <summary>The tile the figure is on (or nearest to, while walking).</summary>
        public RoomTile Tile => new RoomTile(Mathf.RoundToInt(X), Mathf.RoundToInt(Y));

        /// <summary>Where the walk ends, or the current tile when standing.</summary>
        public RoomTile Destination => Walking ? _path[_path.Count - 1] : Tile;

        public void Place(RoomTile tile, RoomFacing facing)
        {
            _path.Clear();
            _next = 0;
            X = tile.X;
            Y = tile.Y;
            Facing = facing;
        }

        /// <summary>Walks to the tile, or to the closest free tile to it. Returns the tile it will reach.</summary>
        public RoomTile WalkTo(RoomTile target)
        {
            // In the middle of a step, finish it first, so the figure never jumps.
            var midStep = Walking;
            var from = midStep ? _path[_next] : Tile;
            var reached = _paths.Find(from, target, _path);
            if (midStep)
                _path.Insert(0, from);
            _next = 0;
            return reached;
        }

        /// <summary>An arrow key: one tile that way if it is free. Turns to face it either way.</summary>
        public bool Step(RoomFacing direction)
        {
            if (Walking)
                return false;
            Facing = direction;
            var tile = Tile;
            var next = new RoomTile(tile.X + Dx(direction), tile.Y + Dy(direction));
            if (!_layout.IsFree(next))
                return false;
            _path.Clear();
            _path.Add(next);
            _next = 0;
            return true;
        }

        public void Stop()
        {
            var tile = Tile;
            Place(tile, Facing);
        }

        /// <summary>Turns to face a tile (for example the other figure), without moving.</summary>
        public void Face(RoomTile tile)
        {
            if (Walking)
                return;
            Facing = FacingFrom(tile.X - X, tile.Y - Y, Facing);
        }

        /// <summary>Moves along the walk. Returns true while still walking.</summary>
        public bool Tick(float deltaSeconds)
        {
            var budget = Mathf.Max(0f, deltaSeconds) * Speed;
            while (budget > 0f && _next < _path.Count)
            {
                var target = _path[_next];
                var dx = target.X - X;
                var dy = target.Y - Y;
                var distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                Facing = FacingFrom(dx, dy, Facing);
                if (distance <= budget)
                {
                    X = target.X;
                    Y = target.Y;
                    budget -= distance;
                    _next++;
                }
                else
                {
                    X += Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), budget);
                    Y += Mathf.Sign(dy) * Mathf.Min(Mathf.Abs(dy), budget);
                    budget = 0f;
                }
            }

            if (_next >= _path.Count && _path.Count > 0)
            {
                _path.Clear();
                _next = 0;
            }

            return Walking;
        }

        public static RoomFacing FacingFrom(float dx, float dy, RoomFacing current)
        {
            if (Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dy) < 0.0001f)
                return current;
            if (Mathf.Abs(dx) > Mathf.Abs(dy))
                return dx < 0f ? RoomFacing.Left : RoomFacing.Right;
            return dy < 0f ? RoomFacing.Up : RoomFacing.Down;
        }

        static int Dx(RoomFacing facing)
        {
            return facing == RoomFacing.Left ? -1 : facing == RoomFacing.Right ? 1 : 0;
        }

        static int Dy(RoomFacing facing)
        {
            return facing == RoomFacing.Up ? -1 : facing == RoomFacing.Down ? 1 : 0;
        }
    }
}
