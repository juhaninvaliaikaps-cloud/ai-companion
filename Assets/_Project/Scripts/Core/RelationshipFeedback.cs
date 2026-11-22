using System;
using System.Globalization;

namespace AICompanion.Core
{
    /// <summary>
    /// One applied relationship change, for the floating pieces (JUH-122): the change, its landing
    /// name, and whether familiarity crossed into a new band.
    /// </summary>
    public struct RelationshipNotice
    {
        public RelationshipChange Change;

        /// <summary>The short name on the piece ("Compliment", "Brushed off"); empty for neutral.</summary>
        public string LandingName;

        /// <summary>True when familiarity crossed up into a band that was not shown yet.</summary>
        public bool NewBand;

        /// <summary>The familiarity band after the change (<see cref="RelationshipCard.KnowingBand"/>).</summary>
        public int Band;

        /// <summary>The secret this message found for the first time (JUH-123); empty for none.</summary>
        public string SecretFound;

        /// <summary>JUH-174: the found secret is one of her 18+ ones, so its chat line shows only while the gate is open.</summary>
        public bool SecretAdult;

        /// <summary>
        /// The notice for a change. <paramref name="announcedBand"/> is the band shown last; a new
        /// band counts only upwards (familiarity never drops).
        /// </summary>
        public static RelationshipNotice From(RelationshipChange change, int announcedBand, string secretFound = null, bool secretAdult = false)
        {
            var band = RelationshipCard.KnowingBand(change.Familiarity);
            var found = string.IsNullOrWhiteSpace(secretFound) ? string.Empty : secretFound.Trim();
            return new RelationshipNotice
            {
                Change = change,
                LandingName = RelationshipHistory.ShortName(change.Landing),
                NewBand = band > announcedBand,
                Band = band,
                SecretFound = found,
                SecretAdult = found.Length > 0 && secretAdult,
            };
        }
    }

    /// <summary>What a floating piece looks like.</summary>
    public enum FeedbackKind
    {
        /// <summary>A pink heart: affection went up.</summary>
        Up = 0,

        /// <summary>A grey heart: affection went down.</summary>
        Down = 1,

        /// <summary>Big and gold with a punch: a critical hit up.</summary>
        CritUp = 2,

        /// <summary>Big and grey: a critical drop.</summary>
        CritDown = 3,

        /// <summary>No icon, light blue: "You know each other well now".</summary>
        Familiarity = 4,

        /// <summary>A gold star: a secret found (JUH-123).</summary>
        Secret = 5,

        /// <summary>A magenta heart: her desire went up (JUH-170, 18+ only).</summary>
        DesireUp = 6,

        /// <summary>A dim violet heart: her desire went down, or a release spent it (JUH-170, 18+ only).</summary>
        DesireDown = 7,
    }

    /// <summary>One piece on screen: what it says, when it started, and when it was pushed out.</summary>
    public struct FeedbackPiece
    {
        /// <summary>
        /// Unique per piece (from 1), so a view sets a piece up once even when two pieces start in
        /// the same frame (a landing and a new band) or the pool shifts. 0 is "no piece".
        /// </summary>
        public int Id;

        public FeedbackKind Kind;

        /// <summary>The big number ("+1.5"), empty for a familiarity band or a secret.</summary>
        public string Amount;

        /// <summary>The name next to it ("Compliment", "You know each other well now").</summary>
        public string Label;

        public float StartTime;

        /// <summary>When a newer normal piece pushed this one out (it fades early); NaN while it is not.</summary>
        public float RetiredAt;

        /// <summary>JUH-174: 18+ (a desire change, or one of her 18+ secrets): its chat line shows only while the gate is open.</summary>
        public bool Adult;

        /// <summary>JUH-170: a desire piece, shown only while the 18+ gate is open.</summary>
        public bool IsDesire
        {
            get { return Kind == FeedbackKind.DesireUp || Kind == FeedbackKind.DesireDown; }
        }

        public bool IsCritical
        {
            get { return Kind == FeedbackKind.CritUp || Kind == FeedbackKind.CritDown; }
        }

