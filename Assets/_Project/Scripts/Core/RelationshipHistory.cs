using System.Globalization;

namespace AICompanion.Core
{
    /// <summary>
    /// The last few relationship changes with a plain reason (JUH-117), so the user can see
    /// why affection moved: "An insult stung: affection -4.6". Kept in memory for the session;
    /// neutral exchanges are not recorded. A fixed ring, so recording never allocates a list.
    /// </summary>
    public sealed class RelationshipHistory
    {
        public const int Capacity = 8;

        readonly string[] _lines = new string[Capacity];
        readonly sbyte[] _directions = new sbyte[Capacity];
        int _next;
        int _count;

        public int Count
        {
            get { return _count; }
        }

        /// <summary>Records a change that moved affection. Returns the line, or empty when nothing moved.</summary>
        public string Record(RelationshipChange change)
        {
            var line = Describe(change);
            if (line.Length == 0)
                return line;
            _lines[_next] = line;
            _directions[_next] = change.AffectionDelta > 0f ? (sbyte)1 : (sbyte)-1;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
                _count++;
            return line;
        }

        /// <summary>The line <paramref name="back"/> steps ago: 0 is the newest. Empty when there is none.</summary>
        public string Line(int back)
        {
            if (back < 0 || back >= _count)
                return string.Empty;
            return _lines[Index(back)] ?? string.Empty;
        }

        public int Direction(int back) => back >= 0 && back < _count ? _directions[Index(back)] : 0;

        int Index(int back) => (_next - 1 - back + Capacity) % Capacity;

        public void Clear()
        {
            for (var i = 0; i < Capacity; i++)
                _lines[i] = null;
            _next = 0;
            _count = 0;
        }

        /// <summary>
        /// "A tease landed well: affection +1.8" or "An insult stung: affection -4.6"; a critical
        /// hit starts with "Critical: " (JUH-122). Empty for a neutral exchange or a change smaller than 0.05.
        /// </summary>
        public static string Describe(RelationshipChange change)
        {
            if (change.Landing == LandingEvent.Neutral || System.Math.Abs(change.AffectionDelta) < 0.05f)
                return string.Empty;
            var up = change.AffectionDelta > 0f;
            return (change.Critical ? "Critical: " : string.Empty) + Phrase(change.Landing, up) + ": affection "
                + change.AffectionDelta.ToString("+0.#;-0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The short name on a floating piece (JUH-122): "Compliment", "Brushed off". Empty for
        /// neutral. Kept next to <see cref="Phrase"/> so the two stay in step.
        /// </summary>
        public static string ShortName(LandingEvent landing)
        {
            switch (landing)
            {
                case LandingEvent.Complimented:
                    return "Compliment";
                case LandingEvent.Thanked:
                    return "Thanked";
                case LandingEvent.Comforted:
                    return "Comfort";
                case LandingEvent.Teased:
                    return "Tease";
                case LandingEvent.Insulted:
                    return "Insult";
                case LandingEvent.Dismissed:
                    return "Brushed off";
                case LandingEvent.Apologized:
                    return "Apology";
                case LandingEvent.Flirted:
                    return "Flirt";
                default:
                    return string.Empty;
            }
        }

        static string Phrase(LandingEvent landing, bool up)
        {
            switch (landing)
            {
                case LandingEvent.Complimented:
                    return up ? "A compliment landed well" : "A compliment fell flat";
                case LandingEvent.Thanked:
                    return up ? "Your thanks landed well" : "Your thanks fell flat";
                case LandingEvent.Comforted:
                    return up ? "Your comfort helped" : "Your comfort was unwelcome";
                case LandingEvent.Teased:
                    return up ? "A tease landed well" : "A tease landed badly";
                case LandingEvent.Insulted:
                    return up ? "An insult was taken as a joke" : "An insult stung";
                case LandingEvent.Dismissed:
                    return up ? "Being brushed off did not bother her" : "She felt brushed off";
                case LandingEvent.Apologized:
                    return up ? "Your apology was accepted" : "Your apology fell flat";
                case LandingEvent.Flirted:
                    return up ? "Flirting was welcome" : "Flirting was unwelcome";
                default:
                    return "Something changed";
            }
        }
    }

    /// <summary>The relationship part of the Mood card (JUH-117): where she stands and the last few reasons.</summary>
    public static class RelationshipCard
    {
        public const int MaxReasons = 4;

        public static string Describe(float affection, float familiarity, RelationshipHistory history, bool changesOn)
        {
            var sb = new System.Text.StringBuilder(240);
            sb.Append("With you: ").Append(Feeling(affection)).Append("; ").Append(Knowing(familiarity))
                .Append(" (affection ").Append(affection.ToString("+0;-0;0", CultureInfo.InvariantCulture))
                .Append(", familiarity ").Append(familiarity.ToString("0", CultureInfo.InvariantCulture)).Append(").");
            if (!changesOn)
            {
                sb.Append("\nRelationship changes are switched off in settings.");
                return sb.ToString();
            }

            if (history == null || history.Count == 0)
                return sb.ToString();
            sb.Append("\nRecently:");
            for (var i = 0; i < history.Count && i < MaxReasons; i++)
                sb.Append("\n- ").Append(history.Line(i));
            return sb.ToString();
        }

        /// <summary>The prompt's affection bands, said about her instead of to her.</summary>
        public static string Feeling(float affection)
        {
            affection = RelationshipMath.ClampAffection(affection);
            if (affection <= -60f)
                return "she can't stand you right now";
            if (affection <= -20f)
                return "she is at odds with you";
            if (affection < 20f)
                return "she has no strong feelings about you yet";
            if (affection < 60f)
                return "she likes you";
            return "she adores you";
        }

        /// <summary>The prompt's familiarity bands, said about the two of you.</summary>
        public static string Knowing(float familiarity)
        {
            switch (KnowingBand(familiarity))
            {
                case 0:
                    return "you have only just met";
                case 1:
                    return "you know each other a little";
                case 2:
                    return "you know each other well";
                default:
                    return "you know each other very well";
            }
        }

        /// <summary>Which familiarity band this is: 0 just met, 1 a little, 2 well, 3 very well.</summary>
        public static int KnowingBand(float familiarity)
        {
            familiarity = RelationshipMath.ClampFamiliarity(familiarity);
            if (familiarity < 10f)
                return 0;
            if (familiarity < 35f)
                return 1;
            if (familiarity < 70f)
                return 2;
            return 3;
        }
    }
}
