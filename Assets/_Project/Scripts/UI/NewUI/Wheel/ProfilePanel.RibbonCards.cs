using AICompanion.Core;
using TMPro;
using UnityEngine;

namespace AICompanion.NewUI.Wheel
{
    public sealed partial class ProfilePanel
    {
        static readonly Color ProfilePink = new Color32(255, 128, 174, 255);
        static readonly Color ProfileTrack = new Color32(21, 26, 34, 255);
        static readonly Color SecretTileBorder = new Color32(63, 75, 90, 255);

        internal bool ShowsRibbonFeelings => _ribbon && Visible && _tab == Tab.Feelings && _data?.Feelings != null;

        internal void RefreshFeelings(ProfileFeelings feelings)
        {
            _data.Feelings = feelings;
            Rebuild(_content.anchoredPosition.y);
        }

        void BuildRibbonFeelings(ProfileFeelings feelings)
        {
            var mood = feelings.CurrentMood;
            var tint = RibbonGlyph.MoodTint(mood);
            var title = ProfileFeelings.Capital(CompanionMoodNames.Name(mood));
            var box = Begin();
            var iconSize = 46f * _scale;
            var titleX = Pad + iconSize + Gap;
            CardIcon(_content, RibbonGlyph.MoodMark(mood), tint, Pad, _y, iconSize);
            CardText(_content, "Mood label", "Mood now", titleX, _y, TextWidth - iconSize - Gap, 17f * _scale, 12f, _theme.TextMuted);
            var headline = CardText(_content, "Current mood", title, titleX, _y + 15f * _scale,
                TextWidth - iconSize - Gap, 35f * _scale, 25f, _theme.Text, true);
            var headlineHeight = 15f * _scale + FitCardText(headline, 35f * _scale);
            _y += Mathf.Max(iconSize, headlineHeight) + Gap;

            foreach (var row in feelings.Moods)
            {
                if (row.Mood == CompanionMood.Intimate && !IntimateExpressions.Active)
                    continue;
                tint = RibbonGlyph.MoodTint(row.Mood);
                var rowHeight = 23f * _scale;
                var labelWidth = Mathf.Min(114f * _scale, TextWidth * 0.43f);
                var valueWidth = 43f * _scale;
                var trackX = Pad + labelWidth;
                var trackWidth = Mathf.Max(1f, TextWidth - labelWidth - valueWidth - Gap);
                CardIcon(_content, RibbonGlyph.MoodMark(row.Mood), tint, Pad, _y + 1f * _scale, 20f * _scale);
                CardText(_content, "Mood name", row.Label, Pad + 26f * _scale, _y, labelWidth - 28f * _scale,
                    rowHeight, 13f, _theme.Text);
                CardBar(trackX, _y + 9f * _scale, trackWidth, row.Fill, tint);
                CardText(_content, "Mood strength", row.Value, Pad + TextWidth - valueWidth, _y,
                    valueWidth, rowHeight, 13f, tint, true, TextAlignmentOptions.MidlineRight);
                _y += rowHeight + 3f * _scale;
            }
            _y += Gap;
            End(box);

            box = Begin();
            CardHeading("Your relationship", RibbonGlyph.Mark.Link, RibbonMenu.Cyan);
            RelationshipHeading("Affection", feelings.AffectionDescription, RibbonGlyph.Mark.Heart, ProfilePink);
            var height = 6f * _scale;
            var middle = Pad + TextWidth * .5f;
            var marker = Pad + TextWidth * Mathf.Clamp01(feelings.AffectionMarker);
            CardBlock(_content, "Affection track", ProfileTrack, Pad, _y, TextWidth, height);
            if (!Mathf.Approximately(middle, marker))
                CardBlock(_content, "Affection fill", marker >= middle ? ProfilePink : _theme.TextMuted, Mathf.Min(middle, marker), _y, Mathf.Abs(marker - middle), height);
            CardBlock(_content, "Affection midpoint", _theme.TextMuted, middle - _scale, _y - 2f * _scale, 2f * _scale, 10f * _scale);
            CardBlock(_content, "Affection marker", marker > middle ? ProfilePink : _theme.TextMuted, Mathf.Clamp(marker - 2f * _scale, Pad, Pad + TextWidth - 4f * _scale),
                _y - 3f * _scale, 4f * _scale, 12f * _scale);
            _y += height + 4f * _scale;
            var third = TextWidth / 3f;
            var legendHeight = 16f * _scale;
            CardText(_content, "Affection low", "Distant", Pad, _y, third, legendHeight, 11f, _theme.TextMuted);
            CardText(_content, "Affection middle", "Neutral", Pad + third, _y, third, legendHeight,
                11f, _theme.TextMuted, false, TextAlignmentOptions.Center);
            CardText(_content, "Affection high", "Close", Pad + 2f * third, _y, third, legendHeight,
                11f, _theme.TextMuted, false, TextAlignmentOptions.MidlineRight);
            _y += legendHeight + Gap;
            RelationshipHeading("Familiarity", feelings.FamiliarityDescription, RibbonGlyph.Mark.People, RibbonMint);
            CardBar(Pad, _y, TextWidth, feelings.FamiliarityFill, RibbonMint);
            _y += height + Gap;
            End(box);

            box = Begin();
            CardHeading("Recent changes", RibbonGlyph.Mark.Activity, RibbonMenu.Cyan);
            if (feelings.Reasons.Count == 0)
                Paragraph(feelings.ReasonsNote, true);
            for (var i = 0; i < feelings.Reasons.Count; i++)
            {
                var direction = i < feelings.ReasonDirections.Count ? feelings.ReasonDirections[i] : 0;
                var mark = direction > 0 ? RibbonGlyph.Mark.Rise : direction < 0 ? RibbonGlyph.Mark.Fall : RibbonGlyph.Mark.Activity;
                var color = direction > 0 ? (Color)RibbonMint : direction < 0 ? ProfilePink : _theme.TextMuted;
                CardIcon(_content, mark, color, Pad, _y, 20f * _scale);
                var reason = CardText(_content, "Change reason", feelings.Reasons[i], Pad + 28f * _scale, _y,
                    TextWidth - 28f * _scale, 20f * _scale, 13f, _theme.Text);
                _y += FitCardText(reason, 20f * _scale) + 4f * _scale;
            }
            End(box);
        }


