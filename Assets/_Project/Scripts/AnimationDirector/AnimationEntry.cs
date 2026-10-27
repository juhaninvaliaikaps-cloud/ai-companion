using System;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AnimationDirector
{
    /// <summary>
    /// One catalog row. The id, description, and tags are what the model sees.
    /// The gesture and animator state are what the director resolves at runtime.
    /// A row may have a clip and no gesture (an emotion) or a gesture and no clip
    /// yet (a catalog entry waiting for its animation).
    /// </summary>
    [Serializable]
    public sealed class AnimationEntry
    {
        public string id = string.Empty;
        public string description = string.Empty;
        public string[] tags = Array.Empty<string>();
        public Emotion emotion = Emotion.Neutral;
        [Range(0f, 1f)] public float intensity = 1f;
        public string gesture = string.Empty;
        public string animatorState = string.Empty;
        public AnimationClip clip;
        public float durationSeconds;

        public bool HasGesture
        {
            get { return !string.IsNullOrWhiteSpace(gesture); }
        }

        public bool HasAnimatorState
        {
            get { return !string.IsNullOrWhiteSpace(animatorState); }
        }
    }
}
