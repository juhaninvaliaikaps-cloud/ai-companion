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
            Entry("stop", "stand still and stop wandering", Emotion.Neutral, 0.3f, string.Empty, string.Empty, string.Empty, 1f, "movement", "still"),

            // Portrait expressions. The body keeps a base emotion; the portrait name in the
            // last argument picks the drawn still, which the five-value Emotion enum cannot express.
            PortraitEntry("concerned", "worried about you or about something you said", Emotion.Neutral, 0.5f, "Idle", "Idle", 2f, "concerned", "care", "worried"),
            PortraitEntry("embarrassed", "flustered and embarrassed by a compliment", Emotion.Neutral, 0.6f, "Idle", "Idle", 2f, "embarrassed", "shy", "compliment"),
            PortraitEntry("shy", "shy and looking away", Emotion.Neutral, 0.5f, "Idle", "Idle", 2f, "shy", "quiet", "soft"),
            PortraitEntry("flustered", "flustered, blushing, lost for words", Emotion.Neutral, 0.7f, "Idle", "Idle", 2f, "flustered", "blush", "tease"),
            PortraitEntry("embarrassed_worried", "embarrassed and worried at the same time", Emotion.Sad, 0.5f, "Sad", "Sad", 2f, "embarrassed_worried", "mixed", "care"),
            PortraitEntry("pouty", "playfully pouting about something", Emotion.Sad, 0.45f, "Sad", "Sad", 2f, "angry", "pout", "playful"),
            PortraitEntry("teasing", "smug, teasing, knowing smile", Emotion.Happy, 0.6f, "Happy", "Happy", 2f, "teasing", "flirt", "smug"),
            PortraitEntry("excited", "excited and delighted", Emotion.Happy, 0.9f, "Happy", "Happy", 2f, "excited", "joy", "hype"),
            PortraitEntry("loving", "warm, affectionate and loving", Emotion.Happy, 0.8f, "Happy", "Happy", 2f, "loving", "affection", "warm"),
            PortraitEntry("sleepy", "sleepy and tired", Emotion.Neutral, 0.3f, "Idle", "Idle", 2f, "sleepy", "late", "tired"),
            };
        }

        /// <summary>
        /// A row whose portrait expression is not one of the five Emotion values. The body
        /// uses the emotion state; the portrait picks the named still instead.
        /// </summary>
        private static AnimationEntry PortraitEntry(
            string id,
            string description,
            Emotion emotion,
            float intensity,
            string animatorState,
            string clipName,
            float durationSeconds,
            string portrait,
            params string[] tags)
        {
            var entry = Entry(id, description, emotion, intensity, string.Empty, animatorState, clipName, durationSeconds, tags);
            entry.portrait = portrait;
            return entry;
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
