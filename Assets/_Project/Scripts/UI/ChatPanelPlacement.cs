using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// Puts the panel on the side of the window that is away from the avatar,
    /// and below the face and the top debug readout.
    /// </summary>
    public static class ChatPanelPlacement
    {
        public const float Width = 220f;
        public const float Height = 340f;
        public const float Margin = 8f;
        public const float TopLimit = 124f;

        public static Rect Place(float screenWidth, float screenHeight, float avatarScreenX)
        {
            var width = Width;
            var height = Height;
            if (screenWidth < width + Margin * 2f)
                width = Mathf.Max(120f, screenWidth - Margin * 2f);

            var placeLeft = avatarScreenX > screenWidth * 0.5f;
            var x = placeLeft ? Margin : screenWidth - width - Margin;
            if (x < Margin)
                x = Margin;

            var y = screenHeight - height - Margin;
            if (y < TopLimit)
                y = TopLimit;
            if (y + height > screenHeight - Margin)
                height = Mathf.Max(120f, screenHeight - Margin - y);

            return new Rect(x, y, width, height);
        }
    }
}
