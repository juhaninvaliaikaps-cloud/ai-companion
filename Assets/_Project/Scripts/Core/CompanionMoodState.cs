using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Numbers that shape her mood. Kept together so they are easy to tune.
    /// </summary>
    public static class CompanionMoodTuning
    {
        /// <summary>A mood needs this much strength before it can show instead of Content.</summary>
        public const float EnterStrength = 0.3f;

        /// <summary>The shown mood stays until its strength falls below this (lower than <see cref="EnterStrength"/>, so it does not flicker at the edge).</summary>
        public const float LeaveStrength = 0.15f;

        /// <summary>A different mood replaces the shown one only when it is stronger by this much.</summary>
        public const float SwitchMargin = 0.1f;

        /// <summary>
        /// Seconds for a mood's strength to halve while nothing feeds it. Annoyed and Sad
        /// fade slower, Happy faster. Index by <see cref="CompanionMood"/>; Content is unused.
        /// </summary>
        static readonly float[] HalfLifeSeconds =
        {
            0f,      // Content
            180f,    // Happy: 3 minutes
            420f,    // Affectionate: 7 minutes
            240f,    // Flustered: 4 minutes
            900f,    // Annoyed: 15 minutes
            1200f,   // Sad: 20 minutes
            420f,    // Anxious: 7 minutes
            1800f,   // Sleepy: 30 minutes (outside the night)
            600f,    // Intimate: 10 minutes
        };

        public static float HalfLife(CompanionMood mood)
        {
            var index = (int)mood;
            return index > 0 && index < HalfLifeSeconds.Length ? HalfLifeSeconds[index] : 0f;
        }

        /// <summary>Sleepy rises toward this late at night (local clock).</summary>
        public const float NightSleepyMax = 0.6f;

        /// <summary>Seconds Sleepy takes to cover about two thirds of the way to its night level.</summary>
        public const float NightSleepySeconds = 1200f;

        /// <summary>
        /// How sleepy the hour of the night makes her: 0 in the day, rising from 22:00 to
        /// <see cref="NightSleepyMax"/> at 01:00, held until 05:00, then 0 again.
        /// </summary>
        public static float NightSleepyLevel(float localHour)
        {
            if (float.IsNaN(localHour))
                return 0f;
            var hour = localHour % 24f;
            if (hour < 0f)
                hour += 24f;
            if (hour >= 22f)
                return NightSleepyMax * (hour - 22f) / 3f;
            if (hour < 1f)
                return NightSleepyMax * (hour + 2f) / 3f;
            if (hour < 5f)
                return NightSleepyMax;
            return 0f;
        }

        /// <summary>Time closed longer than this is treated as this when the mood is restored.</summary>
        public const double MaxRestoreSeconds = 7 * 24 * 3600;
    }

    /// <summary>
    /// Her lasting mood: a strength per mood, decay toward Content, and the one mood that
    /// shows. Pure logic: the caller supplies the time and the inputs.
    /// </summary>
    public sealed class CompanionMoodState
    {
        public const int FileVersion = 1;

        readonly float[] _strength = new float[CompanionMoodNames.Count];
        bool _intimateAllowed;
        PersonaTemperament _temperament = PersonaTemperament.Default;

        /// <summary>
        /// The persona's temperament (JUH-103 phase 3). It scales half-lives and keeps baseline
        /// and bias floors in <see cref="Tick"/>, and <see cref="CompanionMoodTable"/> scales
        /// pushes by it. The default is exactly the phase 1/2 behaviour. Not saved in the mood
        /// file: it belongs to the persona. Setting it lifts every mood to its rest floor at
        /// once, so a Cheerful persona starts on her baseline instead of climbing to it.
        /// </summary>
        public PersonaTemperament Temperament
        {
            get { return _temperament; }
            set
            {
                _temperament = value ?? PersonaTemperament.Default;
                if (LiftToFloors())
                    UpdateShown();
            }
        }

        /// <summary>Raises every mood below its temperament floor to the floor. True when one moved.</summary>
        bool LiftToFloors()
        {
            var lifted = false;
            for (var i = 1; i < _strength.Length; i++)
            {
                var floor = _temperament.RestFloor((CompanionMood)i);
                if (floor > _strength[i])
                {
                    _strength[i] = floor;
                    lifted = true;
                }
            }

            return lifted;
        }

        float HalfLife(int index)
        {
            var mood = (CompanionMood)index;
            return CompanionMoodTuning.HalfLife(mood) * _temperament.HalfLifeScale(mood);
        }

        /// <summary>The mood that shows now. Content when no mood is strong enough.</summary>
        public CompanionMood Shown { get; private set; }

        public float Strength(CompanionMood mood)
        {
            var index = (int)mood;
            return index > 0 && index < _strength.Length ? _strength[index] : 0f;
        }

        /// <summary>
        /// Intimate may only gain strength while this is true (<see cref="IntimateExpressions.Active"/>).
        /// Turning it off clears Intimate at once. Returns true when the shown mood changed.
        /// </summary>
        public bool SetIntimateAllowed(bool allowed)
        {
            _intimateAllowed = allowed;
            if (allowed)
                return false;
            _strength[(int)CompanionMood.Intimate] = 0f;
            return UpdateShown();
        }

        public bool IntimateAllowed
        {
            get { return _intimateAllowed; }
        }

        /// <summary>
        /// Adds to one mood. Repeated pushes add up but never pass 1: each push covers
        /// <paramref name="amount"/> of the distance left. Returns true when the shown mood changed.
        /// </summary>
        public bool Push(CompanionMood mood, float amount)
        {
            var index = (int)mood;
            if (index <= 0 || index >= _strength.Length || float.IsNaN(amount) || amount <= 0f)
                return false;
            if (mood == CompanionMood.Intimate && !_intimateAllowed)
                return false;

            amount = Mathf.Clamp01(amount);
            _strength[index] = Mathf.Clamp01(_strength[index] + amount * (1f - _strength[index]));
            return UpdateShown();
        }

        /// <summary>
        /// Time passes: every mood fades toward 0 at its own rate, and late at night Sleepy
        /// rises toward the night level instead. Returns true when the shown mood changed.
        /// </summary>
        public bool Tick(float seconds, float localHour)
        {
            if (float.IsNaN(seconds) || seconds <= 0f)
                return false;

            var night = CompanionMoodTuning.NightSleepyLevel(localHour);
            var sleepy = (int)CompanionMood.Sleepy;
            for (var i = 1; i < _strength.Length; i++)
            {
                // Below the night level Sleepy does not fade: the night pulls it up instead.
                if (i == sleepy && night > _strength[i])
                    continue;
                // A mood fades down to its temperament floor (baseline or bias), never below it.
                var floor = _temperament.RestFloor((CompanionMood)i);
                if (floor > _strength[i])
                    continue;
                var halfLife = HalfLife(i);
                if (halfLife > 0f && _strength[i] > 0f)
                    _strength[i] *= Mathf.Pow(0.5f, seconds / halfLife);
                if (_strength[i] < floor)
                    _strength[i] = floor;
                if (_strength[i] < 0.0001f)
                    _strength[i] = 0f;
            }

            if (night > _strength[sleepy])
            {
                var approach = 1f - Mathf.Exp(-seconds / CompanionMoodTuning.NightSleepySeconds);
                _strength[sleepy] += (night - _strength[sleepy]) * approach;
            }

            // A mood still below its floor (only from an older mood file) is pulled up.
            for (var i = 1; i < _strength.Length; i++)
            {
                var floor = _temperament.RestFloor((CompanionMood)i);
                if (floor > _strength[i])
                {
                    var pull = 1f - Mathf.Exp(-seconds / PersonaTemperament.RestPullSeconds);
                    _strength[i] += (floor - _strength[i]) * pull;
                }
            }

            if (!_intimateAllowed)
                _strength[(int)CompanionMood.Intimate] = 0f;
            return UpdateShown();
        }

        /// <summary>
        /// The app was closed: fade every mood for that time (capped at a week). The night
        /// rise is not replayed. Returns true when the shown mood changed.
        /// </summary>
        public bool ApplyClosedTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0)
                return false;
            seconds = Math.Min(seconds, CompanionMoodTuning.MaxRestoreSeconds);
            for (var i = 1; i < _strength.Length; i++)
            {
                var halfLife = HalfLife(i);
                if (halfLife > 0f && _strength[i] > 0f)
                    _strength[i] = (float)(_strength[i] * Math.Pow(0.5, seconds / halfLife));
                if (_strength[i] < 0.0001f)
                    _strength[i] = 0f;
            }

            // Time away is rest: every mood ends at least on its temperament floor.
            LiftToFloors();

            return UpdateShown();
        }

        /// <summary>
        /// The three strongest moods with any strength, strongest first. Unused slots are
        /// Content with 0. Intimate is in it only while it is allowed and has strength.
        /// </summary>
        public void Top3(CompanionMood[] moods, float[] strengths)
        {
            for (var slot = 0; slot < 3; slot++)
            {
                moods[slot] = CompanionMood.Content;
                strengths[slot] = 0f;
            }

            for (var i = 1; i < _strength.Length; i++)
            {
                var value = _strength[i];
                if (value <= 0f)
                    continue;
                for (var slot = 0; slot < 3; slot++)
                {
                    if (value <= strengths[slot])
                        continue;
                    for (var move = 2; move > slot; move--)
                    {
                        moods[move] = moods[move - 1];
                        strengths[move] = strengths[move - 1];
                    }

                    moods[slot] = (CompanionMood)i;
                    strengths[slot] = value;
                    break;
                }
            }
        }

        /// <summary>One line for the log and the readout, for example "happy 0.62, annoyed 0.20, sad 0.10".</summary>
        public string DescribeTop3()
        {
            var moods = new CompanionMood[3];
            var strengths = new float[3];
            Top3(moods, strengths);
            var text = new StringBuilder();
            for (var i = 0; i < 3; i++)
            {
                if (strengths[i] <= 0f)
                    break;
                if (text.Length > 0)
                    text.Append(", ");
                text.Append(CompanionMoodNames.Name(moods[i])).Append(' ')
                    .Append(strengths[i].ToString("0.00", CultureInfo.InvariantCulture));
            }

            return text.Length > 0 ? text.ToString() : "none";
        }

        bool UpdateShown()
        {
            var best = CompanionMood.Content;
            var bestValue = 0f;
            for (var i = 1; i < _strength.Length; i++)
            {
                if (_strength[i] > bestValue)
                {
                    best = (CompanionMood)i;
                    bestValue = _strength[i];
                }
            }

            var shown = Shown;
            var shownValue = Strength(shown);
            CompanionMood next;
            if (shown != CompanionMood.Content && shownValue < CompanionMoodTuning.LeaveStrength)
            {
                // The shown mood has faded: the strongest other one, if it is strong enough.
                next = bestValue >= CompanionMoodTuning.EnterStrength ? best : CompanionMood.Content;
            }
            else if (best != shown && bestValue >= CompanionMoodTuning.EnterStrength
                && bestValue >= shownValue + CompanionMoodTuning.SwitchMargin)
            {
                next = best;
            }
            else
            {
                next = shown;
            }

            if (next == CompanionMood.Intimate && !_intimateAllowed)
                next = CompanionMood.Content;
            if (next == shown)
                return false;
            Shown = next;
            return true;
        }

        // ---- the mood file ------------------------------------------------------------------

        [Serializable]
        class MoodFile
        {
            public int version;
            public long savedUtcTicks;
            public string shown;
            public string[] moods;
            public float[] strengths;
        }

        /// <summary>
        /// The mood as JSON, for the encrypted per-persona file. Intimate is never written:
        /// it is a Private mode state and starts at 0 next time.
        /// </summary>
        public string ToJson(DateTime savedUtc)
        {
            var file = new MoodFile
            {
                version = FileVersion,
                savedUtcTicks = savedUtc.ToUniversalTime().Ticks,
                shown = CompanionMoodNames.Name(Shown == CompanionMood.Intimate ? CompanionMood.Content : Shown),
                moods = new string[CompanionMoodNames.Count - 2],
                strengths = new float[CompanionMoodNames.Count - 2],
            };

            var slot = 0;
            for (var i = 1; i < _strength.Length; i++)
            {
                if ((CompanionMood)i == CompanionMood.Intimate)
                    continue;
                file.moods[slot] = CompanionMoodNames.Name((CompanionMood)i);
                file.strengths[slot] = _strength[i];
                slot++;
            }

            return JsonUtility.ToJson(file, true);
        }

        /// <summary>
        /// Reads a mood file. Unknown names, broken numbers and Intimate are ignored. The time
        /// since it was saved fades the moods (<see cref="ApplyClosedTime"/>). Returns null
        /// when the text is not a mood file, so the caller starts at Content.
        /// </summary>
        public static CompanionMoodState FromJson(string json, DateTime nowUtc)
        {
            return FromJson(json, nowUtc, null);
        }

        /// <summary>
        /// As <see cref="FromJson(string, DateTime)"/>, with the persona's temperament set before
        /// the closed time is faded, so its half-lives apply to the time away too.
        /// </summary>
        public static CompanionMoodState FromJson(string json, DateTime nowUtc, PersonaTemperament temperament)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            MoodFile file;
            try
            {
                file = JsonUtility.FromJson<MoodFile>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (file == null || file.version < 1 || file.moods == null || file.strengths == null
                || file.moods.Length == 0 || file.strengths.Length == 0)
                return null;

            var state = new CompanionMoodState { Temperament = temperament };
            var count = Math.Min(file.moods.Length, file.strengths.Length);
            for (var i = 0; i < count; i++)
            {
                CompanionMood mood;
                if (!CompanionMoodNames.TryParse(file.moods[i], out mood) || mood == CompanionMood.Content || mood == CompanionMood.Intimate)
                    continue;
                var value = file.strengths[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    continue;
                state._strength[(int)mood] = Mathf.Clamp01(value);
            }

            CompanionMood shown;
            if (CompanionMoodNames.TryParse(file.shown, out shown) && shown != CompanionMood.Intimate)
                state.Shown = shown;

            if (file.savedUtcTicks > 0 && file.savedUtcTicks <= DateTime.MaxValue.Ticks)
            {
                var closed = (nowUtc.ToUniversalTime() - new DateTime(file.savedUtcTicks, DateTimeKind.Utc)).TotalSeconds;
                if (closed > 0)
                    state.ApplyClosedTime(closed);
            }

            // Strengths from the file may sit below the persona's floors (an older file).
            state.LiftToFloors();
            state.UpdateShown();
            return state;
        }
    }
}
