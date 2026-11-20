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
    /// than four user messages are skipped. Quitting writes an encrypted handoff first, so a
    /// slow local model is retried on the next start instead of losing the session.</item>
    /// </list>
    /// </summary>
    public sealed partial class ChatPanel
    {
        int _sessionUserMessages;
        readonly SessionPrivacy _sessionPrivacy = new SessionPrivacy();
        int _sessionStartLine;
        float _sessionLastMessageAt;
        bool _quitHooked;
        bool _quitAnswered;
        bool _resumeRunning;

        // JUH-134 review: each session has its own handoff file. The rolling checkpoint and
        // the final save share the id; a new session gets a new one.
        string _sessionId = string.Empty;
        readonly HashSet<string> _sessionsCapturing = new HashSet<string>(StringComparer.Ordinal);

        string SessionId
        {
            get
            {
                if (_sessionId.Length == 0)
                    _sessionId = Guid.NewGuid().ToString("N");
                return _sessionId;
            }
        }

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
            // JUH-116: where she sits between Innocent and Perverted; empty while the 18+ gate is closed.
            prompt = InnocenceNow.Append(prompt);
            // JUH-125: her 18+ secrets; empty while the gate is closed (the chat is local while it is open).
            prompt = IntimateSecretsNow.Append(prompt);
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

        /// <summary>Called just before a user message is sent, with that send's private flag.</summary>
        void SessionBeforeSend(bool keepsLocal)
        {
            if (_conversation != null)
            {
                // On startup the loaded transcript is still the current conversation, so the
                // first send may include its recent lines. A real session end sets the start
                // to the next line, keeping earlier sessions out of the raw prompt.
                _conversation.HistoryStart = _sessionStartLine;
            }
            NoteSessionPrivacy(keepsLocal);
        }

        /// <summary>
        /// Every send notes whether it had to stay local (screen looks too), so a session that was
        /// private at any point stays private at its end.
        /// </summary>
        void NoteSessionPrivacy(bool keepsLocal)
        {
            _sessionPrivacy.Note(keepsLocal);
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

        /// <summary>
        /// Finishes a session that the previous process had to hand over. The raw transcript
        /// is already encrypted on disk; a local model can take its time now, and a timeout
        /// leaves the handoff for the next start.
        /// </summary>
        public void ResumePendingSession()
        {
            if (_resumeRunning)
                return;
            _resumeRunning = true;
            ResumePendingSessionNow();
        }

        async void ResumePendingSessionNow()
        {
            try
            {
                await ResumePendingSessionCore();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session handoff: finish failed (" + e.GetType().Name + "); keeping it pending.");
            }
            finally
            {
                _resumeRunning = false;
            }
        }

        /// <summary>JUH-141: the day the pending conversation happened, so its dates are written for that day.</summary>
        static DateTime? PendingLocalDate(PendingSession pending)
        {
            DateTime at;
            if (pending != null && DateTime.TryParse(pending.createdAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out at))
                return at.ToLocalTime();
            return null;
        }

        async System.Threading.Tasks.Task ResumePendingSessionCore()
        {
            List<PendingSession> all;
            try
            {
                all = SessionHandoff.LoadAll();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session handoff: load failed (" + e.GetType().Name + ")");
                return;
            }

            // Oldest first. The running session's checkpoint and a session whose summary is
            // already being written are not finished twice.
            for (var i = 0; i < all.Count; i++)
            {
                var pending = all[i];
                if (pending.session.Length > 0
                    && (string.Equals(pending.session, _sessionId, StringComparison.Ordinal)
                        || _sessionsCapturing.Contains(pending.session)))
                    continue;
                await ResumeOne(pending);
            }
        }

        async System.Threading.Tasks.Task ResumeOne(PendingSession pending)
        {
            if (_registry == null || !_registry.TryGet(pending.providerId, out var provider))
            {
                Debug.Log("Session handoff: waiting for provider " + pending.providerId);
                return;
            }

            if (!PisaraReader.MayUse(provider, pending.keepsLocal))
            {
                Debug.Log("Session handoff: provider is not allowed for this private session; keeping it pending.");
                return;
            }

            Debug.Log("Session handoff: provider=" + provider.ProviderId + " localModel="
                + (settings != null ? settings.localModel : string.Empty));

            // The live store when it is the same persona, so two instances never overwrite each other.
            var live = MemoryRuntime.Store;
            var store = live != null && string.Equals(live.Path, pending.memoryPath, StringComparison.OrdinalIgnoreCase)
                ? live
                : MemoryStore.Load(pending.memoryPath);
            var stored = await SessionMemories.CaptureAsync(
                provider, pending.transcript, pending.herName, pending.userMessages,
                pending.keepsLocal, pending.intimateActive, store,
                SessionMemories.DefaultTimeoutSeconds, CancellationToken.None, false, PendingLocalDate(pending));

            Debug.Log("Session handoff: finished " + pending.why + " with " + stored.Memories.Count + " memories (answered=" + stored.Answered + ")");

            // JUH-141: new lines, corrections and closed promises; the handoff's own date is "today".
            var timeline = RelationshipTimeline.ForStore(store);
            var applied = timeline.ApplySession(stored, pending.keepsLocal, DateTime.UtcNow, store);
            if (applied.Added + applied.Updated + applied.Resolved > 0)
                Debug.Log("Relationship timeline: from the handoff " + applied.Added + " added, " + applied.Updated + " updated, " + applied.Resolved + " resolved");

            if (stored.Answered)
                SessionHandoff.Clear(pending);
        }

        /// <summary>
        /// Keeps a crash-safe copy of the session after every finished reply. A long
        /// conversation no longer has to reach the quiet ten-minute end before anything is
        /// recoverable. It is encrypted and overwritten by the normal final handoff.
        /// </summary>
        void SaveRollingCheckpoint()
        {
            if (!SessionMemories.Qualifies(_sessionUserMessages) || _conversation == null
                || settings == null || _registry == null)
                return;

            var privacy = GetComponent<IPrivacyMode>();
            var lines = _conversation.Lines;
            var keepsLocal = SessionLineFilter.MustStayLocal(
                _sessionPrivacy.KeptLocal,
                SessionLineFilter.AnyLocalOnly(lines, _sessionStartLine),
                IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode));
            var providerId = PrivateChatRouting.ProviderId(keepsLocal, settings.providerId, settings.privateEndpointType);
            if (!_registry.TryGet(providerId, out var provider))
                return;

            var messages = new List<ChatMessage>();
            SessionLineFilter.Append(messages, lines, _sessionStartLine, keepsLocal);
            var transcript = SessionMemories.BuildTranscript(messages, WorldPromptNow.HerName);
            if (transcript.Length == 0)
                return;

            SessionHandoff.Save(SessionId, transcript, WorldPromptNow.HerName, provider.ProviderId,
                MemoryRuntime.Store, "a rolling checkpoint", _sessionUserMessages, keepsLocal,
                IntimateExpressions.Active);
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
            _sessionId = string.Empty;
            _sceneDigest.Reset();
            _sessionUserMessages = 0;
            _sessionStartLine = _conversation != null ? _conversation.Lines.Count : 0;
            if (_conversation != null)
                _conversation.HistoryStart = 0;
            _sessionPrivacy.Reset();
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
            var keptLocal = _sessionPrivacy.KeptLocal;
            var sessionId = SessionId;
            DropSession();
            if (!SessionMemories.Qualifies(userMessages) || _conversation == null || settings == null || _registry == null)
                return false;

            var privacy = GetComponent<IPrivacyMode>();
            var lines = _conversation.Lines;
            // A restored adult line counts as private even when this process never saw the send.
            var hasLocalOnly = SessionLineFilter.AnyLocalOnly(lines, startLine);
            var keepsLocal = SessionLineFilter.MustStayLocal(
                keptLocal,
                hasLocalOnly,
                IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode));
            var providerId = PrivateChatRouting.ProviderId(keepsLocal, settings.providerId, settings.privateEndpointType);
            if (!_registry.TryGet(providerId, out var provider))
                return false;

            var messages = new List<ChatMessage>();
            SessionLineFilter.Append(messages, lines, startLine, keepsLocal);

            var transcript = SessionMemories.BuildTranscript(messages, WorldPromptNow.HerName);
            if (transcript.Length == 0)
                return false;

            // Write the handoff before the model call. A local model or a quit can miss the
            // four-second window; the next start retries this exact session.
            var pending = SessionHandoff.Save(
                sessionId, transcript, WorldPromptNow.HerName, provider.ProviderId, MemoryRuntime.Store,
                why, userMessages, keepsLocal, IntimateExpressions.Active);
            _sessionsCapturing.Add(sessionId);

            // The store of the persona this session belongs to: after a switch MemoryRuntime.Store is another one.
            CaptureSession(provider, transcript, WorldPromptNow.HerName, userMessages, keepsLocal, MemoryRuntime.Store, timeoutSeconds, why, pending, sessionId, done);
            return true;
        }

        async void CaptureSession(IChatProvider provider, string transcript, string herName, int userMessages, bool keepsLocal,
            MemoryStore store, float timeoutSeconds, string why, PendingSession pending, string sessionId, Action done)
        {
            try
            {
                // A new thread in her own life only for a persona, while both world switches are on.
                var askThread = herName.Length > 0 && PisaraSwitches.World && PisaraSwitches.AwayEvents;
                var stored = await SessionMemories.CaptureAsync(
                    provider, transcript, herName, userMessages, keepsLocal, IntimateExpressions.Active, store, timeoutSeconds, CancellationToken.None, askThread);
                Debug.Log("Session memories: " + stored.Memories.Count + " stored after " + why + " (" + userMessages + " messages)"
                    + (askThread ? ", new thread: " + (stored.Thread != null ? "yes" : "no") : string.Empty));

                // JUH-134: the session summary is not enough on its own; keep a dated
                // relationship entry that Pisara reseeding and the short-term fade cannot remove.
                // JUH-141: corrections ("update m1") and closed promises ("resolved m2") too.
                var timeline = RelationshipTimeline.ForStore(store);
                var applied = timeline.ApplySession(stored, keepsLocal, DateTime.UtcNow, store);
                if (applied.Added + applied.Updated + applied.Resolved > 0)
                    Debug.Log("Relationship timeline: after " + why + " " + applied.Added + " added, " + applied.Updated + " updated, " + applied.Resolved + " resolved");

                // Only a real answer clears the handoff. A timeout stays pending for startup.
                if (stored.Answered)
                    SessionHandoff.Clear(pending);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session memories: failed (" + e.GetType().Name + ")");
            }

            _sessionsCapturing.Remove(sessionId);
            if (done != null)
                done();
        }
    }

    /// <summary>
    /// Which transcript lines a session-end request may carry. Local-only lines (adult
    /// interaction turns) stay in a local request and are left out of a cloud one.
    /// </summary>
    public static class SessionLineFilter
    {
        public static bool AnyLocalOnly(IReadOnlyList<ChatLine> lines, int start)
        {
            if (lines == null)
                return false;
            var first = start < 0 ? 0 : start;
            for (var i = first; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line != null && line.LocalOnly && line.KeepInHistory && !string.IsNullOrEmpty(line.Text))
                    return true;
            }

            return false;
        }

        /// <summary>A session that contains a local-only turn stays local at its end.</summary>
        public static bool MustStayLocal(bool keptLocal, bool transcriptHasLocalOnly, bool keepsLocalNow)
        {
            return keptLocal || transcriptHasLocalOnly || keepsLocalNow;
        }

        public static void Append(List<ChatMessage> messages, IReadOnlyList<ChatLine> lines, int start, bool includeLocalOnly)
        {
            if (messages == null || lines == null)
                return;
            var first = start < 0 ? 0 : start;
            if (first > lines.Count)
                first = lines.Count;
            for (var i = first; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || line.Kind == ChatLineKind.System || line.Streaming || !line.KeepInHistory)
                    continue;
                if (!includeLocalOnly && line.LocalOnly)
                    continue;
                if (string.IsNullOrEmpty(line.Text))
                    continue;
                var role = line.Kind == ChatLineKind.User ? ChatRole.User : ChatRole.Assistant;
                messages.Add(new ChatMessage(role, line.Text));
            }
        }
    }
}
