using AICompanion.Core;
using AICompanion.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-122: floating pieces near her head while you chat, like floating combat text. The rules
    /// (what shows, the text, the motion) are in <see cref="RelationshipFeedback"/>; this only
    /// draws them.
    /// <list type="bullet">
    /// <item>Pink heart: affection up. Grey heart: down. Light blue: a new familiarity band.</item>
    /// <item>Crit: big and gold with a punch and a CRITICAL caption. Crit drop: big and grey.</item>
    /// <item>JUH-170, 18+ only: magenta heart, desire up; dim violet, desire down or spent.</item>
    /// </list>
    /// One pool of <see cref="RelationshipFeedback.Capacity"/> labels is built once; a change only
    /// sets their text, so there is no Instantiate per change and no allocation per frame.
    /// Anchored at her head (3D avatar or 2D video), else at the expression portrait. Nothing
    /// shows while "Show relationship changes while chatting" or "Relationship changes" is off.
    /// </summary>
    public sealed class FloatingFeedbackView : MonoBehaviour
    {
        const float PlaneDistance = 0.47f;
        const int SortingOrder = 58;

        static readonly Color Background = HappiTheme.Hex("#0B1530");
        static readonly Color Text = HappiTheme.Hex("#EAF2FF");
        static readonly Color Pink = HappiTheme.Hex("#FF7EB6");
        static readonly Color Grey = HappiTheme.Hex("#B4C3DC");
        static readonly Color GreyBorder = HappiTheme.Hex("#7FA8E0");
        static readonly Color Gold = HappiTheme.Hex("#FFD166");
        static readonly Color Light = HappiTheme.Hex("#9FD4FF");
        static readonly Color Magenta = HappiTheme.Hex("#E86BFF");
        static readonly Color Violet = HappiTheme.Hex("#9A7BC4");

        sealed class Slot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Border;
            public Image Fill;
            public Image Glow;
            public Image Icon;
            public LayoutElement IconSize;
            public TextMeshProUGUI Amount;
            public RectTransform Column;
            public TextMeshProUGUI Caption;
            public TextMeshProUGUI Label;
            public HorizontalLayoutGroup Row;

            /// <summary>The piece this slot was last set up for; 0 for none.</summary>
            public int ShownId;
        }

        /// <summary>How many times a slot was set up (text, colours, layout). Once per piece, not per frame.</summary>
        public int SetupCount { get; private set; }

        SettingsPanel _settings;
        Wheel.HeadLocator _head;
        readonly RelationshipFeedback _feedback = new RelationshipFeedback();
        readonly Slot[] _slots = new Slot[RelationshipFeedback.Capacity];
        GameObject _canvasObject;
        Canvas _canvas;
        bool _subscribed;
        bool _attached;

        public void Attach(SettingsPanel settings)
        {
            _settings = settings;
            _head = new Wheel.HeadLocator(gameObject);
            _attached = true;
            if (_canvasObject == null)
                Build();
            _canvasObject.SetActive(true);
            Subscribe(true);
        }

        public void Detach()
        {
            _attached = false;
            Subscribe(false);
            _feedback.Clear();
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_attached)
                Subscribe(true);
        }

        void OnDisable()
        {
            Subscribe(false);
        }

        void OnDestroy()
        {
            Subscribe(false);
            if (_canvasObject != null)
                Destroy(_canvasObject);
        }

        void Subscribe(bool on)
        {
            if (on == _subscribed)
                return;
            _subscribed = on;
            if (on)
            {
                RelationshipNow.Changed += OnChanged;
                DesireNow.Changed += OnDesire;
            }
            else
            {
                RelationshipNow.Changed -= OnChanged;
                DesireNow.Changed -= OnDesire;
            }
        }

        /// <summary>JUH-170: desire pieces need the floating changes on and the 18+ gate open.</summary>
        bool DesireEnabled
        {
            get
            {
                var settings = _settings != null ? _settings.CurrentSettings : null;
                return settings != null && settings.ShowRelationshipFeedback && IntimateExpressions.Active;
            }
        }

        void OnDesire(DesireNotice notice)
        {
            _feedback.AddDesire(notice, Time.unscaledTime, DesireEnabled);
        }

        bool Enabled
        {
            get
            {
                var settings = _settings != null ? _settings.CurrentSettings : null;
                return settings != null && settings.ShowRelationshipFeedback && PisaraSwitches.Relationship;
            }
        }

        void OnChanged(RelationshipNotice notice)
        {
            var added = _feedback.Add(notice, Time.unscaledTime, Enabled);
            if (added > 0)
                Debug.Log("Relationship feedback: " + DescribeNewest(added));
        }

        string DescribeNewest(int added)
        {
            var text = string.Empty;
            for (var i = _feedback.Count - added; i < _feedback.Count; i++)
            {
                text += (text.Length > 0 ? ", " : string.Empty) + _feedback.Piece(i).LogText;
            }

            return text;
        }

        void Update()
        {
            if (!_attached || _canvasObject == null)
                return;
            var now = Time.unscaledTime;
            if (_feedback.Count > 0)
            {
                // JUH-170: desire pieces go the moment the gate closes; the others with their setting.
                if (!Enabled && _feedback.Has(false))
                    _feedback.Drop(false);
                if (!DesireEnabled && _feedback.Has(true))
                    _feedback.Drop(true);
            }
            _feedback.Tick(now);

            if (_feedback.Count == 0)
            {
                HideFrom(0);
                return;
            }

            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            var scale = HappiTheme.Current.UiScale(Screen.dpi);
            Vector2 anchor;
            var right = Anchor(out anchor, scale);

            // Newest at the bottom; each older piece rises and stays above the newer one.
            var floor = float.NegativeInfinity;
            for (var i = _feedback.Count - 1; i >= 0; i--)
            {
                var piece = _feedback.Piece(i);
                var slot = _slots[i];
                Show(slot, piece);
                var height = slot.Root.rect.height;
                var y = anchor.y + RelationshipFeedback.Rise(piece, now) * scale;
                if (y < floor)
                    y = floor;
                floor = y + height * RelationshipFeedback.Punch(piece, now) + 6f * scale;
                slot.Root.pivot = new Vector2(right ? 0f : 1f, 0f);
                slot.Root.anchoredPosition = new Vector2(anchor.x, y);
                var punch = RelationshipFeedback.Punch(piece, now) * scale;
                slot.Root.localScale = new Vector3(punch, punch, 1f);
                slot.Group.alpha = RelationshipFeedback.Alpha(piece, now);
            }

            HideFrom(_feedback.Count);
        }

        /// <summary>
        /// Where the pieces start, in window pixels from the bottom-left: beside her head, on the
        /// side with room. True when they go to the right of it.
        /// </summary>
        bool Anchor(out Vector2 anchor, float scale)
        {
            Vector2 head;
            var side = 56f * scale;
            if (_head != null && _head.TryGet(Time.unscaledDeltaTime, out head))
            {
                var right = head.x + side + 240f * scale < Screen.width;
                // Keep clear of the expression portrait beside her head.
                var portrait = ExpressionPortraitPanel.ShownRect;
                if (portrait.width > 0f)
                {
                    var portraitCenter = portrait.x + portrait.width * 0.5f;
                    if (right && portraitCenter > head.x)
                        right = head.x - side - 240f * scale < 0f;
                    else if (!right && portraitCenter < head.x)
                        right = true;
                }

                anchor = new Vector2(head.x + (right ? side : -side), head.y - 10f * scale);
                return right;
            }

            var shown = ExpressionPortraitPanel.ShownRect;
            if (shown.width > 0f)
            {
                anchor = new Vector2(shown.x + shown.width * 0.5f, Screen.height - shown.y + 8f * scale);
                return true;
            }

            anchor = new Vector2(Screen.width * 0.5f, Screen.height * 0.7f);
            return true;
        }

        void HideFrom(int index)
        {
            for (var i = index; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Root.gameObject.activeSelf)
                    slot.Root.gameObject.SetActive(false);
                slot.ShownId = 0;
            }
        }

        /// <summary>Sets a slot up for its piece once (text, colours, sizes); later frames only move it.</summary>
        void Show(Slot slot, FeedbackPiece piece)
        {
            if (!slot.Root.gameObject.activeSelf)
                slot.Root.gameObject.SetActive(true);
            // Once per piece: a normal piece's text is "Amount Label", so comparing the text never
            // matched and every piece was rebuilt each frame (review of #107).
            if (!RelationshipFeedback.NeedsSetup(slot.ShownId, piece))
                return;
            slot.ShownId = piece.Id;
            SetupCount++;

            var crit = piece.IsCritical;
            var border = Pink;
            var iconColor = Pink;
            var amountColor = Text;
            Sprite icon = FeedbackSprites.Heart();
            switch (piece.Kind)
            {
                case FeedbackKind.Down:
                    border = GreyBorder;
                    iconColor = Grey;
                    break;
                case FeedbackKind.CritUp:
                    border = Gold;
                    amountColor = Gold;
                    break;
                case FeedbackKind.CritDown:
                    border = Grey;
                    iconColor = Grey;
                    amountColor = Grey;
                    break;
                case FeedbackKind.Familiarity:
                    border = Light;
                    icon = null;
                    break;
                case FeedbackKind.Secret:
                    border = Gold;
                    iconColor = Gold;
                    icon = FeedbackSprites.Star();
                    break;
                case FeedbackKind.DesireUp:
                    border = Magenta;
                    iconColor = Magenta;
                    break;
                case FeedbackKind.DesireDown:
                    border = Violet;
                    iconColor = Violet;
                    break;
            }

            slot.Border.color = border;
            slot.Glow.gameObject.SetActive(piece.Kind == FeedbackKind.CritUp);
            slot.Icon.gameObject.SetActive(icon != null);
            slot.Icon.sprite = icon;
            slot.Icon.color = iconColor;
            var iconSize = crit ? 26f : 14f;
            slot.IconSize.preferredWidth = iconSize;
            slot.IconSize.preferredHeight = iconSize;

            var thick = crit ? 3f : 1f;
            slot.Fill.rectTransform.offsetMin = new Vector2(thick, thick);
            slot.Fill.rectTransform.offsetMax = new Vector2(-thick, -thick);
            slot.Row.padding = crit ? new RectOffset(18, 18, 8, 8) : new RectOffset(10, 10, 4, 4);
            slot.Row.spacing = crit ? 10f : 6f;

            if (crit)
            {
                slot.Amount.gameObject.SetActive(true);
                slot.Amount.text = piece.Amount;
                slot.Amount.color = amountColor;
                slot.Column.gameObject.SetActive(true);
                slot.Caption.text = RelationshipFeedback.CriticalLabel;
                slot.Caption.color = amountColor;
                slot.Label.text = piece.Label;
                slot.Label.fontSize = 13f;
            }
            else
            {
                // One line: "+1.5 Compliment".
                slot.Amount.gameObject.SetActive(false);
                slot.Column.gameObject.SetActive(true);
                slot.Caption.text = string.Empty;
                slot.Label.text = piece.Amount.Length > 0 ? piece.Amount + " " + piece.Label : piece.Label;
                slot.Label.fontSize = 13f;
            }

            slot.Caption.gameObject.SetActive(crit);
            LayoutRebuilder.ForceRebuildLayoutImmediate(slot.Root);
        }

        void Build()
        {
            _canvasObject = HappiUiKit.CreateCanvas(transform, "HappiFloatingFeedback", SortingOrder, out _canvas);
            var raycaster = _canvasObject.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;
            for (var i = 0; i < _slots.Length; i++)
                _slots[i] = BuildSlot(i);
        }

        Slot BuildSlot(int index)
        {
            var slot = new Slot();
            slot.Root = HappiUiKit.Rect(_canvasObject.transform, "Piece " + index);
            slot.Root.anchorMin = Vector2.zero;
            slot.Root.anchorMax = Vector2.zero;
            slot.Root.pivot = Vector2.zero;
            slot.Group = slot.Root.gameObject.AddComponent<CanvasGroup>();
            slot.Group.blocksRaycasts = false;
            slot.Group.interactable = false;

            // The gold glow behind a crit.
            slot.Glow = HappiUiKit.Panel(slot.Root, "Glow", HappiTheme.WithAlpha(Gold, 0.35f), false);
            slot.Glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HappiUiKit.Stretch(slot.Glow.rectTransform, -8f, -8f, -8f, -8f);

            // The border is the rounded shape in the border colour; the fill sits inside it.
            slot.Border = slot.Root.gameObject.AddComponent<Image>();
            slot.Border.sprite = HappiUiKit.Rounded();
            slot.Border.type = Image.Type.Sliced;
            slot.Border.raycastTarget = false;
            slot.Fill = HappiUiKit.Panel(slot.Root, "Fill", Background, false);
            slot.Fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            slot.Fill.rectTransform.anchorMin = Vector2.zero;
            slot.Fill.rectTransform.anchorMax = Vector2.one;

            slot.Row = slot.Root.gameObject.AddComponent<HorizontalLayoutGroup>();
            slot.Row.childAlignment = TextAnchor.MiddleLeft;
            slot.Row.childControlWidth = true;
            slot.Row.childControlHeight = true;
            slot.Row.childForceExpandWidth = false;
            slot.Row.childForceExpandHeight = false;
            var fitter = slot.Root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconRect = HappiUiKit.Rect(slot.Root, "Icon");
            slot.Icon = iconRect.gameObject.AddComponent<Image>();
            slot.Icon.raycastTarget = false;
            slot.Icon.preserveAspect = true;
            slot.IconSize = iconRect.gameObject.AddComponent<LayoutElement>();

            slot.Amount = NoWrap(HappiUiKit.Text(slot.Root, "Amount", string.Empty, 30f, Gold, TextAlignmentOptions.MidlineLeft, true));
            slot.Amount.fontWeight = FontWeight.Black;

            slot.Column = HappiUiKit.Rect(slot.Root, "Column");
            var column = slot.Column.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            column.spacing = 0f;
            slot.Caption = NoWrap(HappiUiKit.Text(slot.Column, "Caption", RelationshipFeedback.CriticalLabel, 12f, Gold, TextAlignmentOptions.MidlineLeft, true));
            slot.Caption.characterSpacing = 8f;
            slot.Label = NoWrap(HappiUiKit.Text(slot.Column, "Label", string.Empty, 13f, Text, TextAlignmentOptions.MidlineLeft, true));

            slot.Root.gameObject.SetActive(false);
            return slot;
        }

        static TextMeshProUGUI NoWrap(TextMeshProUGUI text)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            return text;
        }
    }

    /// <summary>The heart and star icons, drawn once into small textures.</summary>
    static class FeedbackSprites
    {
        const int Size = 64;
        static Sprite _heart;
        static Sprite _star;

        public static Sprite Heart()
        {
            if (_heart == null)
                _heart = Draw(HeartCoverage);
            return _heart;
        }

        public static Sprite Star()
        {
            if (_star == null)
                _star = Draw(StarCoverage);
            return _star;
        }

        static Sprite Draw(System.Func<float, float, bool> inside)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[Size * Size];
            const int samples = 4;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var u = (x + (sx + 0.5f) / samples) / Size * 2f - 1f;
                            var v = (y + (sy + 0.5f) / samples) / Size * 2f - 1f;
                            if (inside(u, v))
                                hits++;
                        }
                    }

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (samples * samples)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>The classic heart curve, (x² + y² - 1)³ - x² y³ ≤ 0, fitted into the square.</summary>
        static bool HeartCoverage(float u, float v)
        {
            var x = u * 1.25f;
            var y = v * 1.25f + 0.15f;
            var a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        static readonly Vector2[] StarPoints = BuildStar();

        static Vector2[] BuildStar()
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                var radius = i % 2 == 0 ? 0.95f : 0.4f;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 0.05f);
            }

            return points;
        }

        static bool StarCoverage(float u, float v)
        {
            var inside = false;
            for (int i = 0, j = StarPoints.Length - 1; i < StarPoints.Length; j = i++)
            {
                var a = StarPoints[i];
                var b = StarPoints[j];
                if ((a.y > v) != (b.y > v) && u < (b.x - a.x) * (v - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }
    }
}
