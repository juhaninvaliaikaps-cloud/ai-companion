using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-226: the shortest walk across her room's free floor, in four directions. A click on
    /// furniture or off the reachable floor walks to the closest free tile instead. Called on a
    /// click or a decision, not per frame; the buffers are kept between calls.
    /// </summary>
    public sealed class RoomPaths
    {
        readonly RoomLayout _layout;
        readonly int[] _distance;
        readonly int[] _parent;
        readonly Queue<int> _queue;

        static readonly int[] StepX = { 0, 0, -1, 1 };
        static readonly int[] StepY = { 1, -1, 0, 0 };

        public RoomPaths(RoomLayout layout)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            var count = layout.Columns * layout.Rows;
            _distance = new int[count];
            _parent = new int[count];
            _queue = new Queue<int>(count);
        }

        /// <summary>
        /// Fills <paramref name="path"/> with the tiles to walk, from the first step to the goal
        /// (the start is not included). Returns the tile actually reached: the target, or the
        /// free tile closest to it. Returns the start, with an empty path, when there is nowhere to go.
        /// </summary>
        public RoomTile Find(RoomTile start, RoomTile target, List<RoomTile> path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            path.Clear();
            if (!_layout.Inside(start))
                return start;

            var columns = _layout.Columns;
            for (var i = 0; i < _distance.Length; i++)
            {
                _distance[i] = -1;
                _parent[i] = -1;
            }

            var startIndex = start.Y * columns + start.X;
            _distance[startIndex] = 0;
            _queue.Clear();
            _queue.Enqueue(startIndex);

            // The goal: the target if reachable, else the reachable tile closest to it (then the nearer walk).
            var best = startIndex;
            var bestScore = Score(start, target, 0);
            while (_queue.Count > 0)
            {
                var index = _queue.Dequeue();
                var tile = new RoomTile(index % columns, index / columns);
                var score = Score(tile, target, _distance[index]);
                if (score < bestScore)
                {
                    best = index;
                    bestScore = score;
                }

                for (var d = 0; d < 4; d++)
                {
                    var next = new RoomTile(tile.X + StepX[d], tile.Y + StepY[d]);
                    if (!_layout.IsFree(next))
                        continue;
                    var nextIndex = next.Y * columns + next.X;
                    if (_distance[nextIndex] >= 0)
                        continue;
                    _distance[nextIndex] = _distance[index] + 1;
                    _parent[nextIndex] = index;
                    _queue.Enqueue(nextIndex);
                }
            }

            for (var at = best; at != startIndex && at >= 0; at = _parent[at])
                path.Add(new RoomTile(at % columns, at / columns));
            path.Reverse();
            return new RoomTile(best % columns, best / columns);
        }

        static long Score(RoomTile tile, RoomTile target, int walk)
        {
            var away = Math.Abs(tile.X - target.X) + Math.Abs(tile.Y - target.Y);
            return away * 10000L + walk;
        }
    }
}
