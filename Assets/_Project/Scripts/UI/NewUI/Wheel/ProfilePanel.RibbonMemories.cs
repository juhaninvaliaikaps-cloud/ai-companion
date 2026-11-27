using TMPro;
using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    public sealed partial class ProfilePanel
    {
        static readonly Color MemoryDate = new Color32(174, 197, 218, 255);
        static readonly Color MemoryAccent = new Color32(151, 228, 242, 255);

        void BuildRibbonMemories()
        {
            var box = Begin();
            MemoryHeading("What she remembers about you", RibbonGlyph.Mark.Book, RibbonMenu.Cyan);
            var actionWidth = OpenSettings == null ? 0f : 156f * _scale;
            var summaryWidth = TextWidth - (actionWidth > 0f ? actionWidth + Gap : 0f);
            var summary = CardText(_content, "Memory summary", _data.MemoriesSummary, Pad, _y,
                summaryWidth, 26f * _scale, 13f, _theme.TextMuted);
            if (OpenSettings != null)
                MemoryManageButton(Pad + TextWidth - actionWidth, actionWidth);
            _y += FitCardText(summary, 28f * _scale) + Gap;

            foreach (var fact in _data.Memories)
            {
                var tile = CardBlock(_content, "Memory card", ProfileTrack, Pad, _y, TextWidth, 0f);
                RibbonShape.Apply(tile, RibbonShape.Kind.Panel);
                var inset = 9f * _scale;
                var mark = CardBlock(tile.transform, "Memory accent", MemoryAccent, 0f, inset, 2f * _scale, 0f);
                var label = CardText(tile.transform, "Remembered detail", fact, 2f * inset, inset,
                    TextWidth - 3f * inset, 20f * _scale, 14f, _theme.Text);
                var height = FitCardText(label, 20f * _scale);
                mark.rectTransform.sizeDelta = new Vector2(2f * _scale, height);
                tile.rectTransform.sizeDelta = new Vector2(TextWidth, height + 2f * inset);
                _y += tile.rectTransform.sizeDelta.y + 5f * _scale;
            }

            if (_data.MemoryCount > _data.Memories.Count)
                Paragraph((_data.MemoryCount - _data.Memories.Count) + " more in Manage memories.", true);
            End(box);

            box = Begin();
            MemoryHeading("Relationship timeline", RibbonGlyph.Mark.Calendar, SecretGold);
            if (_data.Timeline.Count == 0)
                Paragraph("Nothing dated yet. A session summary becomes a timeline entry when a conversation ends.", true);
            else
                BuildRibbonTimeline();
            if (_data.TimelineCount > _data.Timeline.Count)
                Paragraph("And " + (_data.TimelineCount - _data.Timeline.Count) + " earlier.", true);
            End(box);
        }

        void MemoryHeading(string title, RibbonGlyph.Mark mark, Color color)
        {
            var badgeSize = 30f * _scale;
            var indent = badgeSize + Gap;
            CardIcon(_content, mark, color, Pad, _y, badgeSize);
            var label = CardText(_content, "Memory section", title, Pad + indent, _y, TextWidth - indent,
                badgeSize, 17f, _theme.Text, true);
            _y += FitCardText(label, badgeSize) + Gap;
        }

        void MemoryManageButton(float x, float width)
        {
            var button = HappiUiKit.Button(_content, "Manage memories", "Manage memories", 12f * _scale, OpenSettings);
            StyleRibbonButton(button);
            var hover = button.Button.GetComponent<HappiHoverText>();
            button.Label.color = hover.Normal = RibbonMint;
            var arrow = RibbonGlyph.Add(button.Rect, "Manage memories chevron", RibbonGlyph.Mark.Chevron, RibbonMint);
            arrow.Label = button.Label;
            HappiUiKit.Place(button.Label.rectTransform, 7f * _scale, 0f, width - 26f * _scale, 28f * _scale);
            HappiUiKit.Place(arrow.rectTransform, width - 21f * _scale, 7f * _scale, 14f * _scale, 14f * _scale);
            HappiUiKit.Place(button.Rect, x, _y, width, 28f * _scale);
        }

        void BuildRibbonTimeline()
        {
            var metaHeight = 20f * _scale;
            var firstY = _y + metaHeight * .5f;
            var lastY = firstY;
            RectTransform spine = null;
            if (_data.Timeline.Count > 1)
                spine = CardBlock(_content, "Relationship timeline spine", MemoryDate, Pad + 2f * _scale,
                    firstY, 2f * _scale, 0f).rectTransform;

            var indent = 16f * _scale;
            foreach (var row in _data.Timeline)
            {
                lastY = _y + metaHeight * .5f;
                CardBlock(_content, "Relationship timeline node", SecretGold, Pad, lastY - 3f * _scale, 6f * _scale, 6f * _scale);
                var meta = string.IsNullOrEmpty(row.When) ? row.Kind : row.When + " · " + row.Kind;
                var header = CardText(_content, "Timeline date and kind", meta, Pad + indent, _y,
                    TextWidth - indent, metaHeight, 12f, MemoryDate, true);
                _y += FitCardText(header, metaHeight);
                var detail = CardText(_content, "Timeline event", row.Text, Pad + indent, _y,
                    TextWidth - indent, metaHeight, 14f, _theme.Text);
                _y += FitCardText(detail, metaHeight) + Gap;
            }

            if (spine != null)
                spine.sizeDelta = new Vector2(spine.sizeDelta.x, lastY - firstY);
        }
    }
}
