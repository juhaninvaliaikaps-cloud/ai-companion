using System;
using System.Collections.Generic;
using AICompanion.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>The chat header's illustrated, upward-opening persona picker.</summary>
    sealed class RibbonPersonaMenu
    {
        readonly RectTransform _viewport;
        readonly RectTransform _banner;
        readonly RawImage _art;
        readonly RawImage _curl;
        readonly HappiButton[] _quick = new HappiButton[RibbonPersonaSession.MaxChoices];
        readonly RibbonGlyph[] _checks = new RibbonGlyph[RibbonPersonaSession.MaxChoices];
        readonly HappiButton _create;
        readonly HappiButton _all;
        readonly TMP_Text _status;
        readonly RectTransform _focusMark;
        IReadOnlyList<PersonaCard> _choices = Array.Empty<PersonaCard>();
        bool? _busy;
        float _openedAt;

        public bool IsOpen => _viewport.gameObject.activeSelf;
        public bool OpensUp { get; private set; } = true;
        public RectTransform Viewport => _viewport;

        public RibbonPersonaMenu(Transform parent, Action<string> choose, Action create, Action all)
        {
            _viewport = HappiUiKit.Rect(parent, "Persona picker");
            _viewport.gameObject.AddComponent<RectMask2D>();
            _banner = HappiUiKit.Panel(_viewport, "Persona banner hit area", Color.clear, true).rectTransform;
            var paper = HappiUiKit.Panel(_banner, "Yellow persona paper", new Color32(255, 202, 34, 255), false);
            HappiUiKit.Stretch(paper.rectTransform, 0f, 0f, 0f, 0f);
            RibbonShape.Apply(paper, RibbonShape.Kind.ScrollPaper);
            paper.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _art = HappiUiKit.Rect(paper.transform, "Persona painting").gameObject.AddComponent<RibbonScrollArt>();
            _art.texture = Resources.Load<Texture2D>("RibbonArt/Happi-Playful");
            _art.raycastTarget = false;
            var finish = HappiUiKit.Rect(paper.transform, "Painted edge").gameObject.AddComponent<RibbonScrollFinish>();
            finish.raycastTarget = false;
            HappiUiKit.Stretch(finish.rectTransform, 0f, 0f, 0f, 0f);

            _curl = HappiUiKit.Rect(_banner, "Horizontal scroll curl").gameObject.AddComponent<RawImage>();
            _curl.texture = Resources.Load<Texture2D>("RibbonArt/ScrollRoll");
            _curl.enabled = _curl.texture != null;
            _curl.raycastTarget = false;
            _curl.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var shadow = _curl.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(41, 24, 8, 180);
            shadow.effectDistance = new Vector2(2f, -1f);
            for (var i = 0; i < _quick.Length; i++)
            {
                var index = i;
                _quick[i] = Button("Quick persona", string.Empty, () => choose(_choices[index].Id));
                _checks[i] = RibbonGlyph.Add(_quick[i].Rect, "Active persona", RibbonGlyph.Mark.Check, Color.white);
                _checks[i].Label = _quick[i].Label;
            }
            _create = Button("Persona Studio", "Create persona", create);
            _all = Button("Persona list", "All personas", all);
            _status = HappiUiKit.Text(_banner, "Switch status or full persona name", string.Empty, 11f, RibbonMenu.Ink,
                TextAlignmentOptions.MidlineLeft, true);
            _status.textWrappingMode = TextWrappingModes.NoWrap;
            _focusMark = HappiUiKit.Panel(_banner, "Persona keyboard focus", RibbonMenu.Cyan, false).rectTransform;
            _focusMark.gameObject.SetActive(false);
            _viewport.gameObject.SetActive(false);
        }

        HappiButton Button(string name, string text, Action action)
        {
            var button = HappiUiKit.Button(_banner, name, text, 14f, action);
            RibbonShape.Apply(button.Image, true);
            button.Label.alignment = TextAlignmentOptions.MidlineLeft;
            var colors = button.Button.colors;
            colors.normalColor = RibbonMenu.Ink;
            colors.highlightedColor = RibbonMenu.Cyan;
            colors.pressedColor = RibbonMenu.Cyan;
            colors.selectedColor = new Color32(34, 72, 83, 255);
            colors.disabledColor = new Color32(58, 47, 30, 255);
            button.Button.colors = colors;
            var hover = button.Button.GetComponent<HappiHoverText>();
            hover.Hover = RibbonMenu.Ink;
            hover.Disabled = new Color32(220, 210, 185, 255);
            return button;
        }

        public void Open(IReadOnlyList<PersonaCard> choices, bool busy)
        {
            _choices = choices;
            _busy = null;
            for (var i = 0; i < _quick.Length; i++)
            {
                var visible = i < choices.Count;
                _quick[i].SetVisible(visible);
                if (!visible)
                    continue;
                _quick[i].Text = HappiUiKit.Literal(choices[i].Name);
                _checks[i].gameObject.SetActive(choices[i].Active);
            }
            _openedAt = Time.unscaledTime;
            _viewport.SetAsLastSibling();
            _viewport.gameObject.SetActive(true);
            SetBusy(busy);
            SelectFirst();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            var hadFocus = selected != null && selected.transform.IsChildOf(_viewport);
            if (hadFocus)
                events.SetSelectedGameObject(null);
            _viewport.gameObject.SetActive(false);
        }

        public void SetBusy(bool busy)
        {
            if (_busy == busy)
                return;
            _busy = busy;
            _status.gameObject.SetActive(busy);
            _status.text = busy ? "Finish the reply or mic capture first" : string.Empty;
            for (var i = 0; i < _choices.Count; i++)
                _quick[i].Interactable = !busy || _choices[i].Active;
            for (var i = 0; i < _quick.Length + 2; i++)
            {
                var button = SelectableAt(i);
                if (button == null)
                    continue;
                var neighbor = SelectableAt(i ^ 1);
                var up = VerticalNeighbor(i, -1);
                var down = VerticalNeighbor(i, 1);
                button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = up, selectOnLeft = neighbor ?? up,
                    selectOnDown = down, selectOnRight = neighbor ?? down
                };
            }
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (current != null && current.transform.IsChildOf(_viewport) && current.TryGetComponent<Button>(out var selected)
                && !selected.IsInteractable())
                SelectFirst();
        }

        void SelectFirst()
        {
            for (var i = 0; i < _quick.Length + 2; i++)
            {
                var button = SelectableAt(i);
                if (button == null)
                    continue;
                EventSystem.current?.SetSelectedGameObject(button.gameObject);
                return;
            }
        }

        Button SelectableAt(int index)
        {
            var button = index < _quick.Length ? _quick[index] : index == _quick.Length ? _create : _all;
            return button.Button.isActiveAndEnabled && button.Button.IsInteractable() ? button.Button : null;
        }

        Button VerticalNeighbor(int index, int direction)
        {
            var rows = _quick.Length / 2 + 1;
            for (var step = 1; step < rows; step++)
            {
                var row = (index / 2 + direction * step + rows) % rows;
                var candidate = row * 2 + index % 2;
                var button = SelectableAt(candidate) ?? SelectableAt(candidate ^ 1);
                if (button != null)
                    return button;
            }
            return null;
        }

        public void Place(Rect visible, Rect header, float dpiScale)
        {
            var widest = RibbonMenuPlacement.PreferredPersonaWidth * dpiScale;
            var wide = Mathf.InverseLerp(600f * dpiScale, widest, visible.width);
            var wanted = new Vector2(widest, Mathf.Lerp(144f, 192f, wide) * dpiScale);
            var (bounds, opensUp) = RibbonMenuPlacement.PersonaBanner(visible, header, wanted, 112f * dpiScale);
            OpensUp = opensUp;
            var width = bounds.width;
            var height = bounds.height;
            var scale = Mathf.Min(dpiScale, width / 245f, height / 129f);
            if (scale <= 0f)
            {
                HappiUiKit.Place(_viewport, bounds.x, bounds.y, 0f, 0f);
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - _openedAt) / 0.18f);
            var remaining = 1f - progress;
            var reveal = Mathf.Lerp(20f * scale, height, 1f - remaining * remaining * remaining);
            HappiUiKit.Place(_viewport, bounds.x, opensUp ? bounds.yMax - reveal : bounds.yMin, width, reveal);
            HappiUiKit.Place(_banner, 0f, opensUp ? reveal - height : 0f, width, height);

            var showArt = _art.texture != null && width >= 340f * scale;
            var controls = (showArt ? Mathf.Min(width * 0.60f, 395f * scale) : width) - 15f * scale;
            _art.enabled = showArt;
            if (showArt)
            {
                var artLeft = controls - 4f * scale;
                var (frame, uv) = RibbonMenuPlacement.PersonaPainting(new Rect(artLeft, 0f, width - artLeft, height),
                    (float)_art.texture.width / _art.texture.height);
                _art.uvRect = uv;
                HappiUiKit.Place(_art.rectTransform, frame.x, frame.y, frame.width, frame.height);
            }

            var left = 10f * scale;
            var gap = Mathf.Lerp(4f, 6f, wide) * scale;
            var bodyRows = (_choices.Count + 1) / 2;
            var firstRowY = (opensUp ? 8f : 29f) * scale;
            var status = 14f * scale;
            var rowHeight = Mathf.Max(1f, Mathf.Min(Mathf.Lerp(25f, 39f, wide) * scale,
                (height - status - 38f * scale) / (bodyRows + 1) - gap));
            var buttonWidth = Mathf.Max(1f, (controls - left - gap) * 0.5f);
            for (var i = 0; i < _choices.Count; i++)
            {
                var rowX = left + (i % 2) * (buttonWidth + gap);
                var rowY = firstRowY + (i / 2) * (rowHeight + gap);
                PlaceButton(_quick[i], rowX, rowY, buttonWidth, rowHeight, scale, _choices[i].Active);
                HappiUiKit.Place(_checks[i].rectTransform, buttonWidth - 18f * scale, (rowHeight - 12f * scale) * 0.5f,
                    12f * scale, 12f * scale);
            }

            var actionY = firstRowY + bodyRows * (rowHeight + gap);
            PlaceButton(_create, left, actionY, buttonWidth, rowHeight, scale, false);
            PlaceButton(_all, left + buttonWidth + gap, actionY, buttonWidth, rowHeight, scale, false);
            _status.enableAutoSizing = true;
            _status.fontSizeMax = 11f * scale;
            _status.fontSizeMin = 8.5f * scale;
            HappiUiKit.Place(_status.rectTransform, left, actionY + rowHeight, controls - left, status);
            HappiUiKit.Place(_curl.rectTransform, left, (opensUp ? height + scale : 27f * scale),
                27f * scale, Mathf.Min(controls - left, 174f * scale));
        }

        public void RefreshIndicators(Camera camera, Vector2 pointer)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var hasFocus = selected != null && selected.transform.parent == _banner;
            _focusMark.gameObject.SetActive(hasFocus);
            if (hasFocus && _focusMark.parent != selected.transform)
            {
                _focusMark.SetParent(selected.transform, false);
                _focusMark.anchorMin = new Vector2(0.06f, 0f);
                _focusMark.anchorMax = new Vector2(0.90f, 0f);
                _focusMark.offsetMin = new Vector2(0f, 2f);
                _focusMark.offsetMax = new Vector2(0f, 4f);
            }

            if (_busy == true)
                return;
            string hint = null;
            for (var i = 0; i < _choices.Count; i++)
            {
                var row = _quick[i];
                if (row.Label.isTextTruncated && (selected == row.Button.gameObject ||
                    RectTransformUtility.RectangleContainsScreenPoint(row.Rect, pointer, camera)))
                    hint = row.Text;
            }
            _status.text = hint ?? string.Empty;
            _status.gameObject.SetActive(hint != null);
        }

        static void PlaceButton(HappiButton button, float x, float y, float width, float height, float scale, bool check)
        {
            HappiUiKit.Place(button.Rect, x, y, width, height);
            HappiUiKit.Stretch(button.Label.rectTransform, 9f * scale, 0f, (check ? 20f : 5f) * scale, 0f);
            button.Label.enableAutoSizing = true;
            button.Label.fontSizeMin = 10f * scale;
            button.Label.fontSizeMax = 14f * scale;
        }
    }
}
