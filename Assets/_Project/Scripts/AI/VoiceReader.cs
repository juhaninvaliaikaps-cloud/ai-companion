using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Core;

namespace AICompanion.AI
{
    /// <summary>What one voice reading gave. Exactly one of the two results is set when <see cref="Ok"/> is true.</summary>
    public sealed class VoiceReadResult
    {
        public bool Ok;
        public VoiceSheet Everyday;
        public IntimateVoice Intimate;
        public string Failure = string.Empty;
    }

    /// <summary>
    /// JUH-171: the last Pisara reading, how she talks. Two requests that start together, each on
    /// its own model (Jussi, 2026-11-22):
    /// <list type="bullet">
    /// <item>Everyday: built gate-closed, read by the same model as her description (the Pisara
    /// cloud model when that setting is on), but on the local model in private mode.</item>
    /// <item>Intimate: 18+ only, read only while the gate is open and only on the local model or
    /// the user's private server, whatever the Pisara model setting says.</item>
    /// </list>
    /// Both get what the other readings found (her sliders, mood temperament, how she starts
    /// with the user; the 18+ one also her innocence), and what the user wrote comes first.
    /// </summary>
    public static class VoiceReader
    {
        public const int EverydayMaxTokens = 700;
        public const int IntimateMaxTokens = 450;
        public const float Temperature = 0.6f;
        public const float TimeoutSeconds = PisaraReader.RequestTimeoutSeconds;

        public const string GateClosed = InnocenceReader.GateClosed;
        public const string NoModel = PisaraReader.NoModel;
        public const string CloudRefused = InnocenceReader.CloudRefused;
        public const string PrivateRefused = PisaraReader.CloudRefused;
        public const string BadReply = "the reply had no voice";

        public const string EverydaySystemPrompt =
            "You read a character for a companion app and write down exactly how she talks, so another model can stay in her voice in every reply.\n" +
            "What the user wrote comes first: when her description has example lines, dialogue or notes on how she speaks, copy that style and never contradict it. " +
            "Build on what the other readings found: her temperament sliders (for example, high assertiveness and confidence means she speaks with authority even when flustered; " +
            "low confidence means she hedges), her mood temperament and how she starts with the user.\n" +
            "Be concrete: name real habits (how long her sentences are, how she addresses the user, words she uses, what she does when embarrassed), not adjectives. " +
            "Keep every field short. No narration about the user.\n" +
            "Reply with only this JSON object and nothing else:\n" +
            "{\"summary\": \"<one sentence: how she sounds>\", \"register\": \"<formal, casual, crude, old-fashioned...>\", " +
            "\"rhythm\": \"<sentence length and pace>\", \"vocabulary\": \"<the kind of words she uses>\", " +
            "\"habits\": [\"<3 to 5 concrete speech habits>\"], \"never\": [\"<3 to 5 things she would never say or do when talking>\"], " +
            "\"happy\": \"<how her speech changes when happy>\", \"annoyed\": \"<when annoyed>\", \"flustered\": \"<when flustered or embarrassed>\", " +
            "\"sad\": \"<when sad>\", \"affectionate\": \"<when affectionate>\", " +
            "\"stranger\": \"<how she talks to someone she barely knows>\", \"close\": \"<how she talks to someone close>\", " +
            "\"samples\": [\"<2 short lines in her voice, different situations>\"]}";

        public const string IntimateSystemPrompt =
            "You read a character for an adults-only companion app, where she and the user are consenting adults, and write down how she talks in intimate and sexual scenes, " +
            "so another model stays in her voice there.\n" +
            "What the user wrote comes first: her description and her 18+ notes decide. Build on what the other readings found: her temperament sliders, " +
            "and how innocent or perverted she is (an innocent one is shy and holds back in words; a perverted one is bold and explicit). " +
            "Her everyday personality stays: an assertive, confident woman stays assertive in bed; a shy one stays shy until she lets go.\n" +
            "Be concrete: what she says, how she asks for things, how explicit her words are. Keep every field short.\n" +
            "Reply with only this JSON object and nothing else:\n" +
            "{\"holdingBack\": \"<how she talks when aroused but holding back>\", \"lettingGo\": \"<how she talks when she gives in or takes the lead>\", " +
            "\"habits\": [\"<2 to 4 concrete habits>\"], \"never\": [\"<2 to 4 things she would never say>\"], \"samples\": [\"<2 short lines>\"]}";