        /// <summary>
        /// JUH-169: the piece for Player.log. A found secret's text (an 18+ kink, say) stays out of
        /// the log, as do the private and 18+ chats it came from: only that a secret was found.
        /// </summary>
        public string LogText
        {
            get
            {
                var label = Kind == FeedbackKind.Secret ? "secret" : Label ?? string.Empty;
                return Kind + " " + (!string.IsNullOrEmpty(Amount) ? Amount + " " : string.Empty) + label;
            }
        }
    }

    /// <summary>
    /// The rules for the floating pieces near her (JUH-122, docs/PISARA.md "Floating changes").
    /// Plain C#, no scene:
    /// <list type="bullet">
    /// <item>A landing shows when its affection change is <see cref="MinShown"/> or more; a critical
    /// hit always shows.</item>
    /// <item>Familiarity shows only when it crosses into a new band.</item>
    /// <item>At most <see cref="MaxNormalOnScreen"/> normal pieces are on screen; a new one pushes
    /// the oldest out early. Crits are not counted and never pushed out.</item>
    /// <item>Each piece rises about <see cref="RisePixels"/> and fades out in <see cref="LifeSeconds"/>;
    /// a crit punches from <see cref="PunchFrom"/> down to 1 first.</item>
    /// </list>
    /// The queue is a fixed array, so adding and ticking never allocate; only the label text of a
    /// new piece is built.
    /// </summary>
    public sealed class RelationshipFeedback
    {
        public const float MinShown = 0.5f;
        public const int MaxNormalOnScreen = 2;
        public const int Capacity = 6;

        public const float LifeSeconds = 1.5f;
        public const float CritLifeSeconds = 2.2f;
        public const float RisePixels = 30f;
        public const float CritRisePixels = 36f;

        /// <summary>The last part of a piece's life is its fade.</summary>
        public const float FadeShare = 0.4f;

        /// <summary>A piece pushed out by a newer one fades over this long.</summary>
        public const float RetireSeconds = 0.25f;

        public const float PunchFrom = 1.6f;
        public const float PunchSeconds = 0.3f;

        readonly FeedbackPiece[] _pieces = new FeedbackPiece[Capacity];
        int _count;
        int _nextId = 1;

        public int Count
        {
            get { return _count; }
        }

        /// <summary>The piece at <paramref name="index"/>, oldest first.</summary>
        public FeedbackPiece Piece(int index)
        {
            return _pieces[index];
        }

        public void Clear()
        {
            _count = 0;
        }

        /// <summary>
        /// JUH-170: drops the desire pieces (<paramref name="desire"/> true) or all the others, at
        /// once and without allocating: desire pieces go the moment the 18+ gate closes, the others
        /// when the relationship setting goes off.
        /// </summary>
        public void Drop(bool desire)
        {
            var write = 0;
            for (var read = 0; read < _count; read++)
            {
                if (_pieces[read].IsDesire == desire)
                    continue;
                _pieces[write++] = _pieces[read];
            }

            _count = write;
        }

