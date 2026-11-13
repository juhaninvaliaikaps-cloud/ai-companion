using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AICompanion.Avatar;
using AICompanion.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI.Wheel
{
    /// <summary>Shift state even while the overlay window is not focused (right-click on her does not focus it).</summary>
    public static class ModifierKeys
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int virtualKey);
#endif

        public static bool ShiftHeld()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return (GetAsyncKeyState(0x10) & 0x8000) != 0;
            }
            catch (Exception)
            {
            }
#endif
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }

    /// <summary>
    /// Her head in Unity screen pixels (origin bottom-left), the same way the expression portrait
    /// finds it: the 2D video's head point, otherwise the humanoid head bone.
    /// </summary>
    public sealed class HeadLocator
    {
        readonly GameObject _host;
        Transform _head;
        float _retry;

        public HeadLocator(GameObject host)
        {
            _host = host;
        }

        Transform _leftFoot;
        Transform _rightFoot;

        /// <summary>
        /// Her head and feet line (window pixels from the bottom-left). Feet: the 2D video's idle
        /// rectangle bottom, the 3D humanoid's lower foot bone, or the window bottom.
        /// </summary>
        public bool TryGetBody(float deltaTime, out AICompanion.NewUI.HerBody body)
        {
            body = default;
            Vector2 head;
            if (!TryGet(deltaTime, out head))
                return false;
            body.Head = head;
            body.FeetY = 0f;

            var video = _host != null ? _host.GetComponent<VideoAvatar>() : null;
            Rect rect;
            if (video != null && video.TryGetBodyRect(out rect))
            {
                body.FeetY = rect.y;
                return true;
            }

            var camera = Camera.main;
            if (_head != null && camera != null)
            {
                if (_leftFoot == null || _rightFoot == null)
                {
                    var animator = _head.GetComponentInParent<Animator>();
                    if (animator != null && animator.isHuman)
                    {
                        _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                        _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    }
                }

                var feet = float.MaxValue;
                if (_leftFoot != null)
                    feet = Mathf.Min(feet, camera.WorldToScreenPoint(_leftFoot.position).y);
                if (_rightFoot != null)
                    feet = Mathf.Min(feet, camera.WorldToScreenPoint(_rightFoot.position).y);
                if (feet < float.MaxValue)
                    body.FeetY = Mathf.Max(0f, feet);
            }

            return true;
        }

        public bool TryGet(float deltaTime, out Vector2 head)
        {
            head = Vector2.zero;
            if (_host == null)
                return false;

            var video = _host.GetComponent<VideoAvatar>();
            Vector2 top;
            if (video != null && video.TryGetPortraitHead(out top))
            {
                head = new Vector2(top.x, Screen.height - top.y);
                return true;
            }

            var mount = _host.GetComponent<AvatarMount>();
            if (mount != null && mount.ModelHidden)
                return false;

            if (_head == null)
            {
                _retry -= deltaTime;
                if (_retry > 0f)
                    return false;
                _retry = 0.5f;
                var animators = _host.GetComponentsInChildren<Animator>(true);
                for (var i = 0; i < animators.Length && _head == null; i++)
                {
                    if (animators[i] != null && animators[i].isHuman)
                        _head = animators[i].GetBoneTransform(HumanBodyBones.Head);
                }
            }

            var camera = Camera.main;
            if (_head == null || camera == null)
                return false;
            var screen = camera.WorldToScreenPoint(_head.position);
            if (screen.z <= 0.01f)
                return false;
            head = new Vector2(screen.x, screen.y);
            return true;
        }
    }

    /// <summary>One button row on a wheel card.</summary>
    public struct CardButton
    {
        public string Label;
        public Action OnClick;
        public bool Danger;

        public CardButton(string label, Action onClick, bool danger = false)
        {
            Label = label;
            OnClick = onClick;
            Danger = danger;
        }
    }

    /// <summary>
    /// A small card shown in place of the wheel: her mood, a refusal, the memory list or a
    /// confirm step. Title, text, and a column of buttons, all in the Happi theme.
    /// </summary>
    public sealed class WheelCard
    {
        readonly HappiTheme _theme;
        readonly RectTransform _root;
        readonly TextMeshProUGUI _title;
        readonly TextMeshProUGUI _body;
        readonly List<HappiButton> _buttons = new List<HappiButton>();
        readonly CanvasGroup _group;

        public WheelCard(Transform parent, HappiTheme theme)
        {
            _theme = theme;
            var panel = HappiUiKit.Panel(parent, "WheelCard", theme.WheelBackground, true);
            _root = panel.rectTransform;
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            _group = panel.gameObject.AddComponent<CanvasGroup>();
            _title = HappiUiKit.Text(_root, "Title", string.Empty, theme.FontTitle, theme.Text, TextAlignmentOptions.TopLeft, true);
            _body = HappiUiKit.Text(_root, "Body", string.Empty, theme.FontBody, theme.Text, TextAlignmentOptions.TopLeft, false);
            _body.overflowMode = TextOverflowModes.Overflow;
            _root.gameObject.SetActive(false);
        }

        public bool Visible => _root.gameObject.activeSelf;

        public RectTransform Root => _root;

        public void Show(string title, string body, IReadOnlyList<CardButton> buttons, Vector2 center, float scale, Vector2 screen)
        {
            for (var i = 0; i < _buttons.Count; i++)
                UnityEngine.Object.Destroy(_buttons[i].Rect.gameObject);
            _buttons.Clear();

            var pad = _theme.Padding * scale;
            var width = Mathf.Min(270f * scale, screen.x - 16f);
            var inner = width - 2f * pad;
            _title.fontSize = _theme.FontTitle * scale;
            _body.fontSize = _theme.FontBody * scale;
            _title.text = HappiUiKit.Literal(title ?? string.Empty);
            _body.text = HappiUiKit.Literal(body ?? string.Empty);
            var titleH = string.IsNullOrEmpty(title) ? 0f : _title.GetPreferredValues(_title.text, inner, 0f).y;
            var bodyH = string.IsNullOrEmpty(body) ? 0f : _body.GetPreferredValues(_body.text, inner, 0f).y;
            var rowH = _theme.ButtonHeight * scale;
            var gap = _theme.Spacing * scale;
            var count = buttons != null ? buttons.Count : 0;
            var height = pad + titleH + (titleH > 0f ? gap : 0f) + bodyH + (bodyH > 0f ? gap : 0f) + count * (rowH + gap) + pad - gap;
            height = Mathf.Min(height, screen.y - 16f);

            _root.sizeDelta = new Vector2(width, height);
            var clampedX = Mathf.Clamp(center.x, width * 0.5f + 8f, Mathf.Max(width * 0.5f + 8f, screen.x - width * 0.5f - 8f));
            var clampedY = Mathf.Clamp(center.y, height * 0.5f + 8f, Mathf.Max(height * 0.5f + 8f, screen.y - height * 0.5f - 8f));
            _root.anchoredPosition = new Vector2(clampedX, clampedY);

            var y = pad;
            HappiUiKit.Place(_title.rectTransform, pad, y, inner, titleH);
            y += titleH > 0f ? titleH + gap : 0f;
            HappiUiKit.Place(_body.rectTransform, pad, y, inner, bodyH);
            y += bodyH > 0f ? bodyH + gap : 0f;
            for (var i = 0; i < count; i++)
            {
                var spec = buttons[i];
                var button = HappiUiKit.Button(_root, "CardButton", HappiUiKit.Literal(spec.Label), _theme.FontSmall * scale, spec.OnClick);
                button.Label.textWrappingMode = TextWrappingModes.NoWrap;
                button.Label.overflowMode = TextOverflowModes.Ellipsis;
                if (spec.Danger)
                {
                    var colors = button.Button.colors;
                    colors.normalColor = HappiTheme.Darken(_theme.Intimate, 0.35f);
                    button.Button.colors = colors;
                }

                HappiUiKit.Place(button.Rect, pad, y, inner, rowH);
                y += rowH + gap;
                _buttons.Add(button);
            }

            _group.alpha = 1f;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        public bool Contains(Vector2 unityScreen)
        {
            if (!Visible)
                return false;
            var size = _root.sizeDelta;
            var center = _root.anchoredPosition;
            return Mathf.Abs(unityScreen.x - center.x) <= size.x * 0.5f + 2f && Mathf.Abs(unityScreen.y - center.y) <= size.y * 0.5f + 2f;
        }
    }
}
