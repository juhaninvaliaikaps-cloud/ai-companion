using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>Places the Ribbon profile within the visible overlay work area.</summary>
    public readonly struct RibbonProfileLayout
    {
        public readonly Rect Frame;
        public readonly float Scale;
        public readonly float Padding;
        public readonly float Gap;
        public readonly float HeaderHeight;
        public readonly Rect Close;
        public readonly float TabHeight;
        public readonly int TabColumns;
        public readonly float BodyTop;

        public RibbonProfileLayout(Rect workArea, Vector2 center, float dpiScale)
        {
            Scale = Mathf.Max(0.1f, Mathf.Min(Mathf.Max(1f, dpiScale), workArea.width / 380f, workArea.height / 370f));
            var margin = 8f * Scale;
            var width = Mathf.Max(1f, Mathf.Min(520f * Scale, workArea.width - 2f * margin));
            var height = Mathf.Max(1f, Mathf.Min(630f * Scale, workArea.height - 2f * margin));
            var x = Mathf.Clamp(center.x - width * 0.5f, workArea.xMin + margin, workArea.xMax - width - margin);
            var y = Mathf.Clamp(center.y - height * 0.5f, workArea.yMin + margin, workArea.yMax - height - margin);
            Frame = new Rect(x, y, width, height);
            Padding = 15f * Scale;
            Gap = 6f * Scale;
            TabHeight = 32f * Scale;
            TabColumns = width >= 430f * Scale ? 4 : 2;
            // Happi-Mood is 3:1. The illustration spans the frame without stretching or cropping.
            HeaderHeight = width / 3f;
            Close = new Rect(width - 53f * Scale, 22f * Scale, 32f * Scale, 32f * Scale);
            var rows = 4 / TabColumns;
            BodyTop = HeaderHeight + Gap + rows * (TabHeight + Gap);
        }

        public Rect Tab(int index)
        {
            var width = (Frame.width - 2f * Padding - (TabColumns - 1) * Gap) / TabColumns;
            return new Rect(Padding + index % TabColumns * (width + Gap),
                HeaderHeight + Gap + index / TabColumns * (TabHeight + Gap), width, TabHeight);
        }
    }
}