        public static ChatRequest BuildEveryday(string personaName, string input)
        {
            return Build(EverydaySystemPrompt, personaName, input, EverydayMaxTokens, false);
        }

        public static ChatRequest BuildIntimate(string personaName, string input)
        {
            return Build(IntimateSystemPrompt, personaName, input, IntimateMaxTokens, true);
        }

        static ChatRequest Build(string system, string personaName, string input, int maxTokens, bool blockCloud)
        {
            var name = string.IsNullOrWhiteSpace(personaName) ? "the companion" : personaName.Trim();
            return new ChatRequest
            {
                SystemPrompt = system,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "Character name: " + name + "\n\n" + (input ?? string.Empty)),
                },
                Temperature = Temperature,
                MaxTokens = maxTokens,
                TimeoutSeconds = TimeoutSeconds,
                BlockCloud = blockCloud,
                NoMemory = true,
            };
        }

        /// <summary>
        /// The everyday reading. <paramref name="keepsLocal"/> is true in private mode: a cloud
        /// provider is then refused before anything is sent. Never throws.
        /// </summary>
        public static Task<VoiceReadResult> ReadEverydayAsync(IChatProvider provider, bool keepsLocal, string personaName, string input,
            CancellationToken cancellation, float timeoutSeconds = TimeoutSeconds)
        {
            if (provider == null || !provider.IsConfigured)
                return Task.FromResult(Fail(NoModel));
            if (!PisaraReader.MayUse(provider, keepsLocal))
                return Task.FromResult(Fail(PrivateRefused));
            return Ask(provider, BuildEveryday(personaName, input), false, cancellation, timeoutSeconds);
        }

        /// <summary>The 18+ reading: refused unless the gate is open and the provider is local or the private server. Never throws.</summary>
        public static Task<VoiceReadResult> ReadIntimateAsync(IChatProvider provider, bool gateOpen, string personaName, string input,
            CancellationToken cancellation, float timeoutSeconds = TimeoutSeconds)
        {
            if (!gateOpen)
                return Task.FromResult(Fail(GateClosed));
            if (provider == null || !provider.IsConfigured)
                return Task.FromResult(Fail(NoModel));
            if (!PisaraReader.MayUse(provider, true))
                return Task.FromResult(Fail(CloudRefused));
            return Ask(provider, BuildIntimate(personaName, input), true, cancellation, timeoutSeconds);
        }

        static async Task<VoiceReadResult> Ask(IChatProvider provider, ChatRequest request, bool intimate, CancellationToken cancellation, float timeoutSeconds)
        {
            ChatResult reply;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    var call = provider.StreamChat(request, _ => { }, linked.Token);
                    var limit = Task.Delay(TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds)), linked.Token);
                    var first = await Task.WhenAny(call, limit);
                    if (first != call)
                    {
                        linked.Cancel();
                        call.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        return Fail(cancellation.IsCancellationRequested ? "cancelled" : PisaraReader.TimedOut);
                    }

                    reply = await call;
                    linked.Cancel();
                }
                catch (OperationCanceledException)
                {
                    return Fail("cancelled");
                }
                catch (Exception e)
                {
                    return Fail("the call failed (" + e.GetType().Name + ")");
                }
            }

            if (!reply.Ok)
                return Fail("the model did not answer (" + reply.Error + ")");
            if (intimate)
            {
                var voice = IntimateVoice.Parse(reply.Text);
                return voice == null ? Fail(BadReply) : new VoiceReadResult { Ok = true, Intimate = voice };
            }

            var sheet = VoiceSheet.Parse(reply.Text);
            return sheet == null ? Fail(BadReply) : new VoiceReadResult { Ok = true, Everyday = sheet };
        }

        static VoiceReadResult Fail(string why)
        {
            return new VoiceReadResult { Ok = false, Failure = why ?? string.Empty };
        }
    }
}
