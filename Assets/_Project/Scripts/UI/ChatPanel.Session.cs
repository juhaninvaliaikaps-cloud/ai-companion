using System;
using System.Collections.Generic;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-118 sections 2 and 3: the chat side of her world.
    /// <list type="bullet">
    /// <item>The persona prompt gets the lines from <see cref="WorldPromptNow"/>: the first
    /// scene on the first message of an empty conversation, and the away line once.</item>
    /// <item>A session is the messages since the last pause. When it ends (the persona is
    /// switched, ten minutes pass without a message, or the app quits) the model is asked once
    /// for one to three memories of it (<see cref="SessionMemories"/>). Sessions with fewer
    /// than four user messages are skipped. Quitting waits a few seconds at most.</item>
    /// </list>
    /// </summary>
    public sealed partial class ChatPanel
    {
        int _sessionUserMessages;
        int _sessionStartLine;
        float _sessionLastMessageAt;
        bool _quitHooked;
        bool _quitAnswered;

        /// <summary>
        /// The persona text for a request: her mood line, then the lines about her own life.
        /// <paramref name="chatMessage"/> is true for a typed or spoken message; the first scene
        /// is only added there, and only when the conversation has no earlier lines.
        /// </summary>
        string PersonaPrompt(bool chatMessage)
        {
            var prompt = CompanionMoodPrompt.AppendCurrent(settings != null ? settings.systemPrompt : string.Empty);
            // JUH-118 section 4: who she is to the user now, right after the mood line.
            prompt = RelationshipNow.Append(prompt);
            return WorldPromptNow.Append(prompt, chatMessage && ConversationIsEmpty());
        }

        bool ConversationIsEmpty()
        {
            if (_conversation == null)
                return true;
            var lines = _conversation.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Kind != ChatLineKind.System)
                    return false;
            }

            return true;
        }

        /// <summary>Called just before a user message is sent.</summary>
        void SessionBeforeSend()
        {
            if (_sessionUserMessages == 0 && _conversation != null)
                _sessionStartLine = _conversation.Lines.Count;
        }

        int _landingCueSerial = -1;

        /// <summary>
        /// Called when a reply to a user message has finished. The relationship hears it once per
        /// reply (guarded by <see cref="ChatExchange.CueSerial"/>), also when the tag's emotion was
        /// unreadable: the cue is then neutral, and only the exchange itself counts.
        /// </summary>
        void SessionReplyFinished()
        {
            WorldPromptNow.ReplyFinished();
            _sessionUserMessages++;
            _sessionLastMessageAt = Time.unscaledTime;
            if (_exchange != null && _exchange.CueSerial != _landingCueSerial)
            {
                _landingCueSerial = _exchange.CueSerial;
                RelationshipNow.ReportReply(_exchange.LastCue);
            }
        }

        /// <summary>Once per frame: a long pause ends the session. No allocation.</summary>
        void TickSession()
        {
            if (!_quitHooked)
            {
                _quitHooked = true;
                Application.wantsToQuit += OnWantsToQuit;
            }

            if (_sessionUserMessages == 0)
                return;
            if (Time.unscaledTime - _sessionLastMessageAt < SessionMemories.IdleSeconds)
                return;
            if (_conversation != null && _conversation.IsBusy)
                return;
            EndSession("a long pause", SessionMemories.DefaultTimeoutSeconds, null);
        }

        void UnhookSession()
        {
            if (_quitHooked)
                Application.wantsToQuit -= OnWantsToQuit;
            _quitHooked = false;
        }

        /// <summary>
        /// Ends the session now, before the persona changes: the request leaves with the old
        /// persona's model and privacy, and the memories go into the old persona's store.
        /// Called by the settings panel just before a persona switch.
        /// </summary>
        public void EndSessionForSwitch()
        {
            EndSession("a persona switch", SessionMemories.DefaultTimeoutSeconds, null);
        }

        /// <summary>Forgets the running session without asking for memories (the transcript was cleared).</summary>
        void DropSession()
        {
            _sessionUserMessages = 0;
            _sessionStartLine = 0;
        }

        bool OnWantsToQuit()
        {
            if (_quitAnswered || !_alive || !SessionMemories.Qualifies(_sessionUserMessages))
                return true;

            // Ask once, wait a few seconds at most, then quit whatever happened.
            _quitAnswered = true;
            var started = EndSession("quitting", SessionMemories.QuitTimeoutSeconds, Application.Quit);
            return !started;
        }

        /// <summary>
        /// Starts the one request for this session's memories. Returns false when there was
        /// nothing to ask (too short, no provider). <paramref name="done"/> runs when the request
        /// is over, also when it failed or timed out.
        /// </summary>
        bool EndSession(string why, float timeoutSeconds, Action done)
        {
            var userMessages = _sessionUserMessages;
            var startLine = _sessionStartLine;
            DropSession();
            if (!SessionMemories.Qualifies(userMessages) || _conversation == null || settings == null || _registry == null)
                return false;

            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            var providerId = PrivateChatRouting.ProviderId(keepsLocal, settings.providerId, settings.privateEndpointType);
            if (!_registry.TryGet(providerId, out var provider))
                return false;

            var lines = _conversation.Lines;
            var messages = new List<ChatMessage>();
            for (var i = Mathf.Clamp(startLine, 0, lines.Count); i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Kind == ChatLineKind.System || line.Streaming || !line.KeepInHistory)
                    continue;
                messages.Add(new ChatMessage(line.Kind == ChatLineKind.User ? ChatRole.User : ChatRole.Assistant, line.Text));
            }

            var transcript = SessionMemories.BuildTranscript(messages, WorldPromptNow.HerName);
            if (transcript.Length == 0)
                return false;

            // The store of the persona this session belongs to: after a switch MemoryRuntime.Store is another one.
            CaptureSession(provider, transcript, WorldPromptNow.HerName, userMessages, keepsLocal, MemoryRuntime.Store, timeoutSeconds, why, done);
            return true;
        }

        async void CaptureSession(IChatProvider provider, string transcript, string herName, int userMessages, bool keepsLocal,
            MemoryStore store, float timeoutSeconds, string why, Action done)
        {
            try
            {
                // A new thread in her own life only for a persona, while both world switches are on.
                var askThread = herName.Length > 0 && PisaraSwitches.World && PisaraSwitches.AwayEvents;
                var stored = await SessionMemories.CaptureAsync(
                    provider, transcript, herName, userMessages, keepsLocal, IntimateExpressions.Active, store, timeoutSeconds, CancellationToken.None, askThread);
                Debug.Log("Session memories: " + stored.Memories.Count + " stored after " + why + " (" + userMessages + " messages)"
                    + (askThread ? ", new thread: " + (stored.Thread != null ? "yes" : "no") : string.Empty));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session memories: failed (" + e.GetType().Name + ")");
            }

            if (done != null)
                done();
        }
    }
}
