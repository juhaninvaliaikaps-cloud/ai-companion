namespace AICompanion.Core
{
    /// <summary>
    /// Brings the player window forward so a text field can receive keys.
    /// The editor Game view does this on its own.
    /// </summary>
    public interface ITypingFocus
    {
        void FocusForTyping();
    }
}
