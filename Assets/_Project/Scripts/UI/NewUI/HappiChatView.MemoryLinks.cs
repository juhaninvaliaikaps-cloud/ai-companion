using System.Collections.Generic;
using System.Text;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.NewUI.Wheel;
using AICompanion.UI;
using TMPro;
using UnityEngine;

namespace AICompanion.NewUI
{
    public sealed partial class HappiChatView
    {
        RectTransform _memoryPreview;
        TextMeshProUGUI _memoryPreviewText;
        bool _memoryPrivate;
        int _memoryRevision = -1;
        HappiMemoryLinkInput _memoryPreviewOwner;

        void SyncMemoryLinkContext()
        {
            if (_ribbonLatest == null)
                return;
            var privateNow = _chat.PrivateNow;
            var revision = MemoryRuntime.LinkRevision;
            if (privateNow == _memoryPrivate && revision == _memoryRevision)
                return;
            var changedPolicy = privateNow != _memoryPrivate;
            _memoryPrivate = privateNow;
            _memoryRevision = revision;
            _transcript.InvalidateFormatting();
            HideMemoryPreview(_memoryPreviewOwner);
            GetComponent<HappiWheelHost>()?.CloseLinkedMemory(!changedPolicy);
        }

        string FormatMemoryLinks(string part, IReadOnlyList<ChatMemoryLink> links)
        {
            var first = -1;
            var second = -1;
            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                var at = ChatMemoryLink.UniquePosition(part, link.phrase);
                if (at < 0 || MemoryRuntime.ResolveLink(link, _memoryPrivate) == null)
                    continue;
                if (first < 0 || at < ChatMemoryLink.UniquePosition(part, links[first].phrase))
                {
                    second = first;
                    first = i;
                }
                else
                    second = i;
            }
            if (first < 0)
                return HappiUiKit.Literal(part);

            var output = new StringBuilder();
            var atEnd = 0;
            Append(first);
            if (second >= 0)
                Append(second);
            output.Append(HappiUiKit.Literal(part.Substring(atEnd)));
            return output.ToString();

            void Append(int index)
            {
                var phrase = links[index].phrase;
                var at = ChatMemoryLink.UniquePosition(part, phrase);
                if (at < atEnd)
                    return;
                output.Append(HappiUiKit.Literal(part.Substring(atEnd, at - atEnd)));
                output.Append("<link=\"").Append(index).Append("\"><color=#8eeaff><u>")
                    .Append(HappiUiKit.Literal(phrase)).Append("</u></color></link>");
                atEnd = at + phrase.Length;
            }
        }

        void BindMemoryLinks(TextMeshProUGUI text, ChatLine line)
        {
            var links = line?.MemoryLinks;
            var interactive = links != null && text.text.IndexOf("<link=", System.StringComparison.Ordinal) >= 0;
            var input = text.GetComponent<HappiMemoryLinkInput>();
            if (interactive && input == null)
                input = text.gameObject.AddComponent<HappiMemoryLinkInput>();
            if (input != null)
                input.Bind(this, text, interactive ? links : null);
        }

        internal void ShowMemoryPreview(HappiMemoryLinkInput owner, ChatMemoryLink link)
        {
            var value = MemoryRuntime.ResolveLink(link, _chat.PrivateNow);
            if (string.IsNullOrWhiteSpace(value))
            {
                HideMemoryPreview(owner);
                return;
            }
            if (_memoryPreview == null)
            {
                var background = HappiUiKit.Panel(_box, "Memory preview", RibbonMenu.Ink, false);
                _memoryPreview = background.rectTransform;
                _memoryPreviewText = HappiUiKit.Text(_memoryPreview, "Recalled memory", "", _theme.FontSmall,
                    _theme.Text, TextAlignmentOptions.TopLeft, false);
                HappiUiKit.Stretch(_memoryPreviewText.rectTransform, 10f, 8f, 10f, 8f);
            }
            _memoryPreviewOwner = owner;
            var sentenceEnd = value.IndexOf(". ", System.StringComparison.Ordinal);
            if (sentenceEnd >= 0)
                value = value.Substring(0, sentenceEnd + 1);
            if (value.Length > 180)
                value = value.Substring(0, 179) + "…";
            _memoryPreviewText.text = HappiUiKit.Literal(value);
            var width = Mathf.Min(320f * _layoutScale, _box.rect.width - 24f);
            if (width <= 20f)
            {
                HideMemoryPreview(owner);
                return;
            }
            var height = _memoryPreviewText.GetPreferredValues(_memoryPreviewText.text, width - 20f, 0f).y + 16f;
            var chatBounds = _box.rect;
            var row = RectTransformUtility.CalculateRelativeRectTransformBounds(_box, owner.transform);
            var edge = 12f * _layoutScale;
            var top = 60f * _layoutScale;
            var bottom = Mathf.Max(top, chatBounds.height - 46f * _layoutScale - height);
            var x = Mathf.Clamp(row.min.x - chatBounds.xMin, edge, Mathf.Max(edge, chatBounds.width - width - edge));
            var y = chatBounds.yMax - row.min.y + 4f * _layoutScale;
            if (y > bottom)
                y = chatBounds.yMax - row.max.y - height - 4f * _layoutScale;
            HappiUiKit.Place(_memoryPreview, x, Mathf.Clamp(y, top, bottom), width, height);
            _memoryPreview.SetAsLastSibling();
            _memoryPreview.gameObject.SetActive(true);
        }

        internal void HideMemoryPreview(HappiMemoryLinkInput owner)
        {
            if (_memoryPreviewOwner != owner)
                return;
            _memoryPreviewOwner = null;
            if (_memoryPreview != null)
                _memoryPreview.gameObject.SetActive(false);
        }

        internal void OpenMemoryLink(HappiMemoryLinkInput owner, ChatMemoryLink link)
        {
            HideMemoryPreview(owner);
            var value = MemoryRuntime.ResolveLink(link, _chat.PrivateNow);
            if (!string.IsNullOrWhiteSpace(value))
                GetComponent<HappiWheelHost>()?.OpenProfileFromRibbon(ProfilePanel.Tab.Memories,
                    () => MemoryRuntime.ResolveLink(link, _chat != null && _chat.PrivateNow));
        }
    }
}
