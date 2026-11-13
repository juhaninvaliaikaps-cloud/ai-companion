using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Where she is on screen, in window pixels from the bottom-left.</summary>
    public struct HerBody
    {
        /// <summary>Centre of her head.</summary>
        public Vector2 Head;
        /// <summary>Her feet line.</summary>
        public float FeetY;

        public float Height => Mathf.Max(80f, Head.y - FeetY);

        /// <summary>About navel height: 0.62 of the way from her feet to the centre of her head.</summary>
        public float NavelY => FeetY + Height * 0.62f;

        /// <summary>Half the width of her body with arms (beside-her spots keep clear of this).</summary>
        public float BodyHalfWidth => Height * 0.17f;

        /// <summary>Half the width of her head (beside-the-head spots keep clear of this).</summary>
        public float HeadHalfWidth => Height * 0.08f;
    }

    /// <summary>
    /// JUH-104: the chat box's snap spots around her, and free placement relative to her head.
    /// 1 = left of her head, 2 = right of her head (where the 2D portrait was; the portrait moves
    /// to her left), 3 = beside her on the left at navel height, 4 = the same on her right,
    /// 5 = under her, so she stands on top of the box (2D video avatar only: the window grows
    /// down by the box height and she is lifted onto it). For 3 and 4 the box is centred on her
    /// navel, but its bottom never goes below her feet. Pure maths; the view clamps on screen.
    /// </summary>
    public static class ChatSnapSpots
    {
        public const int Free = 0;
        public const int Default = -1;
        public const int LeftOfHead = 1;
        public const int RightOfHead = 2;
        public const int LeftBeside = 3;
        public const int RightBeside = 4;
        public const int Under = 5;
        public const int Last = Under;

        /// <summary>The default spot: under her where that is possible (2D video), else beside her on the right.</summary>
        public static int DefaultSpot(bool underAllowed)
        {
            return underAllowed ? Under : RightBeside;
        }

        /// <summary>The box centre for snap spot 1..5 (unclamped).</summary>
        public static Vector2 Spot(int spot, HerBody body, Vector2 box, float gap)
        {
            var headSide = body.HeadHalfWidth + gap + box.x * 0.5f;
            var bodySide = body.BodyHalfWidth + gap + box.x * 0.5f;
            var besideY = Mathf.Max(body.NavelY, body.FeetY + box.y * 0.5f);
            switch (spot)
            {
                case LeftOfHead:
                    return new Vector2(body.Head.x - headSide, body.Head.y);
                case RightOfHead:
                    return new Vector2(body.Head.x + headSide, body.Head.y);
                case LeftBeside:
                    return new Vector2(body.Head.x - bodySide, besideY);
                case Under:
                    return new Vector2(body.Head.x, body.FeetY - gap - box.y * 0.5f);
                default:
                    return new Vector2(body.Head.x + bodySide, besideY);
            }
        }

        /// <summary>The snap spot within <paramref name="radius"/> of a box centre, or 0. Spot 5 only when allowed.</summary>
        public static int Nearest(Vector2 center, HerBody body, Vector2 box, float gap, float radius, bool underAllowed)
        {
            return Nearest(center, body, box, gap, radius, underAllowed, new Rect(0f, 0f, 0f, 0f), 0f);
        }

        /// <summary>
        /// The same, measured to each spot as it is shown: clamped into <paramref name="visible"/>
        /// (the box can only be where it can be drawn). Spot 5 lies below the window until the box
        /// is there (the room under her is made only then), so without this it could never be reached.
        /// An empty <paramref name="visible"/> measures to the spots themselves.
        /// </summary>
        public static int Nearest(Vector2 center, HerBody body, Vector2 box, float gap, float radius, bool underAllowed, Rect visible, float margin)
        {
            var best = Free;
            var bestDistance = radius;
            for (var spot = LeftOfHead; spot <= Last; spot++)
            {
                if (spot == Under && !underAllowed)
                    continue;
                var at = Spot(spot, body, box, gap);
                if (visible.width > 0f && visible.height > 0f)
                    at = ChatBoxPlacement.Clamp(at, box, visible, margin);
                var distance = (at - center).magnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            return best;
        }

        /// <summary>The spot actually used for a saved value: the default resolved, spot 5 replaced where it is not possible.</summary>
        public static int Effective(int snap, bool underAllowed)
        {
            if (snap == Free)
                return Free;
            if (snap < LeftOfHead || snap > Last)
                return DefaultSpot(underAllowed);
            if (snap == Under && !underAllowed)
                return RightBeside;
            return snap;
        }

        /// <summary>The box centre for a saved placement (snap spot, free offset from her head, or default).</summary>
        public static Vector2 Resolve(int snap, Vector2 offset, float scale, HerBody body, Vector2 box, float gap, bool underAllowed = false)
        {
            var spot = Effective(snap, underAllowed);
            if (spot == Free)
                return body.Head + offset * scale;
            return Spot(spot, body, box, gap);
        }

        /// <summary>The offset to save for a free placement (pixels at 100% scaling).</summary>
        public static Vector2 OffsetFor(Vector2 center, HerBody body, float scale)
        {
            return (center - body.Head) / Mathf.Max(0.01f, scale);
        }

        /// <summary>The same spot on her other side (1 ↔ 2, 3 ↔ 4); 5 and free have no mirror.</summary>
        public static int Mirror(int spot)
        {
            switch (spot)
            {
                case LeftOfHead:
                    return RightOfHead;
                case RightOfHead:
                    return LeftOfHead;
                case LeftBeside:
                    return RightBeside;
                case RightBeside:
                    return LeftBeside;
                default:
                    return spot;
            }
        }

        /// <summary>
        /// The spot to use on screen: the effective one, or its mirror image when its side has no
        /// room (she stands at a screen edge) and the other side does. Free placement and spot 5
        /// are returned as is.
        /// </summary>
        public static int Fit(int snap, HerBody body, Vector2 box, float gap, Rect visible, float margin, bool underAllowed = false)
        {
            var spot = Effective(snap, underAllowed);
            if (spot == Free || spot == Under)
                return spot;
            if (Fits(Spot(spot, body, box, gap), box, visible, margin))
                return spot;
            var mirror = Mirror(spot);
            return Fits(Spot(mirror, body, box, gap), box, visible, margin) ? mirror : spot;
        }

        static bool Fits(Vector2 center, Vector2 box, Rect visible, float margin)
        {
            return center.x - box.x * 0.5f >= visible.xMin + margin - 0.5f
                && center.x + box.x * 0.5f <= visible.xMax - margin + 0.5f;
        }

        /// <summary>True when the portrait should move to her left (the box took her right side by the head).</summary>
        public static bool PortraitGoesLeft(int snap)
        {
            return snap == RightOfHead;
        }

        /// <summary>Side room the window needs on each side of her for a box this wide.</summary>
        public static int SideRoomFor(float boxWidth, float gap)
        {
            return Mathf.CeilToInt(boxWidth + gap * 2f);
        }

        /// <summary>Room the window needs below her for the box under her (spot 5).</summary>
        public static int BottomRoomFor(float boxHeight, float gap)
        {
            return Mathf.CeilToInt(boxHeight + gap * 2f);
        }
    }

    /// <summary>
    /// JUH-104: the 2D portrait docked to the chat box. It sits on the box's left or right side,
    /// scaled so its top and bottom line up with the box's top and bottom (the portrait keeps its
    /// shape). Rectangles are window pixels from the top-left, like the portrait panel uses.
    /// </summary>
    public static class PortraitDocking
    {
        public const int Auto = 0;
        public const int LeftOfBox = 1;
        public const int RightOfBox = 2;
        public const int Free = 3;

        /// <summary>The portrait's width / height (128 × 144 frame).</summary>
        public const float Aspect = 128f / 144f;

        /// <param name="boxCenter">Chat box centre, window pixels from the bottom-left.</param>
        public static Rect DockRect(Vector2 boxCenter, Vector2 boxSize, int side, float gap, float screenHeight)
        {
            var height = boxSize.y;
            var width = height * Aspect;
            var top = screenHeight - (boxCenter.y + boxSize.y * 0.5f);
            var x = side == LeftOfBox
                ? boxCenter.x - boxSize.x * 0.5f - gap - width
                : boxCenter.x + boxSize.x * 0.5f + gap;
            return new Rect(x, top, width, height);
        }

        /// <summary>The docked side to use: the chosen one, or the other when the chosen side has no room on screen.</summary>
        public static int FitSide(int side, Rect left, Rect right, float minX, float maxX)
        {
            var chosen = side == LeftOfBox ? left : right;
            if (chosen.xMin >= minX - 0.5f && chosen.xMax <= maxX + 0.5f)
                return side;
            var other = side == LeftOfBox ? right : left;
            if (other.xMin >= minX - 0.5f && other.xMax <= maxX + 0.5f)
                return side == LeftOfBox ? RightOfBox : LeftOfBox;
            return side;
        }

        /// <summary>The dock (1 left, 2 right) whose spot is within <paramref name="radius"/> of a dragged portrait's centre, or 0.</summary>
        public static int Nearest(Rect dragged, Rect left, Rect right, float radius)
        {
            var toLeft = (dragged.center - left.center).magnitude;
            var toRight = (dragged.center - right.center).magnitude;
            if (toLeft <= radius && toLeft <= toRight)
                return LeftOfBox;
            if (toRight <= radius)
                return RightOfBox;
            return Auto;
        }

        /// <summary>Keeps a top-left rectangle inside an area (also top-left based).</summary>
        public static Rect Clamp(Rect rect, Rect area)
        {
            var x = rect.width > area.width ? area.x : Mathf.Clamp(rect.x, area.xMin, area.xMax - rect.width);
            var y = rect.height > area.height ? area.y : Mathf.Clamp(rect.y, area.yMin, area.yMax - rect.height);
            return new Rect(x, y, rect.width, rect.height);
        }
    }
}
