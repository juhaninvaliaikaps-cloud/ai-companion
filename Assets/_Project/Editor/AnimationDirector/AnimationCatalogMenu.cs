using System.Collections.Generic;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Editor.AnimationDirector
{
    /// <summary>
    /// Writes the starter catalog asset and prints its prompt list. The ten starter
    /// rows are defined here as well as in the committed asset, so the asset can be
    /// rebuilt after an accidental delete or a hand edit.
    /// </summary>
    public static class AnimationCatalogMenu
    {
        public const string CatalogPath = "Assets/_Project/Settings/AnimationCatalog.asset";

        private const string ClipsPath = "Assets/_Project/Animations/";

        [MenuItem("AI Companion/Animation Catalog/Rebuild Starter Catalog", false, 20)]
        public static void RebuildStarterCatalog()
        {
            AnimationClipCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            }

            catalog.SetEntries(StarterEntries());
            if (created)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log((created ? "Created " : "Rebuilt ") + CatalogPath + " with " + catalog.Entries.Count + " entries.");
        }

        [MenuItem("AI Companion/Animation Catalog/Log Prompt List", false, 21)]
        public static void LogPromptList()
        {
            AnimationClipCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("No catalog at " + CatalogPath + ". Run Rebuild Starter Catalog first.");
                return;
            }

            if (!catalog.Validate(out List<string> problems))
            {
                Debug.LogWarning("Animation catalog has problems:\n" + string.Join("\n", problems));
            }

            Debug.Log("Animation catalog prompt list:\n" + catalog.BuildPromptList());
        }

        /// <summary>
        /// The starter set: one row per Animator state the project ships. The four
        /// gestures, the walk loop, and the turn all carry a state and a clip, so a cue
        /// that names one of them plays the animation instead of falling back.
        /// </summary>
        public static List<AnimationEntry> StarterEntries()
        {
            return new List<AnimationEntry>
            {
                Entry("idle", "calm resting pose; the director returns here when nothing else plays", Emotion.Neutral, 0.2f, string.Empty, "Idle", "Idle", 2f, "mood", "idle"),
                Entry("happy", "bright, cheerful reaction to good news", Emotion.Happy, 0.8f, string.Empty, "Happy", "Happy", 2f, "mood", "positive"),
                Entry("sad", "downcast, sympathetic reaction", Emotion.Sad, 0.7f, string.Empty, "Sad", "Sad", 2f, "mood", "negative"),
                Entry("surprised", "short startled reaction", Emotion.Surprised, 0.9f, string.Empty, "Surprised", "Surprised", 1f, "mood", "reaction"),
                Entry("thinking", "considering something with mild uncertainty", Emotion.Thinking, 0.6f, string.Empty, "Thinking", "Thinking", 2f, "mood", "thinking"),
                Entry("talk", "speaking loop while the reply audio plays", Emotion.Neutral, 0.3f, string.Empty, "Talk", "Talk", 1f, "speech", "loop"),
                Entry("wave_hello", "friendly hello wave", Emotion.Happy, 0.7f, "wave", "Wave", "Wave", 1.4f, "greeting", "friendly"),
                Entry("nod_yes", "agreeing nod", Emotion.Neutral, 0.4f, "nod", "Nod", "Nod", 1f, "agreement", "confirmation"),
                Entry("shrug", "uncertain shrug with no strong feeling", Emotion.Neutral, 0.4f, "shrug", "Shrug", "Shrug", 1.3f, "uncertain", "neutral"),
                Entry("laugh", "short laugh at something funny", Emotion.Happy, 0.9f, "laugh", "Laugh", "Laugh", 1.4f, "amused", "funny"),
                Entry("walk", "walking loop on the spot", Emotion.Neutral, 0.5f, "walk", "Walk", "Walk", 1f, "locomotion", "loop"),
                Entry("turn_around", "turn around in place to look behind", Emotion.Neutral, 0.5f, "turn", "Turn180", "Turn180", 0.9f, "turn", "orientation"),
            };
        }

        private static AnimationEntry Entry(
            string id,
            string description,
            Emotion emotion,
            float intensity,
            string gesture,
            string animatorState,
            string clipName,
            float durationSeconds,
            params string[] tags)
        {
            return new AnimationEntry
            {
                id = id,
                description = description,
                tags = tags,
                emotion = emotion,
                intensity = intensity,
                gesture = gesture,
                animatorState = animatorState,
                clip = string.IsNullOrEmpty(clipName)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipsPath + clipName + ".anim"),
                durationSeconds = durationSeconds,
            };
        }
    }
}
