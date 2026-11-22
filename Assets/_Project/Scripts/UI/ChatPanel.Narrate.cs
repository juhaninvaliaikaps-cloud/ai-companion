using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-188: /describe is a narrator turn, not her speaking. It gets a separate prompt,
    /// is drawn as scene text, is never voiced, and does not touch relationship or memory.
    /// </summary>
    public sealed partial class ChatPanel
    {
        bool TryDescribe(string query)
        {
            if (_conversation == null || _conversation.IsBusy || settings == null || _registry == null)
                return false;

            var privacy = GetComponent<IPrivacyMode>();
            var privateMode = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            var providerId = PrivateChatRouting.ProviderId(privateMode, settings.providerId, settings.privateEndpointType);
            if (!_registry.TryGet(providerId, out var provider) || provider == null)
            {
                _conversation.AddSystem(ChatErrors.UnknownProvider(providerId));
                RefreshTranscript(true);
                return false;
            }

            var instruction = string.IsNullOrWhiteSpace(query) ? "the current scene" : query.Trim();
            var name = string.IsNullOrWhiteSpace(WorldPromptNow.HerName) ? "her" : WorldPromptNow.HerName.Trim();
            var system = new System.Text.StringBuilder(512);
            system.Append("You are a neutral, third-person narrator. You are not ").Append(name)
                .Append(" and you never speak as ").Append(name).Append(". ")
                .Append("Describe only what is already established or visible in the scene. ")
                .Append("Use present tense, third person. Do not write dialogue, do not use first person, ")
                .Append("do not add new people, objects, events or hidden information. ")
                .Append("If the scene does not establish something, say that it is not clear rather than inventing it.");

            var scene = _sceneDigest.PreviousFor(privateMode);
            if (!string.IsNullOrWhiteSpace(scene))
                system.Append("\n\nScene so far:\n").Append(scene.Trim());

            var request = new ChatRequest
            {
                SystemPrompt = system.ToString(),
                Temperature = 0.35f,
                MaxTokens = 500,
                TimeoutSeconds = 90f,
                NoMemory = true,
            };

            var messages = new List<ChatMessage>();
            var lines = _conversation.Lines;
            var start = Math.Max(_sessionStartLine, lines.Count - 12);
            SessionLineFilter.Append(messages, lines, start, privateMode);
            request.Messages.Add(new ChatMessage(ChatRole.User, "Narrate/describe: " + instruction));

            _conversation.AddSystem("Describing...");
            RefreshTranscript(true);
            RunDescribe(provider, request, privateMode);
            return true;
        }

        async void RunDescribe(IChatProvider provider, ChatRequest request, bool privateMode)
        {
            ChatResult result;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
            {
                try
                {
                    result = await provider.StreamChat(request, _ => { }, timeout.Token);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Describe: failed (" + e.GetType().Name + ")");
                    result = ChatResult.Fail(ChatErrorKind.Unreachable, "The narrator could not answer.");
                }
            }

            if (!_alive || _conversation == null)
                return;
            if (!result.Ok)
            {
                _conversation.AddSystem(string.IsNullOrWhiteSpace(result.UserMessage)
                    ? "The narrator could not answer."
                    : result.UserMessage);
                RefreshTranscript(true);
                return;
            }

            var text = ScreenLookCue.Strip(result.Text).Trim();
            if (text.Length == 0)
            {
                _conversation.AddSystem("The narrator had nothing to describe.");
                RefreshTranscript(true);
                return;
            }

            _conversation.AddScene(text, privateMode);
            RefreshTranscript(true);
            Debug.Log("Describe: narration added (" + text.Length + " characters, local=" + privateMode + ")");
        }
    }
}
