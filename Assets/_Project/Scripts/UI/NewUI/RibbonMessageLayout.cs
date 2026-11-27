using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Shared layout for the live reply and the finished transcript cards.</summary>
    public readonly struct RibbonMessageLayout
    {
        readonly float _width;
        readonly float _scale;
        readonly bool _roomy;

        public RibbonMessageLayout(float width, float scale)
        {
            _width = width;
            _scale = scale;
            _roomy = width >= 360f * scale;
        }

        public bool ShowSpeakers => _width >= 158f * _scale;
        public float Gap => (_roomy ? 16f : 8f) * _scale;
        public float VerticalPadding => (_roomy ? 11f : 6f) * _scale;

        public float TextPadding(bool reply) => (_roomy ? (reply ? 16f : 14f) : (reply ? 11f : 8f)) * _scale;

        float BadgeSize(bool reply) => (_roomy ? (reply ? 52f : 44f) : (reply ? 33f : 30f)) * _scale;

        float CardLeft(bool reply, bool speaker) =>
            (speaker ? BadgeSize(true) + (_roomy ? (reply ? 17f : 9f) : 8f) * _scale : 0f) + 2f;

        float CardWidth(bool reply, bool speaker) => Mathf.Max(1f, _width - CardLeft(reply, speaker) - 2f);

        public float TextWidth(bool reply, bool speaker) =>
            Mathf.Max(1f, CardWidth(reply, speaker) - 2f * TextPadding(reply));

        public Rect Card(bool reply, bool speaker, float y, float textHeight)
        {
            var height = Mathf.Max(textHeight + 2f * VerticalPadding, speaker ? BadgeSize(reply) : 0f);
            return new Rect(CardLeft(reply, speaker), y, CardWidth(reply, speaker), height);
        }

        public Rect Badge(bool reply, Rect card)
        {
            var size = BadgeSize(reply);
            var x = 2f + (BadgeSize(true) - size) * 0.5f;
            // Keep a portrait aligned with the start of a long message; center it on short replies.
            var offset = Mathf.Clamp((card.height - size) * 0.5f, 0f, 6f * _scale);
            return new Rect(x, card.y + offset, size, size);
        }
    }
}
