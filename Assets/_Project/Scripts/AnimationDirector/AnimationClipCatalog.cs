using System;
using System.Collections.Generic;
using System.Text;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// Data-driven list of animations the companion can play. Chat providers read
    /// the compact prompt list; the animation director resolves ids and gestures to
    /// Animator states. Adding an animation is a new entry, not a code change.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationCatalog", menuName = "AI Companion/Animation Catalog")]
    public sealed class AnimationClipCatalog : ScriptableObject
    {
        [SerializeField] private List<AnimationEntry> entries = new List<AnimationEntry>();

        public IReadOnlyList<AnimationEntry> Entries
        {
            get { return entries; }
        }

        public bool TryGetById(string id, out AnimationEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                string trimmed = id.Trim();
                for (int i = 0; i < entries.Count; i++)
                {
                    AnimationEntry candidate = entries[i];
                    if (candidate != null && string.Equals(candidate.id, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Last resort lookup: a row whose tags contain the token. Keeps a slightly wrong
        /// cue close to the right family instead of falling back to neutral.
        /// </summary>
        public bool TryGetByTag(string tag, out AnimationEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                string trimmed = tag.Trim();
                for (int i = 0; i < entries.Count; i++)
                {
                    AnimationEntry candidate = entries[i];
                    if (candidate == null || candidate.tags == null)
                        continue;

                    for (int t = 0; t < candidate.tags.Length; t++)
                    {
                        if (string.Equals(candidate.tags[t], trimmed, StringComparison.OrdinalIgnoreCase))
                        {
                            entry = candidate;
                            return true;
                        }
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// True when a row's gesture field matches. The director also tries
        /// <see cref="TryGetById"/> first, because the prompt asks for catalog ids.
        /// A miss on both is not an error; the director plays neutral.
        /// </summary>
        public bool TryGetByGesture(string gesture, out AnimationEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(gesture))
            {
                string trimmed = gesture.Trim();
                for (int i = 0; i < entries.Count; i++)
                {
                    AnimationEntry candidate = entries[i];
                    if (candidate != null && candidate.HasGesture &&
                        string.Equals(candidate.gesture, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// The entry that carries this emotion. Gesture rows describe an action, not a
        /// resting face, so they are skipped here.
        /// </summary>
        public bool TryGetByEmotion(Emotion emotion, out AnimationEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                AnimationEntry candidate = entries[i];
                if (candidate != null && candidate.emotion == emotion && !candidate.HasGesture)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// The catalog as compact prompt text: one line per named entry, in list order.
        /// Rows without an id are skipped so a half-filled draft cannot confuse the model.
        /// </summary>
        public string BuildPromptList()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                AnimationEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("- ").Append(entry.id.Trim()).Append(": ").Append(entry.description);
                if (entry.HasGesture)
                {
                    builder.Append("; gesture: ").Append(entry.gesture.Trim());
                }

                builder.Append("; emotion: ").Append(entry.emotion.ToString().ToLowerInvariant());
                string tags = JoinTags(entry.tags);
                if (tags.Length > 0)
                {
                    builder.Append("; tags: ").Append(tags);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns false and fills <paramref name="problems"/> when the catalog would
        /// produce a broken prompt or an ambiguous lookup.
        /// </summary>
        public bool Validate(out List<string> problems)
        {
            problems = new List<string>();
            if (entries == null)
            {
                problems.Add("Catalog has no entry list.");
                return false;
            }

            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenGestures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                AnimationEntry entry = entries[i];
                if (entry == null)
                {
                    problems.Add("Entry " + i + " is null.");
                    continue;
                }

                string id = entry.id == null ? string.Empty : entry.id.Trim();
                if (id.Length == 0)
                {
                    problems.Add("Entry " + i + " has no id.");
                }
                else if (!seenIds.Add(id))
                {
                    problems.Add("Duplicate id '" + id + "'.");
                }
                else if (string.IsNullOrWhiteSpace(entry.description))
                {
                    problems.Add("Entry '" + id + "' has no description.");
                }
                else if (!AnimationCueParser.IsCueToken(id))
                {
                    problems.Add("Entry '" + id + "' has an id that cannot be written in a cue tag.");
                }

                if (HasLineBreak(entry.description))
                {
                    problems.Add("Entry '" + id + "' has a line break in its description.");
                }

                if (entry.intensity < AnimationCue.MinIntensity || entry.intensity > AnimationCue.MaxIntensity ||
                    float.IsNaN(entry.intensity))
                {
                    problems.Add("Entry '" + id + "' has intensity outside 0 to 1.");
                }

                if (entry.durationSeconds < 0f)
                {
                    problems.Add("Entry '" + id + "' has a negative duration.");
                }

                if (entry.HasGesture)
                {
                    string gesture = entry.gesture.Trim();
                    if (!AnimationCueParser.IsCueToken(gesture))
                    {
                        problems.Add("Entry '" + id + "' has a gesture that cannot be written in a cue tag.");
                    }
                    else if (!seenGestures.Add(gesture))
                    {
                        problems.Add("Duplicate gesture '" + gesture + "'.");
                    }
                }

                if (entry.HasAnimatorState &&
                    (entry.animatorState != entry.animatorState.Trim() || HasLineBreak(entry.animatorState)))
                {
                    problems.Add("Entry '" + id + "' has an animator state name with surrounding space or a line break.");
                }

                if (entry.tags != null)
                {
                    for (int t = 0; t < entry.tags.Length; t++)
                    {
                        if (HasLineBreak(entry.tags[t]))
                        {
                            problems.Add("Entry '" + id + "' has a line break in a tag.");
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                AnimationEntry entry = entries[i];
                if (entry == null || !entry.HasGesture)
                {
                    continue;
                }

                string gesture = entry.gesture.Trim();
                string id = entry.id == null ? string.Empty : entry.id.Trim();
                if (seenIds.Contains(gesture) && !string.Equals(gesture, id, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add("Gesture '" + gesture + "' is another row's id, so an id lookup hides this gesture.");
                }
            }

            return problems.Count == 0;
        }

        private static bool HasLineBreak(string value)
        {
            return value != null && (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0);
        }

        /// <summary>
        /// Replaces every entry. Used by the editor menu that writes the starter
        /// catalog and by tests; runtime code only reads the asset.
        /// </summary>
        public void SetEntries(IEnumerable<AnimationEntry> newEntries)
        {
            entries.Clear();
            if (newEntries == null)
            {
                return;
            }

            foreach (AnimationEntry entry in newEntries)
            {
                entries.Add(entry);
            }
        }

        private static string JoinTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(tags[i]))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(tags[i].Trim());
            }

            return builder.ToString();
        }
    }
}
