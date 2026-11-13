using System.Globalization;

namespace AICompanion.Core
{
    /// <summary>
    /// The emotion and strength <see cref="UserMoodDetector"/> read from one user message.
    /// Intensity is 0 for neutral and otherwise in the 0 to 1 range.
    /// </summary>
    public readonly struct UserMood
    {
        public UserEmotion Emotion { get; }
        public float Intensity { get; }

        public UserMood(UserEmotion emotion, float intensity)
        {
            Emotion = emotion;
            Intensity = emotion == UserEmotion.Neutral ? 0f : AnimationCue.ClampIntensity(intensity);
        }

        public static UserMood Neutral
        {
            get { return new UserMood(UserEmotion.Neutral, 0f); }
        }

        public bool IsNeutral
        {
            get { return Emotion == UserEmotion.Neutral; }
        }

        /// <summary>One console line. It never contains the message text.</summary>
        public string ToLogString()
        {
            if (IsNeutral)
                return "User mood: neutral";
            return "User mood: " + Emotion.ToString().ToLowerInvariant() + " "
                + Intensity.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
