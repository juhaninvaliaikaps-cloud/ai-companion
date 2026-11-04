using System;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// One clickable body zone. The bone is a humanoid bone, so the zone follows the avatar
    /// whatever model is loaded; the radius is in screen pixels at any window size.
    /// </summary>
    [Serializable]
    public sealed class TouchZone
    {
        public string id = "head";
        public HumanBodyBones bone = HumanBodyBones.Head;
        public float radiusPixels = 56f;

        public Emotion emotion = Emotion.Happy;
        public float intensity = 0.6f;
        public string gesture = string.Empty;

        /// <summary>Portrait name for the 2D frame, for example happy or shy. Empty keeps the emotion portrait.</summary>
        public string expression = string.Empty;
        public float blush = 0.35f;

        /// <summary>Seconds before this zone can react again.</summary>
        public float cooldownSeconds = 1.5f;

        /// <summary>True keeps the reaction while the button stays down (hand holding).</summary>
        public bool hold;
    }

    /// <summary>
    /// Data-driven touch reactions, so a model or a future adult-mode pack can add zones
    /// without touching the runtime code.
    /// </summary>
    [CreateAssetMenu(fileName = "TouchZones", menuName = "AI Companion/Touch Zones")]
    public sealed class TouchZoneSet : ScriptableObject
    {
        public TouchZone[] zones = Array.Empty<TouchZone>();

        public static TouchZoneSet CreateDefault()
        {
            var set = CreateInstance<TouchZoneSet>();
            set.zones = new[]
            {
                new TouchZone
                {
                    id = "head",
                    bone = HumanBodyBones.Head,
                    radiusPixels = 58f,
                    emotion = Emotion.Happy,
                    intensity = 0.6f,
                    gesture = "nod_yes",
                    expression = "happy",
                    blush = 0.35f,
                    cooldownSeconds = 1.5f,
                },
                new TouchZone
                {
                    id = "hand_left",
                    bone = HumanBodyBones.LeftHand,
                    radiusPixels = 44f,
                    emotion = Emotion.Happy,
                    intensity = 0.7f,
                    gesture = string.Empty,
                    expression = "shy",
                    blush = 0.5f,
                    cooldownSeconds = 1f,
                    hold = true,
                },
                new TouchZone
                {
                    id = "hand_right",
                    bone = HumanBodyBones.RightHand,
                    radiusPixels = 44f,
                    emotion = Emotion.Happy,
                    intensity = 0.7f,
                    gesture = string.Empty,
                    expression = "shy",
                    blush = 0.5f,
                    cooldownSeconds = 1f,
                    hold = true,
                },
                new TouchZone
                {
                    id = "shoulder_left",
                    bone = HumanBodyBones.LeftShoulder,
                    radiusPixels = 46f,
                    emotion = Emotion.Neutral,
                    intensity = 0.4f,
                    gesture = "shrug",
                    expression = string.Empty,
                    blush = 0.2f,
                    cooldownSeconds = 2f,
                },
                new TouchZone
                {
                    id = "shoulder_right",
                    bone = HumanBodyBones.RightShoulder,
                    radiusPixels = 46f,
                    emotion = Emotion.Neutral,
                    intensity = 0.4f,
                    gesture = "shrug",
                    expression = string.Empty,
                    blush = 0.2f,
                    cooldownSeconds = 2f,
                },
            };
            return set;
        }
    }
}
