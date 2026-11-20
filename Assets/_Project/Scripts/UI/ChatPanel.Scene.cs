using System;
using System.Collections.Generic;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>JUH-129: the running scene digest. The logic is in <see cref="SceneDigest"/>.</summary>
    public sealed partial class ChatPanel
    {
        const float SceneDigestTimeoutSeconds = 120f;

        readonly SceneDigest _sceneDigest = new SceneDigest();

        /// <summary>The continuity rule, and the scene so far when this send may see it.</summary>
        string WithScene(string prompt, bool keepsLocal, IChatProvider provider)
        {
            // A local or private server may see a digest of a session that stayed local; a cloud model may not.
            return _sceneDigest.Append(prompt, keepsLocal || PisaraReader.MayUse(provider, true));
        }

        /// <summary>
        /// After a finished reply: once the session no longer fits the chat window, ask for a new
        /// digest every few user messages. Same model and privacy as the session-end summary.
        /// </summary>
        void MaybeUpdateSceneDigest()
        {
            if (_conversation == null || settings == null || _registry == null)
                return;

            var lines = _conversation.Lines;
            var start = _sessionStartLine;
            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = SessionLineFilter.MustStayLocal(
                _sessionPrivacy.KeptLocal,
                SessionLineFilter.AnyLocalOnly(lines, start),
                IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode));

            var messages = new List<ChatMessage>();
            SessionLineFilter.Append(messages, lines, start, keepsLocal);
            var characters = 0;
            for (var i = 0; i < messages.Count; i++)
                characters += messages[i].Content != null ? messages[i].Content.Length : 0;
            if (!_sceneDigest.IsDue(_sessionUserMessages, messages.Count, characters,
                    _conversation.MaxMessages, _conversation.MaxCharacters))
                return;

            var providerId = PrivateChatRouting.ProviderId(keepsLocal, settings.providerId, settings.privateEndpointType);
            if (!_registry.TryGet(providerId, out var provider) || !PisaraReader.MayUse(provider, keepsLocal))
                return;

            var transcript = SessionMemories.BuildTranscript(messages, WorldPromptNow.HerName);
            if (transcript.Length == 0)
                return;

            var request = SceneDigest.BuildRequest(_sceneDigest.PreviousFor(keepsLocal), transcript);
            var generation = _sceneDigest.Begin(_sessionUserMessages);
            RunSceneDigest(provider, request, generation, keepsLocal);
        }

        async void RunSceneDigest(IChatProvider provider, ChatRequest request, int generation, bool keepsLocal)
        {
            string text = null;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(SceneDigestTimeoutSeconds)))
            {
                try
                {
                    var result = await provider.StreamChat(request, _ => { }, timeout.Token);
                    if (result.Ok)
                        text = result.Text;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Scene digest: failed (" + e.GetType().Name + ")");
                }
            }

            if (_sceneDigest.Finish(generation, text, keepsLocal))
                Debug.Log("Scene digest: updated (" + _sceneDigest.Text.Length + " characters, local=" + keepsLocal + ")");
        }
    }
}
