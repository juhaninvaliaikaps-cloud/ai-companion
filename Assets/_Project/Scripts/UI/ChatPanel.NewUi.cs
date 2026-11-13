using System;
using System.Collections.Generic;
using AICompanion.AI;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-104 / JUH-108: what the new chat view needs from this panel. While the new UI is
    /// on, this panel stays the chat engine (providers, private mode, speech origin, memory,
    /// screen look) and only its own canvas is hidden. With the new UI off nothing here is
    /// used and the panel behaves as on main.
    /// </summary>
    public sealed partial class ChatPanel
    {
        ISpeechOutput _speechOverride;
        bool _replyCuesElsewhere;
        bool _legacyHidden;

        /// <summary>
        /// Routes reply speech through <paramref name="sink"/> (the visual-novel player) instead of
        /// the voice directly. With <paramref name="cuesElsewhere"/> the reply's cue is no longer
        /// played at the end of the reply; the sink fires each cue at its own line. Null restores main.
        /// </summary>
        public void UseSpeechOverride(ISpeechOutput sink, bool cuesElsewhere)
        {
            if (!_started)
                StartCompanion();
            if (_speech != null && !ReferenceEquals(_speech, sink))
                _speech.Cancel();
            _speechOverride = sink;
            _replyCuesElsewhere = sink != null && cuesElsewhere;
            _speech = null;
            BindSpeech();
        }

        /// <summary>Raw provider pieces (cue tags included) for the new chat; null to stop.</summary>
        public void SetRawReplyObserver(Action<string> observer)
        {
            if (!_started)
                StartCompanion();
            if (_exchange != null)
                _exchange.RawDelta = observer;
        }

        /// <summary>Hides this panel's own canvas (chat, model badge) while the new chat view shows instead.</summary>
        public void SetLegacyHidden(bool hidden)
        {
            if (!_started)
                StartCompanion();
            _legacyHidden = hidden;
            if (hidden)
            {
                if (_input != null && _input.isFocused)
                    _input.DeactivateInputField();
                if (_keyInput != null && _keyInput.isFocused)
                    _keyInput.DeactivateInputField();
            }

            if (_canvasObject != null)
                _canvasObject.SetActive(!hidden);
        }

        public bool LegacyHidden => _legacyHidden;

        /// <summary>The chat open state shared by F10, the tray, double-click and push-to-talk.</summary>
        public bool ChatVisible => _visible;

        public void ShowChat(bool visible)
        {
            if (!_started)
                StartCompanion();
            SetVisible(visible, false);
        }

        public IReadOnlyList<ChatLine> TranscriptLines
        {
            get
            {
                if (!_started)
                    StartCompanion();
                return _conversation != null ? _conversation.Lines : Array.Empty<ChatLine>();
            }
        }

        /// <summary>The newest assistant line, or empty.</summary>
        public string LastAssistantText
        {
            get
            {
                var lines = TranscriptLines;
                for (var i = lines.Count - 1; i >= 0; i--)
                {
                    if (lines[i] != null && lines[i].Kind == ChatLineKind.Assistant)
                        return lines[i].Text;
                }

                return string.Empty;
            }
        }

        /// <summary>The same badge text and colour the old panel shows (provider, model, private/local).</summary>
        public string ModelBadgeText => _modelBadge != null ? _modelBadge.text : string.Empty;

        public Color ModelBadgeColor => _modelBadge != null ? _modelBadge.color : Color.white;

        public bool MicRecording
        {
            get
            {
                BindTalk();
                return _talk != null && _talk.IsRecording;
            }
        }

        public bool MicTranscribing
        {
            get
            {
                BindTalk();
                return _talk != null && _talk.IsTranscribing;
            }
        }

        public void SetMicButtonHeld(bool held)
        {
            SetMicHeld(held);
        }

        /// <summary>The chat's API-key field (C-08) for the new view. Same messages as the old Save button.</summary>
        public void SaveApiKeyFromNewUi(string key)
        {
            if (!_started)
                StartCompanion();
            if (_conversation == null)
                return;

            var trimmed = (key ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                _conversation.AddSystem("Enter an API key first.");
                RefreshTranscript(true);
                return;
            }

            try
            {
                ApiKeyFile.SavePreferred(trimmed);
                _conversation.AddSystem("API key saved on this PC.");
            }
            catch (Exception)
            {
                _conversation.AddSystem("Could not save the API key on this PC.");
            }

            RefreshTranscript(true);
        }
    }
}
