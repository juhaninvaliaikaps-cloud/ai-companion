using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>What happened to a reaction request.</summary>
    public enum AvatarReactionResult
    {
        /// <summary>No such clip, or it cannot be played (failed to load, a held pose).</summary>
        Refused = 0,

        /// <summary>She is on the default idle: it plays as soon as the clip is loaded.</summary>
        Started = 1,

        /// <summary>She is busy (walking, a variation, another reaction): it plays when she is back on the default idle, if that is soon.</summary>
        Waiting = 2,
    }

    /// <summary>
    /// The avatar that can play body reaction clips by name. The 2D video avatar
    /// implements it. The animation director calls it for a cue's <c>reaction</c> attribute.
    /// </summary>
    public interface IAvatarReactions
    {
        AvatarReactionResult RequestReaction(string name);
    }

    /// <summary>
    /// The reaction clips the companion may ask for right now, for the system prompt.
    /// The 2D video avatar fills it from its loaded clip folder and clears it when it
    /// stops playing, so 3D mode offers none and new clips are offered as soon as they load.
    /// </summary>
    public static class AvatarReactionCatalog
    {
        public const int MaxNames = 24;

        static readonly List<string> _names = new List<string>();

        public static IReadOnlyList<string> Names
        {
            get { return _names; }
        }

        /// <summary>Replaces the list. Names are cleaned to cue tokens; duplicates and empties are dropped.</summary>
        public static void Set(IEnumerable<string> names)
        {
            _names.Clear();
            if (names == null)
                return;

            foreach (var name in names)
            {
                var token = AnimationCueParser.SanitizeGesture(name);
                if (token.Length == 0 || _names.Contains(token))
                    continue;
                _names.Add(token);
                if (_names.Count >= MaxNames)
                    break;
            }
        }

        public static void Clear()
        {
            _names.Clear();
        }

        /// <summary>True when <paramref name="name"/> is one of the offered reactions, ignoring case.</summary>
        public static bool Contains(string name)
        {
            var token = AnimationCueParser.SanitizeGesture(name);
            return token.Length > 0 && _names.Contains(token);
        }

        /// <summary>The prompt line for the current list, or empty when there is none.</summary>
        public static string PromptLine()
        {
            return PromptLine(_names);
        }

        public static string PromptLine(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
                return string.Empty;

            var list = new StringBuilder();
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                    list.Append(", ");
                list.Append(names[i]);
            }

            var example = names[0];
            for (var i = 0; i < names.Count; i++)
            {
                if (!string.Equals(names[i], "happy", StringComparison.Ordinal))
                {
                    example = names[i];
                    break;
                }
            }

            return "Body reactions you can play once, by name: " + list + ". "
                + "To play one, add reaction=\"name\" to the cue tag, for example "
                + "<animation emotion=\"neutral\" reaction=\"" + example + "\" />. "
                + "Use one when it fits the moment, such as a pout when teased or a dance when delighted, not in every reply.";
        }
    }
}
