namespace AICompanion.Core
{
    /// <summary>
    /// What the user seems to feel, read from their own message. This is not the
    /// avatar's face: <see cref="UserMoodReaction"/> maps it to an <see cref="Emotion"/>.
    /// </summary>
    public enum UserEmotion
    {
        Neutral = 0,
        Happy = 1,
        Sad = 2,
        Angry = 3,
        Anxious = 4,
        Surprised = 5,
        Affectionate = 6,
        Confused = 7,
    }
}
