using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-239: the chat box and her room window side by side. While the room is open the chat
    /// box docks beside the room instead of following her desktop figure. When the overlay is
    /// too narrow for both, the room shrinks (not below its minimum) to make space; failing that,
    /// the chat box sits over the room's lower edge.
    /// </summary>
    public static class RoomChatDock
    {
        /// <summary>
        /// The room's square side when the chat box must fit beside it, or the wanted side when it
        /// already fits. Returns false (and the wanted side) when even the smallest room leaves no
        /// space: then the two overlap.
        /// </summary>
        public static bool FitBeside(float areaWidth, float wantedSide, float padding, float chatWidth, float gap,
            float minSide, out float side)
        {
            side = wantedSide;
            if (chatWidth <= 0f)
                return false;
            var space = areaWidth - chatWidth - gap - 2f * padding;
            if (space >= wantedSide)
                return true;
            if (space >= minSide)
            {
                side = space;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The left edge of the room frame (y-down area coordinates) with the chat box beside it:
        /// the pair is centred together, the room on the left.
        /// </summary>
        public static float FrameLeft(Rect area, float frameWidth, float chatWidth, float gap, bool beside)
        {
            if (!beside)
                return area.center.x - frameWidth * 0.5f;
            return area.center.x - (frameWidth + gap + chatWidth) * 0.5f;
        }

        /// <summary>
        /// The chat box centre (Unity screen pixels, origin bottom-left) beside the room window:
        /// its bottom level with the room's bottom, on the side with space (right first). With no
        /// space on either side, under the room; failing that, over the room's lower edge.
        /// Always kept inside <paramref name="visible"/>.
        /// </summary>
        public static Vector2 Center(Rect room, Vector2 box, Rect visible, float gap, float margin)
        {
            var halfW = box.x * 0.5f;
            var halfH = box.y * 0.5f;
            var y = room.yMin + halfH;
            var right = room.xMax + gap + halfW;
            var left = room.xMin - gap - halfW;
            var rightFits = right + halfW <= visible.xMax - margin;
            var leftFits = left - halfW >= visible.xMin + margin;

            Vector2 center;
            if (rightFits)
                center = new Vector2(right, y);
            else if (leftFits)
                center = new Vector2(left, y);
            else
            {
                var below = room.yMin - gap - halfH;
                center = below - halfH >= visible.yMin + margin
                    ? new Vector2(room.center.x, below)
                    : new Vector2(room.center.x, visible.yMin + margin + halfH);
            }

            return Clamp(center, box, visible, margin);
        }

        /// <summary>Keeps the whole box inside the bounds (centred when the box is bigger), as the chat box does.</summary>
        static Vector2 Clamp(Vector2 center, Vector2 size, Rect bounds, float margin)
        {
            var halfW = size.x * 0.5f + margin;
            var halfH = size.y * 0.5f + margin;
            var x = bounds.width >= 2f * halfW ? Mathf.Clamp(center.x, bounds.xMin + halfW, bounds.xMax - halfW) : bounds.center.x;
            var y = bounds.height >= 2f * halfH ? Mathf.Clamp(center.y, bounds.yMin + halfH, bounds.yMax - halfH) : bounds.center.y;
            return new Vector2(x, y);
        }

        /// <summary>The bubble's text: the line as is, or cut at a word with an ellipsis when it is long.</summary>
        public static string BubbleText(string line, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;
            var text = line.Trim();
            if (text.Length <= maxChars)
                return text;
            var cut = text.LastIndexOf(' ', Mathf.Max(0, maxChars - 1));
            if (cut < maxChars / 2)
                cut = maxChars - 1;
            return text.Substring(0, cut).TrimEnd(',', ';', ':', ' ') + "…";
        }
    }
}