        /// <summary>True when any piece of that family is showing.</summary>
        public bool Has(bool desire)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_pieces[i].IsDesire == desire)
                    return true;
            }

            return false;
        }

        /// <summary>The smallest desire change that gets a piece, unless it is a release.</summary>
        public const float DesireMinShown = 0.5f;

        /// <summary>
        /// JUH-170: one desire change: "+8 Desire 48" (magenta) or "-3 Desire 41"; a release is
        /// "Release · desire 7". Nothing while <paramref name="enabled"/> is false (the gate is
        /// closed or the floating changes are off). Returns true when a piece was added.
        /// </summary>
        public bool AddDesire(DesireNotice notice, float now, bool enabled)
        {
            if (!enabled || float.IsNaN(notice.Delta))
                return false;
            if (!notice.Release && Math.Abs(notice.Delta) < DesireMinShown)
                return false;
            Push(new FeedbackPiece
            {
                Kind = notice.Delta > 0f && !notice.Release ? FeedbackKind.DesireUp : FeedbackKind.DesireDown,
                Amount = notice.Release ? string.Empty : DesireAmountText(notice.Delta),
                Label = DesireText(notice.Desire, notice.Release),
                Adult = true,
                StartTime = now,
                RetiredAt = float.NaN,
            }, now);
            return true;
        }

        /// <summary>
        /// JUH-174: the readable line the chat keeps for a piece after it has floated away:
        /// "+1.5 affection · Compliment", "Critical! +3.0 affection · Compliment",
        /// "Secret found: …", "You know each other well now", "+8 desire (now 48)".
        /// </summary>
        public static string ChatText(FeedbackPiece piece)
        {
            var label = piece.Label ?? string.Empty;
            var amount = piece.Amount ?? string.Empty;
            switch (piece.Kind)
            {
                case FeedbackKind.Up:
                case FeedbackKind.Down:
                    return amount + " affection · " + label;
                case FeedbackKind.CritUp:
                case FeedbackKind.CritDown:
                    return "Critical! " + amount + " affection · " + label;
                case FeedbackKind.DesireUp:
                case FeedbackKind.DesireDown:
                    if (amount.Length == 0)
                        return label;
                    var now = label.StartsWith("Desire ", StringComparison.Ordinal) ? label.Substring("Desire ".Length) : label;
                    return amount + " desire (now " + now + ")";
                default:
                    return label;
            }
        }

        /// <summary>"+8", "-3": whole numbers, always signed (a change under 1 shows one decimal).</summary>
        public static string DesireAmountText(float delta)
        {
            return Math.Abs(delta) < 1f
                ? delta.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)
                : delta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        }

        /// <summary>"Desire 48", or "Release · desire 7".</summary>
        public static string DesireText(float desire, bool release)
        {
            var value = DesireMath.Clamp(desire).ToString("0", CultureInfo.InvariantCulture);
            return release ? "Release · desire " + value : "Desire " + value;
        }

        // ---- Which changes show ---------------------------------------------------------------

        /// <summary>True when the affection part of this change gets a piece.</summary>
        public static bool ShowsAffection(RelationshipChange change)
        {
            if (change.Landing == LandingEvent.Neutral)
                return false;
            if (change.Critical && change.AffectionDelta != 0f)
                return true;
            return Math.Abs(change.AffectionDelta) >= MinShown;
        }

        public static FeedbackKind KindFor(RelationshipChange change)
        {
            var up = change.AffectionDelta > 0f;
            if (change.Critical)
                return up ? FeedbackKind.CritUp : FeedbackKind.CritDown;
            return up ? FeedbackKind.Up : FeedbackKind.Down;
        }

        /// <summary>
        /// Adds the pieces for one applied change: its affection piece, its band piece, both or
        /// neither. Returns how many were added. Nothing shows while <paramref name="enabled"/>
        /// is false (the setting is off, or "Relationship changes" is off).
        /// </summary>
        public int Add(RelationshipNotice notice, float now, bool enabled)
        {
            if (!enabled)
                return 0;
            var added = 0;
            var change = notice.Change;
            if (ShowsAffection(change))
            {
                Push(new FeedbackPiece
                {
                    Kind = KindFor(change),
                    Amount = AmountText(change.AffectionDelta),
                    Label = string.IsNullOrEmpty(notice.LandingName) ? "affection" : notice.LandingName,
                    StartTime = now,
                    RetiredAt = float.NaN,
                }, now);
                added++;
            }

            // JUH-123: a secret found comes with the affection piece, after it.
            if (AddSecret(notice.SecretFound, now, true, notice.SecretAdult))
                added++;

            if (notice.NewBand && notice.Band > 0)
            {
                Push(new FeedbackPiece
                {
                    Kind = FeedbackKind.Familiarity,
                    Amount = string.Empty,
                    Label = BandText(notice.Band),
                    StartTime = now,
                    RetiredAt = float.NaN,
                }, now);
                added++;
            }

            return added;
        }

        /// <summary>
        /// The secret-found piece (JUH-123), "Secret found: being trusted". It counts as a normal
        /// piece. <see cref="Add"/> adds it for a notice that found one.
        /// </summary>
        public bool AddSecret(string what, float now, bool enabled, bool adult = false)
        {
            if (!enabled || string.IsNullOrWhiteSpace(what))
                return false;
            Push(new FeedbackPiece
            {
                Kind = FeedbackKind.Secret,
                Amount = string.Empty,
                Label = SecretText(what),
                    Adult = adult,
                StartTime = now,
                RetiredAt = float.NaN,
            }, now);
            return true;
        }

        /// <summary>Drops the pieces that are done. Call once per frame; never allocates.</summary>
        public void Tick(float now)
        {
            var write = 0;
            for (var read = 0; read < _count; read++)
            {
                if (IsDone(_pieces[read], now))
                    continue;
                _pieces[write++] = _pieces[read];
            }

            _count = write;
        }

        /// <summary>How many normal pieces are on screen and not pushed out yet.</summary>
        public int NormalShown
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _count; i++)
                {
                    if (!_pieces[i].IsCritical && float.IsNaN(_pieces[i].RetiredAt))
                        n++;
                }

                return n;
            }
        }

        void Push(FeedbackPiece piece, float now)
        {
            if (!piece.IsCritical)
            {
                // At most two normal pieces: the oldest one still showing fades out early.
                while (NormalShown >= MaxNormalOnScreen)
                {
                    for (var i = 0; i < _count; i++)
                    {
                        if (!_pieces[i].IsCritical && float.IsNaN(_pieces[i].RetiredAt))
                        {
                            _pieces[i].RetiredAt = now;
                            break;
                        }
                    }
                }
            }

            if (_count == Capacity)
            {
                // Full (a burst of crits): the oldest goes at once.
                for (var i = 1; i < _count; i++)
                    _pieces[i - 1] = _pieces[i];
                _count--;
            }

            piece.Id = _nextId++;
            _pieces[_count++] = piece;
        }

        /// <summary>
        /// True when a view slot that last set up <paramref name="shownId"/> must set up this piece
        /// (text, colours, layout). False on every later frame of the same piece, so a piece is set
        /// up once and only moved after that.
        /// </summary>
        public static bool NeedsSetup(int shownId, FeedbackPiece piece)
        {
            return shownId != piece.Id;
        }

        // ---- The text ---------------------------------------------------------------------------

        /// <summary>"+1.5", "-2.4": one decimal, always signed.</summary>
        public static string AmountText(float delta)
        {
            return delta.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>"You know each other well now" for bands 1 to 3.</summary>
        public static string BandText(int band)
        {
            switch (band)
            {
                case 1:
                    return "You know each other a little now";
                case 2:
                    return "You know each other well now";
                case 3:
                    return "You know each other very well now";
                default:
                    return string.Empty;
            }
        }

        public static string SecretText(string what)
        {
            return "Secret found: " + (what ?? string.Empty).Trim();
        }

        /// <summary>The small caption over a crit's name.</summary>
        public const string CriticalLabel = "CRITICAL";

        // ---- The motion ---------------------------------------------------------------------

        public static float Life(FeedbackPiece piece)
        {
            return piece.IsCritical ? CritLifeSeconds : LifeSeconds;
        }

        static bool IsDone(FeedbackPiece piece, float now)
        {
            if (!float.IsNaN(piece.RetiredAt) && now - piece.RetiredAt >= RetireSeconds)
                return true;
            return now - piece.StartTime >= Life(piece);
        }

        /// <summary>How far up the piece has risen, in pixels: fast at first, easing out.</summary>
        public static float Rise(FeedbackPiece piece, float now)
        {
            var t = Progress(piece, now);
            var eased = 1f - (1f - t) * (1f - t);
            return eased * (piece.IsCritical ? CritRisePixels : RisePixels);
        }

        /// <summary>
        /// Opacity: full, then a linear fade over the last <see cref="FadeShare"/> of its life; a
        /// piece pushed out fades over <see cref="RetireSeconds"/> from wherever it was.
        /// </summary>
        public static float Alpha(FeedbackPiece piece, float now)
        {
            var t = Progress(piece, now);
            var alpha = t <= 1f - FadeShare ? 1f : Clamp01((1f - t) / FadeShare);
            if (!float.IsNaN(piece.RetiredAt))
                alpha = Math.Min(alpha, Clamp01(1f - (now - piece.RetiredAt) / RetireSeconds));
            return alpha;
        }

        /// <summary>
        /// Scale: a crit starts at <see cref="PunchFrom"/> and settles to 1 over
        /// <see cref="PunchSeconds"/> (easing out); a normal piece is 1.
        /// </summary>
        public static float Punch(FeedbackPiece piece, float now)
        {
            if (!piece.IsCritical)
                return 1f;
            var t = Clamp01((now - piece.StartTime) / PunchSeconds);
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            return PunchFrom + (1f - PunchFrom) * eased;
        }

        static float Progress(FeedbackPiece piece, float now)
        {
            return Clamp01((now - piece.StartTime) / Life(piece));
        }

        static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
