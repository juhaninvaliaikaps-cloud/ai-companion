using AICompanion.Core;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Keeps one reply's expression on the 3D face and the 2D portrait for the whole
    /// utterance. Lip sync is a separate mouth layer and does not clear this.
    /// The hold does not expire in the quiet gap before the first syllable. It
    /// returns to neutral after speech has been heard and then stopped for
    /// <see cref="ReleaseSeconds"/>, or immediately when a new cue arrives.
    /// A neutral cue clears at once.
    /// </summary>
    public sealed class ExpressionHold
    {
        public const float ReleaseSeconds = 0.8f;

        bool _heardSpeech;
        float _quietFor;
        string _portrait = string.Empty;

        public Emotion Emotion { get; private set; }

        public float Intensity { get; private set; }

        public string Portrait
        {
            get { return _portrait; }
        }

        public bool Active { get; private set; }

        public void Apply(Emotion emotion, float intensity, string portrait, bool speaking)
        {
            var name = Normalize(portrait);
            var named = name.Length > 0 && name != "neutral";
            if (emotion == Emotion.Neutral && !named)
            {
                Clear();
                return;
            }

            Emotion = emotion;
            Intensity = AnimationCue.ClampIntensity(intensity);
            _portrait = named ? name : string.Empty;
            Active = true;
            _quietFor = 0f;
            _heardSpeech = speaking;
        }

        public void Clear()
        {
            Emotion = Emotion.Neutral;
            Intensity = 0f;
            _portrait = string.Empty;
            Active = false;
            _heardSpeech = false;
            _quietFor = 0f;
        }

        /// <summary>
        /// Advances the release tail. True on the tick that lets go.
        /// </summary>
        public bool Tick(float deltaSeconds, bool speaking)
        {
            if (!Active)
                return false;

            if (float.IsNaN(deltaSeconds) || deltaSeconds < 0f)
                deltaSeconds = 0f;

            if (speaking)
            {
                _heardSpeech = true;
                _quietFor = 0f;
                return false;
            }

            if (!_heardSpeech)
                return false;

            _quietFor += deltaSeconds;
            if (_quietFor < ReleaseSeconds)
                return false;

            Clear();
            return true;
        }

        static string Normalize(string portrait)
        {
            if (string.IsNullOrWhiteSpace(portrait))
                return string.Empty;
            return portrait.Trim().ToLowerInvariant();
        }
    }
}
