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
        public float blush = 0.35f;

        /// <summary>Seconds before this zone can react again.</summary>
        public float cooldownSeconds = 1.5f;

        /// <summary>True keeps the reaction while the button stays down (hand holding).</summary>
        public bool hold;

        /// <summary>Portrait name for the 2D frame, for example happy or shy. Empty keeps the emotion portrait.</summary>
        public string expression = string.Empty;

        /// <summary>Offset from the bone, in the bone's local space. Used for zones without a humanoid bone, like a cheek.</summary>
        public Vector3 localOffset = Vector3.zero;

        /// <summary>True lets the pointer drag this hand with IK while the button stays down.</summary>
        public bool drag;

        /// <summary>
        /// True makes this zone a handle: pressing it does nothing but move the window. It never
        /// reacts, so a user can always grab her somewhere without triggering a touch.
        /// </summary>
        public bool passive;
    }

    /// <summary>
    /// Data-driven touch reactions, so a model or a future content pack can add zones without
    /// touching the runtime code.
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
                    localOffset = new Vector3(0f, 0.02f, 0f),
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
                    id = "hair",
                    bone = HumanBodyBones.Head,
                    localOffset = new Vector3(0f, 0.10f, 0.01f),
                    radiusPixels = 52f,
                    emotion = Emotion.Neutral,
                    intensity = 0.4f,
                    gesture = "nod_yes",
                    expression = string.Empty,
                    blush = 0.15f,
                    cooldownSeconds = 1.5f,
                },
                new TouchZone
                {
                    id = "cheek",
                    bone = HumanBodyBones.Head,
                    localOffset = new Vector3(0.055f, 0f, 0.075f),
                    radiusPixels = 34f,
                    emotion = Emotion.Happy,
                    intensity = 0.5f,
                    gesture = string.Empty,
                    expression = "flustered",
                    blush = 0.65f,
                    cooldownSeconds = 2f,
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
                    drag = true,
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
                    drag = true,
                },
                new TouchZone
                {
                    id = "forearm_left",
                    bone = HumanBodyBones.LeftLowerArm,
                    radiusPixels = 40f,
                    emotion = Emotion.Happy,
                    intensity = 0.4f,
                    gesture = string.Empty,
                    expression = "shy",
                    blush = 0.3f,
                    cooldownSeconds = 2f,
                },
                new TouchZone
                {
                    id = "forearm_right",
                    bone = HumanBodyBones.RightLowerArm,
                    radiusPixels = 40f,
                    emotion = Emotion.Happy,
                    intensity = 0.4f,
                    gesture = string.Empty,
                    expression = "shy",
                    blush = 0.3f,
                    cooldownSeconds = 2f,
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
                new TouchZone
                {
                    id = "belly",
                    bone = HumanBodyBones.Spine,
                    localOffset = new Vector3(0f, 0.06f, 0.07f),
                    radiusPixels = 48f,
                    emotion = Emotion.Neutral,
                    intensity = 0f,
                    gesture = string.Empty,
                    expression = string.Empty,
                    blush = 0f,
                    cooldownSeconds = 0f,
                    passive = true,
                },
                new TouchZone
                {
                    id = "back",
                    bone = HumanBodyBones.Chest,
                    localOffset = new Vector3(0f, 0f, -0.12f),
                    radiusPixels = 54f,
                    emotion = Emotion.Surprised,
                    intensity = 0.5f,
                    gesture = string.Empty,
                    expression = string.Empty,
                    blush = 0.2f,
                    cooldownSeconds = 2.5f,
                },
                new TouchZone
                {
                    id = "side",
                    bone = HumanBodyBones.Hips,
                    localOffset = new Vector3(0.14f, 0.02f, 0f),
                    radiusPixels = 46f,
                    emotion = Emotion.Surprised,
                    intensity = 0.6f,
                    gesture = string.Empty,
                    expression = string.Empty,
                    blush = 0.25f,
                    cooldownSeconds = 2.5f,
                },
            };
            return set;
        }
    }
}
