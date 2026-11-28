using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AICompanion.Core
{
    [Serializable]
    public sealed class ChatMemoryLink
    {
        public string store;
        public string id;
        public bool timeline;
        public bool privateEntry;
        public string phrase;

        public const int MaxPhraseLength = 80;

        public static int UniquePosition(string text, string phrase)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(phrase) ||
                phrase.Length > MaxPhraseLength || phrase.IndexOfAny(InvalidPhraseCharacters) >= 0)
                return -1;

            var position = text.IndexOf(phrase, StringComparison.Ordinal);
            if (position < 0 || text.IndexOf(phrase, position + 1, StringComparison.Ordinal) >= 0)
                return -1;
            var end = position + phrase.Length;
            if ((position > 0 && char.IsLetterOrDigit(text[position - 1]) && char.IsLetterOrDigit(phrase[0])) ||
                (end < text.Length && char.IsLetterOrDigit(text[end]) && char.IsLetterOrDigit(phrase[phrase.Length - 1])))
                return -1;
            return position;
        }

        static readonly char[] InvalidPhraseCharacters = { '\r', '\n', '<', '>' };
    }

    // Source text is scoped to one request. Only the target and the displayed phrase are saved.
    public sealed class ChatMemorySources
    {
        static readonly Regex Words = new Regex(@"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant);
        static readonly HashSet<string> Common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "about", "after", "again", "also", "because", "been", "before", "could", "earlier", "from",
            "have", "here", "just", "like", "really", "remember", "said", "some", "that", "them", "then",
            "there", "these", "they", "thing", "this", "told", "user", "want", "were", "what", "when",
            "where", "with", "would", "your"
        };
        readonly List<(ChatMemoryLink Target, string Text)> _items = new List<(ChatMemoryLink, string)>();
        readonly string _store;

        public ChatMemorySources(string store) => _store = store;
        public int Count => _items.Count;

        public string Add(string id, string text, bool timeline, bool privateEntry)
        {
            if (string.IsNullOrEmpty(_store) || string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(text))
                return string.Empty;
            _items.Add((new ChatMemoryLink { store = _store, id = id, timeline = timeline, privateEntry = privateEntry }, text));
            return "[r" + _items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "] ";
        }

        public ChatMemoryLink[] Verify(string reply, string first, string second)
        {
            var a = VerifyOne(reply, first);
            var b = VerifyOne(reply, second);
            if (a == null)
                return b == null ? null : new[] { b };
            if (b == null || (a.id == b.id && a.timeline == b.timeline))
                return new[] { a };
            var aStart = ChatMemoryLink.UniquePosition(reply, a.phrase);
            var bStart = ChatMemoryLink.UniquePosition(reply, b.phrase);
            if (aStart < bStart + b.phrase.Length && bStart < aStart + a.phrase.Length)
                return new[] { a };
            return new[] { a, b };
        }

        ChatMemoryLink VerifyOne(string reply, string marker)
        {
            if (string.IsNullOrEmpty(marker) || marker[0] != 'r' || marker.Length > ChatMemoryLink.MaxPhraseLength + 5)
                return null;
            var split = marker.IndexOf('|');
            if (split < 2 || !int.TryParse(marker.Substring(1, split - 1),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
                || index < 1 || index > _items.Count)
                return null;
            var phrase = marker.Substring(split + 1).Trim();
            var source = _items[index - 1];
            if (ChatMemoryLink.UniquePosition(reply, phrase) < 0 || !SharesDetail(source.Text, phrase))
                return null;
            return new ChatMemoryLink
            {
                store = source.Target.store, id = source.Target.id, timeline = source.Target.timeline,
                privateEntry = source.Target.privateEntry, phrase = phrase
            };
        }

        static bool SharesDetail(string source, string phrase)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Words.Matches(source))
                tokens.Add(match.Value);
            foreach (Match match in Words.Matches(phrase))
                if (match.Length >= 3 && !Common.Contains(match.Value) && tokens.Contains(match.Value))
                    return true;
            return false;
        }
    }
}
