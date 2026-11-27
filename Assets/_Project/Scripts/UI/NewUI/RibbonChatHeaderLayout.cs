using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Top-left chatbox coordinates for the Ribbon header.</summary>
    public readonly struct RibbonChatHeaderLayout
    {
        public readonly Rect Title;
        public readonly Rect Avatar;
        public readonly Rect Privacy;
        public readonly Rect Voice;
        public readonly Rect Hide;
        public readonly Rect Options;
        public readonly Rect Divider;
        public readonly Rect Art;
        public readonly float Bottom;

        public RibbonChatHeaderLayout(float width, float pad, float scale, bool wide, bool canStack,
            bool showAvatar, bool hasArt)
        {
            var stacked = !wide && canStack && width - 2f * pad < 286f * scale;
            Bottom = (wide ? 64f : stacked ? 74f : 46f) * scale;
            var titleRow = stacked ? 38f * scale : Bottom;
            var iconSize = (wide ? 30f : 24f) * scale;
            var privacyWidth = (wide ? 87f : 63f) * scale;
            var gap = (wide ? 12f : 4f) * scale;
            var beforeDivider = wide ? 12f * scale : 0f;
            var buttonSpan = privacyWidth + 3f * iconSize + 3f * gap + beforeDivider;
            var avatarSize = showAvatar ? (wide ? 30f : 22f) * scale : 0f;
            var titleX = pad + (showAvatar ? avatarSize + 5f * scale : 0f);
            var minimumTitle = (wide ? 125f : 57f) * scale;
            var minimumControlsX = stacked ? pad : titleX + minimumTitle + gap;
            var artGap = (wide ? 12f : 6f) * scale;
            var availableArt = width - minimumControlsX - buttonSpan - artGap;
            var artWidth = hasArt ? Mathf.Min(availableArt, (wide ? 125f : 86f) * scale) : 0f;
            if (artWidth < 47f * scale)
                artWidth = 0f;

            Art = new Rect(width - artWidth, 0f, artWidth, Bottom);
            var controlEnd = artWidth > 0f ? Art.xMin - artGap : width - pad;
            var controlStart = controlEnd - buttonSpan;
            var controlCenter = stacked ? (titleRow + Bottom) * 0.5f : Bottom * 0.5f;
            var controlY = controlCenter - iconSize * 0.5f;
            Privacy = new Rect(controlStart, controlY + scale, privacyWidth, iconSize - 2f * scale);
            Voice = new Rect(Privacy.xMax + gap, controlY, iconSize, iconSize);
            Divider = wide ? new Rect(Voice.xMax + 5f * scale, controlY + 4f * scale, scale, iconSize - 8f * scale) : default;
            Hide = new Rect(Voice.xMax + gap + beforeDivider, controlY, iconSize, iconSize);
            Options = new Rect(Hide.xMax + gap, controlY, iconSize, iconSize);

            var titleEnd = stacked ? controlEnd : controlStart - gap;
            var titleHeight = (wide ? 41f : 30f) * scale;
            Title = new Rect(titleX, (titleRow - titleHeight) * 0.5f,
                Mathf.Clamp(titleEnd - titleX, 1f, (wide ? 161f : 154f) * scale), titleHeight);
            Avatar = new Rect(pad, (titleRow - avatarSize) * 0.5f, avatarSize, avatarSize);
        }
    }
}
