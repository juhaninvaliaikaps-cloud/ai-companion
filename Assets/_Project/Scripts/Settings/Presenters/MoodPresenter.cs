using System;
using AICompanion.Core;

namespace AICompanion.Settings.Presenters
{
    /// <summary>
    /// JUH-104 PR B: her mood in words for the wheel's Mood slice and the mood debug toggle
    /// (S-OV-08). Reads the JUH-103 mood state; it never changes it.
    /// </summary>
    public static class MoodPresenter
    {
        public const float StrongAt = 0.6f;
        public const float NoticeableAt = 0.3f;

        /// <summary>"She feels happy (strongly)." plus why, from the three strongest moods.</summary>
        public static string Describe(CompanionMood shown, float strength, string top3, bool intimateAllowed, float localHour)
        {
            if (shown == CompanionMood.Intimate && !intimateAllowed)
                shown = CompanionMood.Content;

            var name = CompanionMoodNames.Name(shown);
            string head;
            if (shown == CompanionMood.Content)
                head = "She feels content.";
            else
                head = "She feels " + name + " (" + StrengthWord(strength) + ").";

            var why = Why(shown, strength, localHour);
            var detail = string.IsNullOrWhiteSpace(top3) ? string.Empty : " Strongest moods: " + top3.Trim();
            if (!intimateAllowed && detail.IndexOf(CompanionMoodNames.Name(CompanionMood.Intimate), StringComparison.OrdinalIgnoreCase) >= 0)
                detail = string.Empty;
            return head + (why.Length > 0 ? " " + why : string.Empty) + detail;
        }

        public static string StrengthWord(float strength)
        {
            if (strength >= StrongAt)
                return "strongly";
            if (strength >= NoticeableAt)
                return "noticeably";
            return "a little";
        }

        /// <summary>A short reason for the mood she shows. Simple on purpose: the mood state keeps no history.</summary>
        public static string Why(CompanionMood shown, float strength, float localHour)
        {
            switch (shown)
            {
                case CompanionMood.Sleepy:
                    return CompanionMoodTuning.NightSleepyLevel(localHour) > 0f
                        ? "It is late, and the night makes her sleepy."
                        : "She has been quiet for a while.";
                case CompanionMood.Happy:
                case CompanionMood.Affectionate:
                    return "Recent replies and your messages lifted her mood.";
                case CompanionMood.Annoyed:
                    return "Something in the conversation annoyed her. It fades over time.";
                case CompanionMood.Sad:
                case CompanionMood.Anxious:
                    return "The conversation weighed on her. It fades over time.";
                case CompanionMood.Flustered:
                    return "Something made her flustered.";
                default:
                    return string.Empty;
            }
        }
    }
}
