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
        /// True when the catalog has an entry for this gesture name. A cue with an
        /// unknown gesture falls back to the emotion, so a miss here is not an error.
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
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                else if (!seen.Add(id))
                {
                    problems.Add("Duplicate id '" + id + "'.");
                }
                else if (string.IsNullOrWhiteSpace(entry.description))
                {
                    problems.Add("Entry '" + id + "' has no description.");
                }

                if (entry.intensity < AnimationCue.MinIntensity || entry.intensity > AnimationCue.MaxIntensity)
                {
                    problems.Add("Entry '" + id + "' has intensity outside 0 to 1.");
                }

                if (entry.durationSeconds < 0f)
                {
                    problems.Add("Entry '" + id + "' has a negative duration.");
                }
            }

            return problems.Count == 0;
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
