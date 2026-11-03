using System.Collections.Generic;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Editor.AnimationDirector
{
    /// <summary>
    /// Writes the starter catalog asset and prints its prompt list. The starter
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
        /// Portrait rows keep the body on Idle or a base emotion. The portrait name
        /// drives the 2D still and, when it names a face preset, the 3D face.
        /// </summary>
        public static List<AnimationEntry> StarterEntries()
        {
            return new List<AnimationEntry>
            {
                Entry("idle", "calm resting pose; the director returns here when nothing else plays", Emotion.Neutral, 0.2f, string.Empty, "Idle", "Idle", 2f, "mood", "idle"),
                Entry("happy", "use for good news, a joke, or a warm greeting. Not the default", Emotion.Happy, 0.8f, string.Empty, "Happy", "Happy", 2f, "mood", "positive"),
                Entry("sad", "use for sympathy, disappointment, bad news, or a let-down", Emotion.Sad, 0.7f, string.Empty, "Sad", "Sad", 2f, "mood", "negative"),
                Entry("surprised", "use for a sudden reveal, shock, or something unexpected", Emotion.Surprised, 0.9f, string.Empty, "Surprised", "Surprised", 1f, "mood", "reaction"),
                Entry("thinking", "use when working something out, pausing, or unsure", Emotion.Thinking, 0.6f, string.Empty, "Thinking", "Thinking", 2f, "mood", "thinking"),
                Entry("wave_hello", "friendly hello wave", Emotion.Happy, 0.7f, "wave", "Wave", "Wave", 1.4f, "greeting", "friendly"),
                Entry("nod_yes", "agreeing nod", Emotion.Neutral, 0.4f, "nod", "Nod", "Nod", 1f, "agreement", "confirmation"),
                Entry("shrug", "uncertain shrug with no strong feeling", Emotion.Neutral, 0.4f, "shrug", "Shrug", "Shrug", 1.3f, "uncertain", "neutral"),
                Entry("laugh", "short laugh at something funny", Emotion.Happy, 0.9f, "laugh", "Laugh", "Laugh", 1.4f, "amused", "funny"),
                Entry("walk", "walk along the desktop when asked to walk, move, come here, or go left or right", Emotion.Neutral, 0.5f, "walk", "Walk", "Walk", 1f, "locomotion", "loop"),
                Entry("turn_around", "turn around in place to look behind", Emotion.Neutral, 0.5f, "turn", "Turn180", "Turn180", 0.9f, "turn", "orientation"),
                PriorityRow(Entry("wave_bye", "friendly goodbye wave", Emotion.Happy, 0.7f, "wave_bye", "Wave", "Wave", 1.4f, "greeting", "goodbye"), 1),
                Entry("nod_no", "shaking the head no", Emotion.Neutral, 0.4f, "nod_no", "Nod", "Nod", 1f, "disagreement", "no"),
                Entry("sigh", "a quiet sigh", Emotion.Sad, 0.5f, string.Empty, "Sad", "Sad", 2f, "sigh", "tired"),
                Entry("stop", "stop and stand still when asked to stop, wait, or stay", Emotion.Neutral, 0.3f, "stop", string.Empty, string.Empty, 1f, "movement", "still"),

                // Portrait expressions. The body stays put. The last argument is the still
                // the 2D panel shows, and the name the 3D face resolves when it knows it.
                PortraitEntry("concerned", "use when worried about the user or about what they said. Inner brows, no blush", Emotion.Neutral, 0.5f, "Idle", "Idle", 2f, "concerned", "care", "worried"),
                PortraitEntry("embarrassed", "use when a compliment or being caught out makes her blush hard", Emotion.Neutral, 0.6f, "Idle", "Idle", 2f, "embarrassed", "shy", "compliment"),
                PortraitEntry("shy", "use when she is quiet and looking away, with a small smile and a light blush", Emotion.Neutral, 0.5f, "Idle", "Idle", 2f, "shy", "quiet", "soft"),
                PortraitEntry("flustered", "use when she is lost for words or overwhelmed, blushing with an open mouth", Emotion.Neutral, 0.7f, "Idle", "Idle", 2f, "flustered", "blush", "tease"),
                PortraitEntry("embarrassed_worried", "use when she is embarrassed and worried at the same time", Emotion.Sad, 0.5f, "Sad", "Sad", 2f, "embarrassed_worried", "mixed", "care"),
                PortraitEntry("pouty", "use for playful annoyance or a pout. Write emotion sad, or gesture pouty", Emotion.Sad, 0.45f, "Sad", "Sad", 2f, "angry", "pout", "playful"),
                PortraitEntry("teasing", "use for a smug, knowing, teasing smile", Emotion.Happy, 0.6f, "Happy", "Happy", 2f, "teasing", "flirt", "smug"),
                PortraitEntry("excited", "use when she is delighted and energetic, more than a plain happy smile", Emotion.Happy, 0.9f, "Happy", "Happy", 2f, "excited", "joy", "hype"),
                PortraitEntry("loving", "use when she is warm, affectionate, or loving", Emotion.Happy, 0.8f, "Happy", "Happy", 2f, "loving", "affection", "warm"),
                PortraitEntry("sleepy", "use when she is tired or ready for bed", Emotion.Neutral, 0.3f, "Idle", "Idle", 2f, "sleepy", "late", "tired"),
                // Laughing is a portrait. The 3D face uses happy. There is no body clip, so the smile stays face-only.
                PortraitFace("laughing", "laughing, open smile on the portrait while the body stays still", Emotion.Happy, 0.9f, "laughing", "amused", "laugh"),
            };
        }

        /// <summary>
        /// A portrait expression whose 3D side is only the face emotion. No animator state and no clip.
        /// </summary>
        private static AnimationEntry PortraitFace(
            string id,
            string description,
            Emotion emotion,
            float intensity,
            string portrait,
            params string[] tags)
        {
            return new AnimationEntry
            {
                id = id,
                description = description,
                tags = tags,
                emotion = emotion,
                intensity = intensity,
                gesture = string.Empty,
                animatorState = string.Empty,
                clip = null,
                durationSeconds = 2f,
                portrait = portrait,
            };
        }

        /// <summary>
        /// A row whose drawn expression is a portrait name. The body is not cross-faded.
        /// The portrait name picks the still, and the 3D face reads that same name.
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

        private static AnimationEntry PriorityRow(AnimationEntry entry, int priority)
        {
            if (entry != null)
                entry.priority = priority;
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
