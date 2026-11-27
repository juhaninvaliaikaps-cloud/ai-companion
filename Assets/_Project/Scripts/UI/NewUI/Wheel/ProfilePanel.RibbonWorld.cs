using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.Memory;
using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    public sealed partial class ProfilePanel
    {
        const float WorldVerticalTrim = 4f;
        static readonly Color WorldCalendar = new Color32(147, 226, 244, 255);
        static readonly Color WorldBook = new Color32(216, 185, 244, 255);
        static readonly Color WorldDate = new Color32(174, 197, 218, 255);

        float WorldTextX => Pad + 47f * _scale;
        float WorldTextWidth => TextWidth - (WorldTextX - Pad);

        void BuildRibbonWorld(ProfileWorld world)
        {
            if (world.RightNow.Length > 0)
            {
                var box = BeginWorldCard("Right now", RibbonGlyph.Mark.Compass, RibbonMenu.Cyan, 42f);
                WorldLine("Current activity", world.RightNow, WorldTextX, WorldTextWidth, 20f, true);
                EndWorldCard(box, 42f);
            }

            if (world.Waiting.Count > 0)
            {
                var box = BeginWorldCard("Open threads", RibbonGlyph.Mark.Link, RibbonMint);
                foreach (var thread in world.Waiting)
                {
                    var marker = RibbonGlyph.Add(_content, "Open thread marker", RibbonGlyph.Mark.HollowDot, RibbonMint);
                    HappiUiKit.Place(marker.rectTransform, WorldTextX, _y + 4f * _scale, 12f * _scale, 12f * _scale);
                    var indent = 17f * _scale;
                    WorldLine("Open thread", thread, WorldTextX + indent, WorldTextWidth - indent);
                }
                EndWorldCard(box);
            }

            if (world.WhileAway.Count > 0)
                WorldDatedCard("While you were away", world.WhileAway, RibbonGlyph.Mark.MoodSleepy, SecretGold, false);
            if (world.Week.Count > 0)
                WorldDatedCard("Recent days", world.Week, RibbonGlyph.Mark.Calendar, WorldCalendar, true);

            if (world.Backstory.Count > 0)
            {
                var box = BeginWorldCard("Her backstory", RibbonGlyph.Mark.Book, WorldBook);
                var shown = _allBackstory ? world.Backstory.Count : 1;
                for (var i = 0; i < shown; i++)
                    WorldLine("Backstory entry", world.Backstory[i], WorldTextX, WorldTextWidth);
                if (world.Backstory.Count > 1)
                    WorldBackstoryButton();
                EndWorldCard(box);
            }
        }

        RectTransform BeginWorldCard(string title, RibbonGlyph.Mark mark, Color tint, float badgeSize = 38f)
        {
            var box = Begin();
            _y -= WorldVerticalTrim * _scale;
            CardIcon(_content, mark, tint, Pad, _y, badgeSize * _scale);
            var heading = CardText(_content, "World section", title, WorldTextX, _y, WorldTextWidth,
                24f * _scale, 16f, _theme.Text, true);
            _y += FitCardText(heading, 24f * _scale) + 2f * _scale;
            return box;
        }

        void EndWorldCard(RectTransform box, float badgeSize = 38f)
        {
            var badgeBottom = -box.anchoredPosition.y + Pad + (badgeSize - WorldVerticalTrim) * _scale;
            _y = Mathf.Max(_y, badgeBottom + Gap) - WorldVerticalTrim * _scale;
            End(box);
        }

        void WorldLine(string name, string value, float x, float width, float font = 14f, bool bold = false)
        {
            var height = (font + 6f) * _scale;
            var label = CardText(_content, name, value, x, _y, width, height, font, _theme.Text, bold);
            _y += FitCardText(label, height) + 3f * _scale;
        }

        void WorldDatedCard(string title, List<ProfileDated> entries, RibbonGlyph.Mark mark, Color tint, bool timeline)
        {
            var box = BeginWorldCard(title, mark, tint);
            var dateX = WorldTextX + (timeline ? 17f * _scale : 0f);
            var rowWidth = WorldTextWidth - (dateX - WorldTextX);
            var inline = rowWidth >= 340f * _scale;
            var dateWidth = inline ? 91f * _scale : rowWidth;
            var textX = inline ? dateX + dateWidth + 6f * _scale : dateX;
            var textWidth = rowWidth - (textX - dateX);
            RectTransform spine = null;
            var firstY = _y + 10f * _scale;
            var lastY = firstY;
            if (timeline && entries.Count > 1)
                spine = CardBlock(_content, "Timeline spine", WorldDate, WorldTextX + 2f * _scale, firstY,
                    2f * _scale, 0f).rectTransform;

            foreach (var item in entries)
            {
                var top = _y;
                var height = 20f * _scale;
                if (timeline)
                {
                    lastY = top + height * .5f;
                    CardBlock(_content, "Timeline node", WorldDate, WorldTextX, lastY - 3f * _scale,
                        6f * _scale, 6f * _scale);
                }
                var date = CardText(_content, "World date", ProfileFeelings.Capital(item.When), dateX, top, dateWidth,
                    height, 13f, WorldDate);
                var dateHeight = FitCardText(date, height);
                var entry = CardText(_content, "World event", item.Text, textX, inline ? top : top + dateHeight,
                    textWidth, height, 14f, _theme.Text);
                var entryHeight = FitCardText(entry, height);
                _y += (inline ? Mathf.Max(dateHeight, entryHeight) : dateHeight + entryHeight) + 4f * _scale;
            }
            if (spine != null)
                spine.sizeDelta = new Vector2(spine.sizeDelta.x, lastY - firstY);
            EndWorldCard(box);
        }

        void WorldBackstoryButton()
        {
            var label = _allBackstory ? "Show less" : "Show more";
            var button = HappiUiKit.Button(_content, "Backstory expansion", label, 13f * _scale, ToggleBackstory);
            StyleRibbonButton(button);
            var hover = button.Button.GetComponent<HappiHoverText>();
            button.Label.color = hover.Normal = WorldDate;
            var chevron = RibbonGlyph.Add(button.Rect, "Expansion chevron",
                _allBackstory ? RibbonGlyph.Mark.ChevronUp : RibbonGlyph.Mark.ChevronDown, WorldDate);
            chevron.Label = button.Label;
            HappiUiKit.Place(chevron.rectTransform, 84f * _scale, 6f * _scale, 16f * _scale, 16f * _scale);
            HappiUiKit.Place(button.Label.rectTransform, 6f * _scale, 0f, 76f * _scale, 28f * _scale);
            HappiUiKit.Place(button.Rect, WorldTextX, _y + 2f * _scale, 108f * _scale, 28f * _scale);
            _y += 30f * _scale + Gap;
        }

        void ToggleBackstory()
        {
            _allBackstory = !_allBackstory;
            Rebuild(_ribbon ? _content.anchoredPosition.y : 0f);
        }
    }
}
