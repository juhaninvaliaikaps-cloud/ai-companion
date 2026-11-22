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
        // JUH-178: her lines are no longer part of this reading (the refresh writes them mood by
        // mood), so the reply is short enough for a 12B model to finish.
        public const int EverydayMaxTokens = 1000;
        public const int IntimateMaxTokens = 800;
        public const int RefreshMaxTokens = 300;
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
            "\"stranger\": \"<how she talks to someone she barely knows>\", \"close\": \"<how she talks to someone close>\"}";

        /// <summary>What makes her lines hers: asked in every request that writes lines.</summary>
        public const string LinesRule =
            "Her lines are short things she would actually say out loud, one or two sentences each. Every line must be something only she would say: " +
            "use her world, her habits and her words, never a line any other character could say. Make every line different: a different first word, " +
            "length and shape; never start two lines the same way.\n";

        public const string IntimateSystemPrompt =
            "You read a character for an adults-only companion app, where she and the user are consenting adults, and write down how she talks in intimate and sexual scenes, " +
            "so another model stays in her voice there.\n" +
            "What the user wrote comes first: her description and her 18+ notes decide. Build on what the other readings found: her temperament sliders, " +
            "and how innocent or perverted she is (an innocent one is shy and holds back in words; a perverted one is bold and explicit). " +
            "Her everyday personality stays: an assertive, confident woman stays assertive in bed; a shy one stays shy until she lets go.\n" +
            "Be concrete: what she says, how she asks for things, how explicit her words are. Keep every field short.\n" +
            LinesRule +
            "Reply with only this JSON object and nothing else:\n" +
            "{\"holdingBack\": \"<how she talks when aroused but holding back>\", \"lettingGo\": \"<how she talks when she gives in or takes the lead>\", " +
            "\"habits\": [\"<2 to 4 concrete habits>\"], \"never\": [\"<2 to 4 things she would never say>\"], " +
            "\"lines\": {\"holdingBack\": [\"<3 lines while holding back>\"], \"lettingGo\": [\"<3 lines when letting go>\"]}}";

        public const string RefreshSystemPrompt =
            "You write new lines for a character in a companion app, in her own voice, for one situation. " +
            LinesRule +
            "Never repeat or rephrase a line she has already said. Reply with only this JSON object and nothing else: {\"lines\": [\"\", \"\", \"\"]}";

        public const string IntimateRefreshSystemPrompt =
            "You write new lines for a character in an adults-only companion app, where she and the user are consenting adults, in her own voice, " +
            "for one moment in an intimate scene. " +
            LinesRule +
            "Never repeat or rephrase a line she has already said. Reply with only this JSON object and nothing else: {\"lines\": [\"\", \"\", \"\"]}";

        public static ChatRequest BuildEveryday(string personaName, string input)
        {
            return Build(EverydaySystemPrompt, personaName, input, EverydayMaxTokens, false);
        }

        public static ChatRequest BuildIntimate(string personaName, string input)
        {
            return Build(IntimateSystemPrompt, personaName, input, IntimateMaxTokens, true);
        }

        /// <summary>The refresh input: her voice, the moment, how well she knows the user now, and the lines she has had.</summary>
        public static string RefreshInput(VoiceSheet voice, string moment, bool close, IList<string> hadLines, IntimateVoice intimate = null)
        {
            var sb = new System.Text.StringBuilder();
            if (voice != null)
            {
                sb.Append("How she talks: ").Append(voice.summary).Append(' ').Append(voice.register).Append("; ").Append(voice.rhythm).Append("; ").Append(voice.vocabulary).Append('\n');
                if (voice.habits.Length > 0)
                    sb.Append("Her habits: ").Append(string.Join("; ", voice.habits)).Append('\n');
                if (voice.never.Length > 0)
                    sb.Append("She never: ").Append(string.Join("; ", voice.never)).Append('\n');
            }

            if (intimate != null)
                sb.Append("In intimate scenes, holding back: ").Append(intimate.holdingBack).Append(" Letting go: ").Append(intimate.lettingGo).Append('\n');
            sb.Append("Write 3 new lines for: ").Append(moment).Append('\n');
            sb.Append("How well she knows the user now: ").Append(close ? "someone close" : "still getting to know them").Append('\n');
            if (hadLines != null && hadLines.Count > 0)
            {
                sb.Append("Lines she has already said:\n");
                for (var i = 0; i < hadLines.Count; i++)
                    sb.Append("- ").Append(hadLines[i]).Append('\n');
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>The moment a refresh writes for, from the pool key ("flustered", "holdingBack").</summary>
        public static string Moment(string key)
        {
            switch (key)
            {
                case "content": return "an ordinary moment, in a calm mood";
                case "holdingBack": return "an intimate moment when she is aroused but still holding back";
                case "lettingGo": return "an intimate moment when she gives in or takes the lead";
                default: return "when she is " + key;
            }
        }

        public static ChatRequest BuildRefresh(string personaName, string input, bool intimate)
        {
            return Build(intimate ? IntimateRefreshSystemPrompt : RefreshSystemPrompt, personaName, input, RefreshMaxTokens, intimate);
        }

        [Serializable]
        class LinesReply
        {
            public string[] lines = new string[0];
        }

        /// <summary>New lines from a refresh reply, or an empty array.</summary>
        public static string[] ParseLines(string reply)
        {
            var json = CharacterSheet.ExtractObject(reply) ?? JsonRepair.CloseTruncated(reply);
            if (json == null)
                return new string[0];
            try
            {
                var parsed = UnityEngine.JsonUtility.FromJson<LinesReply>(json);
                return VoiceText.Items(parsed != null ? parsed.lines : null, VoiceSheet.MaxLinesPerMood, VoiceSheet.MaxLine);
            }
            catch (ArgumentException)
            {
                return new string[0];
            }
        }

        /// <summary>
        /// One refresh: new lines for one mood, or (<paramref name="intimate"/>) one 18+ stage. An
        /// 18+ refresh is refused unless the gate is open and the provider is local; an everyday
        /// one follows <paramref name="keepsLocal"/> like the everyday reading. Never throws.
        /// </summary>
        public static async Task<string[]> RefreshAsync(IChatProvider provider, bool keepsLocal, bool intimate, bool gateOpen,
            string personaName, string input, CancellationToken cancellation, float timeoutSeconds = TimeoutSeconds)
        {
            if (intimate && !gateOpen)
                return new string[0];
            if (provider == null || !provider.IsConfigured || !PisaraReader.MayUse(provider, intimate || keepsLocal))
                return new string[0];
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    var call = provider.StreamChat(BuildRefresh(personaName, input, intimate), _ => { }, linked.Token);
                    var limit = Task.Delay(TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds)), linked.Token);
                    var first = await Task.WhenAny(call, limit);
                    if (first != call)
                    {
                        linked.Cancel();
                        call.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        return new string[0];
                    }

                    var reply = await call;
                    linked.Cancel();
                    return reply.Ok ? ParseLines(reply.Text) : new string[0];
                }
                catch (OperationCanceledException)
                {
                    return new string[0];
                }
                catch (Exception)
                {
                    return new string[0];
                }
            }
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

        /// <summary>JUH-178: a reply that cannot be read is asked once more before the reading gives up.</summary>
        public const int ReadTries = 2;

        static async Task<VoiceReadResult> Ask(IChatProvider provider, ChatRequest request, bool intimate, CancellationToken cancellation, float timeoutSeconds)
        {
            VoiceReadResult result = null;
            for (var attempt = 0; attempt < ReadTries; attempt++)
            {
                result = await AskOnce(provider, request, intimate, cancellation, timeoutSeconds);
                if (result.Ok || !result.Failure.StartsWith(BadReply, StringComparison.Ordinal) || cancellation.IsCancellationRequested)
                    return result;
            }

            return result;
        }

        /// <summary>
        /// "the reply had no voice (1834 chars, unfinished)": the length, and whether the JSON
        /// was left open, never the text.
        /// </summary>
        public static string BadReplyDetail(string reply)
        {
            var text = reply ?? string.Empty;
            var finished = CharacterSheet.ExtractObject(text) != null;
            var opened = text.IndexOf('{') >= 0;
            return BadReply + " (" + text.Length + " chars, " + (!opened ? "no JSON" : finished ? "unreadable JSON" : "unfinished") + ")";
        }

        static async Task<VoiceReadResult> AskOnce(IChatProvider provider, ChatRequest request, bool intimate, CancellationToken cancellation, float timeoutSeconds)
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
                return voice == null ? Fail(BadReplyDetail(reply.Text)) : new VoiceReadResult { Ok = true, Intimate = voice };
            }

            var sheet = VoiceSheet.Parse(reply.Text);
            return sheet == null ? Fail(BadReplyDetail(reply.Text)) : new VoiceReadResult { Ok = true, Everyday = sheet };
        }

        static VoiceReadResult Fail(string why)
        {
            return new VoiceReadResult { Ok = false, Failure = why ?? string.Empty };
        }
    }
}
