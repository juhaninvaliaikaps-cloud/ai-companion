using System;
using System.Collections.Generic;
using System.Globalization;
using AICompanion.Core;

namespace AICompanion.Settings.Presenters
{
    /// <summary>What a "Do something" entry does.</summary>
    public enum DoSomethingKind
    {
        /// <summary>A 2D video reaction clip by name (IAvatarReactions.RequestReaction).</summary>
        Reaction = 0,
        /// <summary>A 2D walk test: Value is -1 (left) or +1 (right).</summary>
        Walk = 1,
        /// <summary>2D: stop walking.</summary>
        Stop = 2,
        /// <summary>2D: back to the front idle.</summary>
        Front = 3,
        /// <summary>3D: an emotion, the same as the debug keys 1-5.</summary>
        Emotion = 4,
    }

    /// <summary>One existing animation or reaction she can be asked to do.</summary>
    public readonly struct DoSomethingChoice
    {
        public readonly DoSomethingKind Kind;
        public readonly string Label;
        /// <summary>Reaction clip name for <see cref="DoSomethingKind.Reaction"/>.</summary>
        public readonly string Name;
        /// <summary>Walk direction, or the emotion for <see cref="DoSomethingKind.Emotion"/>.</summary>
        public readonly float Value;
        public readonly Emotion Emotion;

        public DoSomethingChoice(DoSomethingKind kind, string label, string name, float value, Emotion emotion)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Name = name ?? string.Empty;
            Value = value;
            Emotion = emotion;
        }
    }

    /// <summary>
    /// JUH-104 PR B: the 2D video avatar's test actions and reactions (S-AV-10, S-AV-11) and the
    /// wheel's "Do something" list. Only animations and reactions that already exist are offered:
    /// in 2D, the reaction clips the loaded folder has; in 3D, the debug emotions 1-5.
    /// </summary>
    public static class VideoAvatarPresenter
    {
        /// <summary>The reaction test buttons on the old Avatar tab, in the same order.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> TestReactions = new[]
        {
            new KeyValuePair<string, string>("Test: happy", "happy"),
            new KeyValuePair<string, string>("Test: concerned", "concerned"),
            new KeyValuePair<string, string>("Test: spin (turn360)", "turn360"),
            new KeyValuePair<string, string>("Test: embarrassed", "embarrassed"),
            new KeyValuePair<string, string>("Test: flustered", "flustered"),
            new KeyValuePair<string, string>("Test: pout", "pout"),
            new KeyValuePair<string, string>("Test: wink dance", "wink_dance"),
            new KeyValuePair<string, string>("Test: turn around (back)", "turn_around_back"),
        };

        public const string NotPlaying = "The 2D avatar is not playing.";

        /// <summary>
        /// The status line after a reaction request; empty when it simply started. Same wording
        /// as the old Avatar tab.
        /// </summary>
        public static string ReactionStatus(AvatarReactionResult result, string name, float pendingSeconds)
        {
            if (result == AvatarReactionResult.Refused)
                return "No playable \"" + name + "\" clip in the video folder.";
            if (result == AvatarReactionResult.Waiting)
                return "\"" + name + "\" plays when she is back on the front idle (within " + pendingSeconds.ToString("0", CultureInfo.InvariantCulture) + " s).";
            return string.Empty;
        }

        /// <summary>
        /// The "Do something" submenu. In 2D (video playing): the loaded reaction clips, then
        /// walk left, walk right, stop and front idle. In 3D: the five debug emotions.
        /// </summary>
        public static IReadOnlyList<DoSomethingChoice> DoSomething(bool videoPlaying, IReadOnlyList<string> loadedReactions)
        {
            var list = new List<DoSomethingChoice>();
            if (videoPlaying)
            {
                if (loadedReactions != null)
                {
                    for (var i = 0; i < loadedReactions.Count; i++)
                    {
                        var name = loadedReactions[i];
                        if (string.IsNullOrWhiteSpace(name))
                            continue;
                        list.Add(new DoSomethingChoice(DoSomethingKind.Reaction, ReactionLabel(name), name, 0f, Emotion.Neutral));
                    }
                }

                list.Add(new DoSomethingChoice(DoSomethingKind.Walk, "Walk left", string.Empty, -1f, Emotion.Neutral));
                list.Add(new DoSomethingChoice(DoSomethingKind.Walk, "Walk right", string.Empty, 1f, Emotion.Neutral));
                list.Add(new DoSomethingChoice(DoSomethingKind.Stop, "Stop", string.Empty, 0f, Emotion.Neutral));
                list.Add(new DoSomethingChoice(DoSomethingKind.Front, "Face me", string.Empty, 0f, Emotion.Neutral));
                return list;
            }

            list.Add(new DoSomethingChoice(DoSomethingKind.Emotion, "Neutral", string.Empty, 1f, Emotion.Neutral));
            list.Add(new DoSomethingChoice(DoSomethingKind.Emotion, "Happy", string.Empty, 1f, Emotion.Happy));
            list.Add(new DoSomethingChoice(DoSomethingKind.Emotion, "Sad", string.Empty, 1f, Emotion.Sad));
            list.Add(new DoSomethingChoice(DoSomethingKind.Emotion, "Surprised", string.Empty, 1f, Emotion.Surprised));
            list.Add(new DoSomethingChoice(DoSomethingKind.Emotion, "Thinking", string.Empty, 1f, Emotion.Thinking));
            return list;
        }

        /// <summary>"wink_dance" → "Wink dance", "turn360" → "Turn360".</summary>
        public static string ReactionLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var text = name.Trim().Replace('_', ' ');
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
