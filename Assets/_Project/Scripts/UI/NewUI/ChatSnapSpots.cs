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
    /// to her left), 3 = beside her on the left at navel height, 4 = the same on her right. For 3
    /// and 4 the box is centred on her navel, but its bottom never goes below her feet, so a tall
    /// box sits with its bottom level with her feet. Pure maths; the view clamps the result on screen.
    /// </summary>
    public static class ChatSnapSpots
    {
        public const int Free = 0;
        public const int Default = -1;
        public const int LeftOfHead = 1;
        public const int RightOfHead = 2;
        public const int LeftBeside = 3;
        public const int RightBeside = 4;
        public const int DefaultSpot = RightBeside;

        /// <summary>The box centre for snap spot 1..4 (unclamped).</summary>
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
                default:
                    return new Vector2(body.Head.x + bodySide, besideY);
            }
        }

        /// <summary>The snap spot (1..4) within <paramref name="radius"/> of a box centre, or 0.</summary>
        public static int Nearest(Vector2 center, HerBody body, Vector2 box, float gap, float radius)
        {
            var best = Free;
            var bestDistance = radius;
            for (var spot = LeftOfHead; spot <= RightBeside; spot++)
            {
                var distance = (Spot(spot, body, box, gap) - center).magnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            return best;
        }

        /// <summary>The box centre for a saved placement (snap spot, free offset from her head, or default).</summary>
        public static Vector2 Resolve(int snap, Vector2 offset, float scale, HerBody body, Vector2 box, float gap)
        {
            if (snap >= LeftOfHead && snap <= RightBeside)
                return Spot(snap, body, box, gap);
            if (snap == Free)
                return body.Head + offset * scale;
            return Spot(DefaultSpot, body, box, gap);
        }

        /// <summary>The offset to save for a free placement (pixels at 100% scaling).</summary>
        public static Vector2 OffsetFor(Vector2 center, HerBody body, float scale)
        {
            return (center - body.Head) / Mathf.Max(0.01f, scale);
        }

        /// <summary>The same spot on her other side (1 ↔ 2, 3 ↔ 4).</summary>
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
        /// The spot to use on screen: the chosen one (default: 4), or its mirror image when the
        /// chosen side has no room (she stands at a screen edge) and the other side does. Free
        /// placement (0) is returned as is.
        /// </summary>
        public static int Fit(int snap, HerBody body, Vector2 box, float gap, Rect visible, float margin)
        {
            if (snap == Free)
                return Free;
            var spot = snap >= LeftOfHead && snap <= RightBeside ? snap : DefaultSpot;
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
    }
}