        void BuildRibbonSecrets(ProfileSecrets secrets)
        {
            var box = Begin();
            CardIcon(_content, RibbonGlyph.Mark.Key, SecretGold, Pad, _y, 42f * _scale);
            var infoX = Pad + 53f * _scale;
            var infoWidth = TextWidth - (infoX - Pad);
            var title = CardText(_content, "Secrets title", "Her secrets", infoX, _y, infoWidth, 30f * _scale, 21f, _theme.Text, true);
            var titleHeight = FitCardText(title, 30f * _scale);
            if (secrets.Total == 0)
            {
                _y += Mathf.Max(46f * _scale, titleHeight + Gap);
                Paragraph("Her description has not been read yet, so she has no secrets to find.", true);
                End(box);
                return;
            }

            CardText(_content, "Discovery count", secrets.Found + " of " + secrets.Total + " discovered", infoX,
                _y + titleHeight, infoWidth, 19f * _scale, 13f, _theme.TextMuted);
            _y += titleHeight + 23f * _scale;
            var cellGap = 4f * _scale;
            var cellWidth = Mathf.Max(1f, (TextWidth - (secrets.Total - 1) * cellGap) / secrets.Total);
            for (var i = 0; i < secrets.Total; i++)
                CardBlock(_content, "Discovery segment", i < secrets.Found ? SecretGold : ProfileTrack,
                    Pad + i * (cellWidth + cellGap), _y, cellWidth, 6f * _scale);
            _y += 6f * _scale + Gap;
            End(box);

            var stings = GroupCount(secrets, SecretGroup.Stings);
            var wins = GroupCount(secrets, SecretGroup.WinsHerOver);
            var columns = stings > 0 && wins > 0 && _inner >= 430f * _scale;
            var gutter = Gap * 1.5f;
            var column = columns ? (_inner - gutter) * .5f : _inner;
            if (stings > 0)
            {
                var left = SecretCard(secrets, SecretGroup.Stings, "What stings her", RibbonGlyph.Mark.CrackedHeart, ProfilePink, 0f, column);
                if (columns)
                {
                    var right = SecretCard(secrets, SecretGroup.WinsHerOver, "What wins her over", RibbonGlyph.Mark.Heart, RibbonMint, column + gutter, column);
                    var height = Mathf.Max(left.sizeDelta.y, right.sizeDelta.y);
                    left.sizeDelta = right.sizeDelta = new Vector2(column, height);
                    _y += height + gutter;
                    return;
                }
                _y += left.sizeDelta.y + gutter;
            }

            if (wins > 0)
                _y += SecretCard(secrets, SecretGroup.WinsHerOver, "What wins her over", RibbonGlyph.Mark.Heart, RibbonMint,
                    0f, column).sizeDelta.y + gutter;
        }

        static int GroupCount(ProfileSecrets secrets, SecretGroup group)
        {
            var count = 0;
            foreach (var item in secrets.Items)
                if (item.Group == group)
                    count++;
            return count;
        }

        RectTransform SecretCard(ProfileSecrets secrets, SecretGroup group, string title, RibbonGlyph.Mark mark,
            Color color, float x, float width)
        {
            var card = HappiUiKit.Panel(_content, title, RibbonCard, false);
            RibbonShape.Apply(card, RibbonShape.Kind.Panel);
            var top = Pad;
            var tileWidth = width - 2f * Pad;
            CardIcon(card.transform, mark, color, Pad, top, 26f * _scale);
            CardText(card.transform, "Secret group title", title, Pad + 34f * _scale, top, tileWidth - 34f * _scale,
                26f * _scale, 16f, _theme.Text, true);
            top += 26f * _scale + Gap;
            foreach (var item in secrets.Items)
            {
                if (item.Group != group)
                    continue;
                top += SecretTile(card.transform, title, item, top, tileWidth) + 5f * _scale;
            }
            HappiUiKit.Place(card.rectTransform, x, _y, width, top + Pad - 5f * _scale);
            return card.rectTransform;
        }

