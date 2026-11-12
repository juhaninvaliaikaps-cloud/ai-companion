using AICompanion.Core;
using AICompanion.UI.Presenters;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-104 PR B: the chat presenter, built from this panel's existing entry points. Kept
    /// in its own file so the open PRs that edit ChatPanel.cs merge around one word (partial).
    /// </summary>
    public sealed partial class ChatPanel
    {
        ChatPresenter _presenter;

        /// <summary>Send/Stop, Mute, Look and Escape for the old panel and the new chat view.</summary>
        public ChatPresenter Presenter
        {
            get
            {
                if (_presenter == null)
                {
                    _presenter = new ChatPresenter(
                        () => _conversation != null && _conversation.IsBusy,
                        () =>
                        {
                            BindSpeech();
                            return _speech;
                        },
                        () => GetComponent<IScreenLook>(),
                        text =>
                        {
                            if (!_started)
                                StartCompanion();
                            return TrySend(text);
                        },
                        CancelReply);
                }

                return _presenter;
            }
        }
    }
}
