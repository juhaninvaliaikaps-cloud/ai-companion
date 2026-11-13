namespace AICompanion.Core
{
    /// <summary>
    /// JUH-108 spoken-text glow: what she is saying right now. The voice plays one clip per
    /// sentence; this reports that clip's sentence and how far it has played, so the chat can
    /// light up the words as they are said. None of the current engines (xAI, Piper, Kokoro)
    /// return word timestamps here, so the clip's real duration is the timing source.
    /// </summary>
    public interface ISpeechProgress
    {
        /// <param name="clipSerial">Goes up by one for every clip that starts playing.</param>
        /// <param name="text">The sentence the clip says (as sent to the engine).</param>
        /// <param name="time">Seconds played.</param>
        /// <param name="length">Clip length in seconds.</param>
        /// <returns>False while no clip is playing.</returns>
        bool TryGetSpokenClip(out int clipSerial, out string text, out float time, out float length);
    }
}
