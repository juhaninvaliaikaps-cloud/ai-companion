using System;
using System.Collections.Generic;
using System.Globalization;

namespace AICompanion.Core
{
    /// <summary>One labelled bar in the Profile panel, filled 0 to 1.</summary>
    public struct ProfileBar
    {
        public string Label;
        public string Value;
        public float Fill;
    }

    /// <summary>
    /// JUH-121, Profile → Feelings (Jussi picked option A, gauges): how she feels about the
    /// user, how well they know each other, her mood, and why the relationship last moved.
    /// Plain data for the panel; no Unity objects.
    /// </summary>
    public sealed class ProfileFeelings
    {
        public const int MaxReasons = 4;

        public string AffectionValue = string.Empty;
        /// <summary>Where the affection marker sits on the bar, 0 (−100) to 1 (+100).</summary>
        public float AffectionMarker = 0.5f;
        public string FamiliarityValue = string.Empty;
        public float FamiliarityFill;
        public string MoodTitle = string.Empty;
        public readonly List<ProfileBar> Moods = new List<ProfileBar>(3);
        public readonly List<string> Reasons = new List<string>(MaxReasons);
        /// <summary>Shown instead of the reasons when there are none, or when relationship changes are off.</summary>
        public string ReasonsNote = string.Empty;

        public static ProfileFeelings Build(float affection, float familiarity, CompanionMood shown, CompanionMood[] top, float[] strengths, RelationshipHistory history, bool changesOn)
        {
            affection = RelationshipMath.ClampAffection(affection);
            familiarity = RelationshipMath.ClampFamiliarity(familiarity);
            var view = new ProfileFeelings
            {
                AffectionValue = affection.ToString("+0;-0;0", CultureInfo.InvariantCulture) + " · " + RelationshipCard.Feeling(affection),
                AffectionMarker = (affection + 100f) / 200f,
                FamiliarityValue = familiarity.ToString("0", CultureInfo.InvariantCulture) + " · " + RelationshipCard.Knowing(familiarity),
                FamiliarityFill = familiarity / 100f,
                MoodTitle = "Mood now: " + Capital(CompanionMoodNames.Name(shown)),
            };

            if (top != null && strengths != null)
            {
                for (var i = 0; i < top.Length && i < strengths.Length && i < 3; i++)
                {
                    if (strengths[i] <= 0f)
                        break;
                    var fill = Math.Max(0f, Math.Min(1f, strengths[i]));
                    view.Moods.Add(new ProfileBar
                    {
                        Label = Capital(CompanionMoodNames.Name(top[i])),
                        Value = Math.Round(fill * 100f).ToString("0", CultureInfo.InvariantCulture) + "%",
                        Fill = fill,
                    });
                }
            }

            if (!changesOn)
            {
                view.ReasonsNote = "Relationship changes are switched off in settings.";
                return view;
            }

            if (history != null)
            {
                for (var i = 0; i < history.Count && i < MaxReasons; i++)
                    view.Reasons.Add(history.Line(i));
            }

            if (view.Reasons.Count == 0)
                view.ReasonsNote = "Nothing has moved yet this session.";
            return view;
        }

        public static string Capital(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }

    /// <summary>
    /// JUH-121, Profile → Personality: her temperament as five axes for the radar, the
    /// reading's line about it, and (18+ only) where she sits between Innocent and Perverted.
    /// Her likes and dislikes stay out of it until JUH-123 makes them something to discover.
    /// </summary>
    public sealed class ProfilePersonality
    {
        /// <summary>The radar's axes, clockwise from the top. Same order as the mockup.</summary>
        public static readonly string[] AxisNames = { "Calm", "Cheerful", "Shy", "Tsundere", "Moody" };

        public readonly float[] Axes = new float[5];
        public bool HasReading;
        public string Title = string.Empty;
        public string Reason = string.Empty;
        public bool ShowInnocence;
        public float Innocence = InnocenceMath.Default;
        public string InnocenceValue = string.Empty;

        public static ProfilePersonality Build(CharacterSheet sheet, string temperamentLine, bool gateOpen, float innocence)
        {
            var view = new ProfilePersonality
            {
                Title = string.IsNullOrWhiteSpace(temperamentLine) ? "Temperament" : temperamentLine.Trim(),
            };

            var t = sheet != null ? sheet.temperament : null;
            if (t != null)
            {
                view.Axes[0] = Unit(t.calm);
                view.Axes[1] = Unit(t.cheerful);
                view.Axes[2] = Unit(t.shy);
                view.Axes[3] = Unit(t.tsundere);
                view.Axes[4] = Unit(t.moody);
                view.Reason = (t.reason ?? string.Empty).Trim();
                for (var i = 0; i < view.Axes.Length; i++)
                {
                    if (view.Axes[i] > 0f)
                        view.HasReading = true;
                }
            }

            if (!view.HasReading)
                view.Reason = "Her description has not been read yet.";

            if (gateOpen)
            {
                view.ShowInnocence = true;
                view.Innocence = InnocenceMath.Clamp(innocence);
                view.InnocenceValue = view.Innocence.ToString("0", CultureInfo.InvariantCulture) + " · " + InnocenceMath.BandName(view.Innocence);
            }

            return view;
        }

        static float Unit(float value)
        {
            if (float.IsNaN(value))
                return 0f;
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    /// <summary>JUH-121: the line under her name in the Profile header.</summary>
    public static class ProfileHeader
    {
        public static string RoleLine(SheetRelationship relationship)
        {
            if (relationship == null)
                return string.Empty;
            var role = RelationshipRoles.Normalize(relationship.role);
            var roleWords = role.Replace('_', ' ');
            var label = (relationship.label ?? string.Empty).Trim();
            var line = label.Length > 0 && !string.Equals(label, roleWords, StringComparison.OrdinalIgnoreCase)
                ? ProfileFeelings.Capital(label) + " (" + roleWords + ")"
                : ProfileFeelings.Capital(roleWords);
            if (relationship.sharedHistory && role != RelationshipRoles.Stranger)
                line += " · you share a past";
            return line;
        }
    }
}
