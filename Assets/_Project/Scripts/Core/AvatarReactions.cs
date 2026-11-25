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
        static readonly List<string> _adultNames = new List<string>();

        public static IReadOnlyList<string> Names
        {
            get
            {
                if (IntimateExpressions.Undressed)
                {
                    var naked = new List<string>();
                    for (var i = 0; i < _names.Count; i++)
                    {
                        if (IntimateExpressions.IsNakedCue(_names[i]))
                            naked.Add(_names[i]);
                    }

                    naked.Add("dress");
                    return naked;
                }

                if (_adultNames.Count == 0 || IntimateExpressions.Active)
                    return _names;
                var visible = new List<string>(_names.Count);
                for (var i = 0; i < _names.Count; i++)
                {
                    if (!IsAdult(_names[i]))
                        visible.Add(_names[i]);
                }

                return visible;
            }
        }

        /// <summary>Replaces the list. Names are cleaned to cue tokens; duplicates and empties are dropped.</summary>
        public static void Set(IEnumerable<string> names)
        {
            Set(names, null);
        }

        /// <summary>
        /// Replaces the list. <paramref name="adultNames"/> mark the subset that is only
        /// offered while <see cref="IntimateExpressions.Active"/>.
        /// </summary>
        public static void Set(IEnumerable<string> names, IEnumerable<string> adultNames)
        {
            _names.Clear();
            _adultNames.Clear();
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

            if (adultNames == null)
                return;
            foreach (var name in adultNames)
            {
                var token = AnimationCueParser.SanitizeGesture(name);
                if (token.Length == 0 || _adultNames.Contains(token) || !_names.Contains(token))
                    continue;
                _adultNames.Add(token);
            }
        }

        public static void Clear()
        {
            _names.Clear();
            _adultNames.Clear();
        }

        /// <summary>True when <paramref name="name"/> is one of the offered reactions, ignoring case.</summary>
        public static bool Contains(string name)
        {
            var token = AnimationCueParser.SanitizeGesture(name);
            if (token.Length == 0)
                return false;
            if (IntimateExpressions.Undressed)
                return string.Equals(token, "dress", StringComparison.Ordinal)
                    || (IntimateExpressions.IsNakedCue(token) && _names.Contains(token));
            if (!_names.Contains(token))
                return false;
            return IntimateExpressions.Active || !IsAdult(token);
        }

        static bool IsAdult(string token)
        {
            return _adultNames.Contains(token);
        }

        /// <summary>
        /// The offered reaction a model's word stands for: the same name, a known spelling
        /// such as "pouty" for "pout" or "teary_happy" for "happy", or else the one offered name that starts with the
        /// word and an underscore (turn_around -> turn_around_back, wink -> wink_dance).
        /// Empty when none or more than one fit.
        /// </summary>
        public static string Find(string word)
        {
            var token = Canonical(AnimationCueParser.SanitizeGesture(word));
            if (token.Length == 0)
                return string.Empty;
            if (IntimateExpressions.Undressed)
            {
                if (string.Equals(token, "dress", StringComparison.Ordinal))
                    return "dress";
                if (!IntimateExpressions.IsNakedCue(token))
                    return string.Empty;
                if (_names.Contains(token))
                    return token;
                var nakedPrefix = token + "_";
                var nakedFound = string.Empty;
                for (var i = 0; i < _names.Count; i++)
                {
                    if (!IntimateExpressions.IsNakedCue(_names[i])
                        || !_names[i].StartsWith(nakedPrefix, StringComparison.Ordinal))
                        continue;
                    if (nakedFound.Length > 0)
                        return string.Empty;
                    nakedFound = _names[i];
                }

                return nakedFound;
            }

            if (!IntimateExpressions.Active && IsAdult(token))
                return string.Empty;
            if (_names.Contains(token))
                return token;

            var prefix = token + "_";
            var found = string.Empty;
            for (var i = 0; i < _names.Count; i++)
            {
                if (!IntimateExpressions.Active && IsAdult(_names[i]))
                    continue;
                if (!_names[i].StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (found.Length > 0)
                    return string.Empty;
                found = _names[i];
            }

            return found;
        }

        /// <summary>
        /// The clip name for a spelling the model may write. Only known aliases change;
        /// every other name is returned untouched so existing reactions keep resolving
        /// exactly as before. Used for the reaction attribute, which skips <see cref="Find"/>.
        /// </summary>
        public static string Alias(string name)
        {
            var token = AnimationCueParser.SanitizeGesture(name);
            var canonical = Canonical(token);
            return string.Equals(canonical, token, StringComparison.Ordinal) ? name : canonical;
        }

        static string Canonical(string token)
        {
            switch (token)
            {
                case "pouty":
                case "pouting":
                    return "pout";
                // JUH-113: gesture="teary_happy" is a portrait name, not a clip. Happy tears
                // play the happy clip; distressed and anxious play the concerned one.
                case "teary_happy":
                case "teary":
                    return "happy";
                case "distressed":
                case "anxious":
                case "upset":
                    return "concerned";
                case "sit":
                case "sitting":
                case "sitdown":
                case "sit_down":
                    return "sit_down";
                case "lie":
                case "lying":
                case "liedown":
                case "lie_down":
                case "laydown":
                case "lay_down":
                    return "lie_down";
                case "getup":
                case "get_up":
                    return "get_up";
                case "stand":
                case "standup":
                case "stand_up":
                    return "stand_up";
                default:
                    return token;
            }
        }

        /// <summary>The prompt line for the current list, or empty when there is none.</summary>
        public static string PromptLine()
        {
            var names = Names;
            var line = PromptLine(names);
            if (line.Length == 0)
                return line;
            if (IntimateExpressions.Undressed)
            {
                return line + " She is naked now. Use only those naked animations; to get dressed again use reaction=\"dress\". There is no dressing animation, so that cue changes her state and returns to the clothed idle.";
            }

            if (!IntimateExpressions.Active || _adultNames.Count == 0)
                return line;

            var list = new StringBuilder();
            for (var i = 0; i < _adultNames.Count; i++)
            {
                if (!_names.Contains(_adultNames[i]))
                    continue;
                if (list.Length > 0)
                    list.Append(", ");
                list.Append(_adultNames[i]);
                var hint = AdultHint(_adultNames[i]);
                if (hint.Length > 0)
                    list.Append(" (").Append(hint).Append(')');
            }

            if (list.Length == 0)
                return line;
            return line + " Adult-only video reactions: " + list
                + ". Trigger these only with reaction=\"name\" in the animation cue, for example <animation emotion=\"flustered\" reaction=\"strip\" />; do not use emotion= or gesture= for them. "
                + "When she is clothed and the scene becomes intimate, use reaction=\"strip\" first; without that cue the video stays clothed no matter what the text says. After strip she is naked and stays naked: use only the naked animations, and reaction=\"dress\" when she should get dressed again. "
                + ClothingRule()
                + "Use these only in Private mode with the 18+ switch on, when the scene is already intimate and both of you clearly want it; keep the wording suggestive.";
        }

        /// <summary>
        /// Explicit clothing-removal rule. Small models often narrate a shirt coming off but
        /// leave reaction="strip" out, which leaves the video clothed.
        /// </summary>
        public static string ClothingRule()
        {
            if (!IntimateExpressions.Active || IntimateExpressions.Undressed)
                return string.Empty;
            return " Clothing removal rule: when the user pulls off, opens, lifts or removes any item of her clothing, or your narration says an item comes off, you must put reaction=\"strip\" in the cue for that reply. Do not describe her as undressed without that cue. ";
        }

        static string AdultHint(string name)
        {
            switch (name)
            {
                case "strip":
                    return "she undresses";
                case "naked_idle":
                    return "naked breathing loop";
                case "naked_idle_still":
                    return "still naked loop";
                case "bend_over":
                    return "turns and bends over";
                case "bend_over_end":
                    return "returns from bending over";
                default:
                    return string.Empty;
            }
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
