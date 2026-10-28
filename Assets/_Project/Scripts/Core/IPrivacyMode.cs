namespace AICompanion.Core
{
    /// <summary>
    /// Private mode keeps adult or local content on this machine.
    /// Cloud chat, cloud TTS, the voice agent, and Uthana must stay off.
    /// </summary>
    public interface IPrivacyMode
    {
        bool PrivateMode { get; }
    }
}
