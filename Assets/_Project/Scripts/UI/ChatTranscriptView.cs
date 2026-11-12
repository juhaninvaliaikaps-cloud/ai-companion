using System;
using System.Collections.Generic;

namespace AICompanion.UI
{
    /// <summary>
    /// What the chat panel shows (JUH-111): one entry per message, only the last
    /// <see cref="MaxShownMessages"/>, each cut to its last <see cref="MaxShownCharacters"/>
    /// characters. A UI Text needs 4 vertices per character and Unity stops at 65000, so the
    /// old single Text broke in long conversations. The conversation itself, what is sent to
    /// the model and <see cref="ChatConversation.FormatTranscript"/> are not changed.
    /// An entry is rebuilt only when its message changed, so streaming stays cheap.
    /// </summary>
    public sealed class ChatTranscriptView
    {
        /// <summary>Messages kept as UI objects. Older ones stay in the conversation.</summary>
        public const int MaxShownMessages = 100;

        /// <summary>Characters shown from one message: 6000 x 4 = 24000 vertices, well below 65000.</summary>
        public const int MaxShownCharacters = 6000;

        public const string HiddenNoteFormat = "Earlier messages are not shown here ({0}). They are still kept.";
        public const string CutPrefix = "[...] ";

        readonly List<string> _entries = new List<string>();
        readonly List<bool> _changed = new List<bool>();
        readonly List<ChatLine> _sources = new List<ChatLine>();
        readonly List<string> _sourceTexts = new List<string>();
        readonly List<int> _visible = new List<int>();
        int _hidden = -1;

        public int Count
        {
            get { return _entries.Count; }
        }

        /// <summary>Messages left out because of <see cref="MaxShownMessages"/>.</summary>
        public int HiddenMessages { get; private set; }

        public string Entry(int index)
        {
            return _entries[index];
        }

        /// <summary>True when entry <paramref name="index"/> differs from the last <see cref="Update"/>.</summary>
        public bool Changed(int index)
        {
            return _changed[index];
        }

        /// <summary>
        /// Builds the entries from the conversation. Returns true when anything changed.
        /// Entry 0 is a note when older messages are hidden.
        /// </summary>
        public bool Update(IReadOnlyList<ChatLine> lines)
        {
            _visible.Clear();
            if (lines != null)
            {
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line == null)
                        continue;
                    // Same rule as FormatTranscript: an empty reply that is still streaming is not shown yet.
                    if (line.Kind == ChatLineKind.Assistant && line.Streaming && line.Text.Length == 0)
                        continue;
                    _visible.Add(i);
                }
            }

            var hidden = Math.Max(0, _visible.Count - MaxShownMessages);
            var first = hidden;
            var count = _visible.Count - first + (hidden > 0 ? 1 : 0);
            var any = count != _entries.Count;

            while (_entries.Count < count)
            {
                _entries.Add(string.Empty);
                _changed.Add(true);
                _sources.Add(null);
                _sourceTexts.Add(null);
            }

            while (_entries.Count > count)
            {
                var last = _entries.Count - 1;
                _entries.RemoveAt(last);
                _changed.RemoveAt(last);
                _sources.RemoveAt(last);
                _sourceTexts.RemoveAt(last);
            }

            var slot = 0;
            if (hidden > 0)
            {
                var noteChanged = hidden != _hidden || _sources[0] != null;
                if (noteChanged)
                {
                    _entries[0] = string.Format(HiddenNoteFormat, hidden);
                    _sources[0] = null;
                    _sourceTexts[0] = null;
                }

                _changed[0] = noteChanged;
                any |= noteChanged;
                slot = 1;
            }

            _hidden = hidden;
            HiddenMessages = hidden;

            for (var v = first; v < _visible.Count; v++, slot++)
            {
                var line = lines[_visible[v]];
                // The text is a new string after every streamed piece, so a reference check is enough.
                var same = ReferenceEquals(_sources[slot], line) && ReferenceEquals(_sourceTexts[slot], line.Text);
                if (!same)
                {
                    _entries[slot] = Format(line);
                    _sources[slot] = line;
                    _sourceTexts[slot] = line.Text;
                }

                _changed[slot] = !same;
                any |= !same;
            }

            return any;
        }

        /// <summary>"You", "Companion" or "Note", a line break, then the text, cut to its last <see cref="MaxShownCharacters"/>.</summary>
        public static string Format(ChatLine line)
        {
            var text = line.Text ?? string.Empty;
            if (text.Length > MaxShownCharacters)
                text = CutPrefix + text.Substring(text.Length - MaxShownCharacters);
            return Label(line.Kind) + "\n" + text;
        }

        static string Label(ChatLineKind kind)
        {
            switch (kind)
            {
                case ChatLineKind.User:
                    return "You";
                case ChatLineKind.Assistant:
                    return "Companion";
                default:
                    return "Note";
            }
        }
    }
}
