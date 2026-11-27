using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Places the entire animated feedback stack in visible window coordinates.</summary>
    public static class RibbonFeedbackPlacement
    {
        public static bool TryPlace(Rect work, Rect chat, Rect portrait, Vector2 size, Vector2 preferred, float gap,
            out Vector2 position)
        {
            work = Pad(work, -gap);
            chat = Pad(chat, gap);
            portrait = Pad(portrait, gap);
            var nearest = default(Vector2);
            var distance = float.PositiveInfinity;
            var cuts = work.Overlaps(chat) && chat.width > 0f ? 4 : 1;
            for (var i = 0; i < cuts; i++)
            {
                var room = cuts == 1 ? work : Outside(work, chat, i);
                if (room.width < size.x || room.height < size.y)
                    continue;
                if (room.Overlaps(portrait) && portrait.width > 0f)
                {
                    for (var j = 0; j < 4; j++)
                        Consider(Outside(room, portrait, j));
                }
                else
                    Consider(room);
            }

            position = nearest;
            return !float.IsPositiveInfinity(distance);

            void Consider(Rect room)
            {
                if (room.width < size.x || room.height < size.y)
                    return;
                var candidate = new Vector2(
                    Mathf.Clamp(preferred.x, room.xMin, room.xMax - size.x),
                    Mathf.Clamp(preferred.y, room.yMin, room.yMax - size.y));
                var score = (candidate - preferred).sqrMagnitude;
                if (score >= distance)
                    return;
                distance = score;
                nearest = candidate;
            }
        }

        static Rect Pad(Rect rect, float inset)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return default;
            return Rect.MinMaxRect(rect.xMin - inset, rect.yMin - inset, rect.xMax + inset, rect.yMax + inset);
        }

        // A rectangular stack on any side of an obstacle fits in one of these strips.
        static Rect Outside(Rect room, Rect obstacle, int side)
        {
            switch (side)
            {
                case 0:
                    return Rect.MinMaxRect(room.xMin, room.yMin, Mathf.Min(room.xMax, obstacle.xMin), room.yMax);
                case 1:
                    return Rect.MinMaxRect(Mathf.Max(room.xMin, obstacle.xMax), room.yMin, room.xMax, room.yMax);
                case 2:
                    return Rect.MinMaxRect(room.xMin, room.yMin, room.xMax, Mathf.Min(room.yMax, obstacle.yMin));
                default:
                    return Rect.MinMaxRect(room.xMin, Mathf.Max(room.yMin, obstacle.yMax), room.xMax, room.yMax);
            }
        }
    }
}