        float SecretTile(Transform card, string title, ProfileSecret item, float y, float width)
        {
            var locked = !item.Found;
            var newest = item.Found && item.Newest;
            var text = locked ? "Undiscovered" : item.Text;
            var iconColor = locked ? _theme.TextMuted : newest ? SecretGold : (Color)RibbonMint;
            var tile = HappiUiKit.Panel(card, "Secret tile", newest ? SecretGold : SecretTileBorder, false);
            var inset = _scale;
            var face = HappiUiKit.Panel(tile.transform, "Secret tile surface", ProfileTrack, false);
            CardIcon(face.transform, locked ? RibbonGlyph.Mark.Lock : RibbonGlyph.Mark.OpenLock, iconColor,
                7f * _scale, 7f * _scale, 22f * _scale);
            var textX = 36f * _scale;
            var textWidth = width - textX - 7f * _scale - 2f * inset;
            var label = CardText(face.transform, "Secret text", text, textX, 6f * _scale, textWidth,
                22f * _scale, 13f, locked ? _theme.TextMuted : _theme.Text, newest);
            var textHeight = FitCardText(label, 22f * _scale);
            if (newest)
            {
                CardIcon(face.transform, RibbonGlyph.Mark.Spark, SecretGold, textX, textHeight + 6f * _scale, 13f * _scale);
                CardText(face.transform, "Newest discovery", "Just discovered", textX + 17f * _scale,
                    textHeight + 5f * _scale, textWidth - 17f * _scale, 15f * _scale, 11f, SecretGold, true);
                textHeight += 15f * _scale;
            }
            var height = textHeight + 13f * _scale + 2f * inset;
            HappiUiKit.Place(tile.rectTransform, Pad, y, width, height);
            HappiUiKit.Place(face.rectTransform, inset, inset, width - 2f * inset, height - 2f * inset);
            face.raycastTarget = true;
            var hover = face.gameObject.AddComponent<ProfileChipHover>();
            hover.Text = title + "\n" + (locked ? "Talk with her to uncover this." : text);
            hover.Enter = OnChipEnter;
            hover.Exit = OnChipExit;
            return height;
        }

        void CardHeading(string title, RibbonGlyph.Mark mark, Color color)
        {
            var height = 25f * _scale;
            CardIcon(_content, mark, color, Pad, _y, height);
            CardText(_content, "Card heading", title, Pad + 34f * _scale, _y, TextWidth - 34f * _scale,
                height, 17f, _theme.Text, true);
            _y += height + Gap;
        }

        void RelationshipHeading(string title, string description, RibbonGlyph.Mark mark, Color color)
        {
            var height = 22f * _scale;
            var labelWidth = Mathf.Min(TextWidth * .5f, 116f * _scale);
            CardIcon(_content, mark, color, Pad, _y, height);
            CardText(_content, "Relationship label", title, Pad + 29f * _scale, _y, labelWidth - 29f * _scale,
                height, 13f, _theme.Text, true);
            var text = CardText(_content, "Relationship description", description, Pad + labelWidth, _y,
                TextWidth - labelWidth, height, 13f, color, false, TextAlignmentOptions.MidlineRight);
            _y += FitCardText(text, height) + 4f * _scale;
        }

        void CardBar(float x, float y, float width, float fill, Color color)
        {
            var height = 6f * _scale;
            CardBlock(_content, "Gauge track", ProfileTrack, x, y, width, height);
            if (fill > 0f)
                CardBlock(_content, "Gauge fill", color, x, y, width * Mathf.Clamp01(fill), height);
        }

        void CardIcon(Transform parent, RibbonGlyph.Mark mark, Color color, float x, float y, float size)
        {
            var background = CardBlock(parent, mark + " badge", new Color(color.r, color.g, color.b, .13f), x, y, size, size);
            RibbonShape.Apply(background, RibbonShape.Kind.Panel);
            var icon = RibbonGlyph.Add(background.transform, mark + " icon", mark, color);
            var inset = size * .19f;
            HappiUiKit.Place(icon.rectTransform, inset, inset, size - 2f * inset, size - 2f * inset);
        }

        static UnityEngine.UI.Image CardBlock(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            var panel = HappiUiKit.Panel(parent, name, color, false);
            HappiUiKit.Place(panel.rectTransform, x, y, width, height);
            return panel;
        }

        TextMeshProUGUI CardText(Transform parent, string name, string text, float x, float y, float width,
            float height, float font, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var label = HappiUiKit.Text(parent, name, HappiUiKit.Literal(text), font * _scale, color, align, bold);
            HappiUiKit.Place(label.rectTransform, x, y, width, height);
            return label;
        }

        static float FitCardText(TMP_Text label, float minHeight)
        {
            var size = label.rectTransform.sizeDelta;
            var height = Mathf.Max(minHeight, label.GetPreferredValues(label.text, size.x, 0f).y);
            label.rectTransform.sizeDelta = new Vector2(size.x, height);
            return height;
        }
    }
}
