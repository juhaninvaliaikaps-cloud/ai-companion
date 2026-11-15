using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>What one 18+ secrets reading gave. <see cref="Ok"/> is false when nothing usable came back.</summary>
    public sealed class IntimateSecretsReadResult
    {
        public bool Ok;
        public readonly List<IntimateKink> Kinks = new List<IntimateKink>();
        public readonly List<string> Sensitive = new List<string>();
        public readonly List<string> Shy = new List<string>();
        public string Failure = string.Empty;
    }

    /// <summary>
    /// JUH-125: reads her 18+ secrets from her persona: her kinks (a genre and the specific
    /// thing), where she is most sensitive, and what she is shy to do. Like
    /// <see cref="InnocenceReader"/>, a separate request from the character sheet that runs only
    /// while the 18+ gate is open, and only on the local model or the user's private server.
    /// It never goes to a cloud model, whatever the Pisara model setting says. The counts come
    /// from <see cref="IntimateSecretCounts"/>, so the model is asked for exactly that many.
    /// </summary>
    public static class IntimateSecretsReader
    {
        public const int MaxTokens = 400;
        public const float Temperature = 0.6f;
        public const float TimeoutSeconds = PisaraReader.RequestTimeoutSeconds;

        public const string GateClosed = InnocenceReader.GateClosed;
        public const string NoModel = PisaraReader.NoModel;
        public const string CloudRefused = InnocenceReader.CloudRefused;
        public const string BadReply = "the reply had no secrets";

        public static string SystemPrompt(int kinks, int sensitive, int shy)
        {
            return "You read a character description for an adults-only companion app, where she and the user are consenting adults, " +
                   "and decide her private sexual preferences. They are secrets the user finds out over time.\n" +
                   "- kinks: exactly " + kinks + ". Each has a genre (for example BDSM, roleplay, exhibitionism, praise) and the specific thing " +
                   "within it (what kind of BDSM, what kind of roleplay). Use what the description says; when it says nothing, choose what fits " +
                   "her personality most likely.\n" +
                   "- sensitive: exactly " + sensitive + " places on her body where she gets more aroused than elsewhere.\n" +
                   "- shy: exactly " + shy + " sex acts or positions she is not eager to try while she is still innocent.\n" +
                   "Keep every item short: a few words, no sentences. " +
                   "Reply with only this JSON object and nothing else: " +
                   "{\"kinks\": [{\"genre\": \"\", \"detail\": \"\"}], \"sensitive\": [\"\"], \"shy\": [\"\"]}";
        }

        public static ChatRequest BuildRequest(string personaName, string personaPrompt, int kinks, int sensitive, int shy)
        {
            var name = string.IsNullOrWhiteSpace(personaName) ? "the companion" : personaName.Trim();
            return new ChatRequest
            {
                SystemPrompt = SystemPrompt(kinks, sensitive, shy),
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "Her name: " + name + "\nHer description:\n" + (personaPrompt ?? string.Empty)),
                },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                TimeoutSeconds = TimeoutSeconds,
                BlockCloud = true,
                NoMemory = true,
            };
        }

        [Serializable]
        class Reply
        {
            public List<IntimateKink> kinks = new List<IntimateKink>();
            public List<string> sensitive = new List<string>();
            public List<string> shy = new List<string>();
        }

        /// <summary>The lists in a model reply, or false when it has none. Accepts code fences and text around the object.</summary>
        public static bool TryParse(string reply, IntimateSecretsReadResult result)
        {
            var json = CharacterSheet.ExtractObject(reply);
            if (json == null || result == null)
                return false;
            Reply parsed;
            try
            {
                parsed = JsonUtility.FromJson<Reply>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (parsed == null)
                return false;
            if (parsed.kinks != null)
            {
                for (var i = 0; i < parsed.kinks.Count; i++)
                {
                    if (parsed.kinks[i] != null && parsed.kinks[i].Text.Length > 0)
                        result.Kinks.Add(parsed.kinks[i]);
                }
            }

            AddPhrases(parsed.sensitive, result.Sensitive);
            AddPhrases(parsed.shy, result.Shy);
            return result.Kinks.Count + result.Sensitive.Count + result.Shy.Count > 0;
        }

        static void AddPhrases(List<string> from, List<string> to)
        {
            if (from == null)
                return;
            for (var i = 0; i < from.Count; i++)
            {
                var text = HerSecrets.Clean(from[i]);
                if (text.Length > 0)
                    to.Add(text);
            }
        }

        /// <summary>
        /// Asks the model once. Refused before anything is sent when the gate is closed or the
        /// provider is not the local model or the private server. Never throws.
        /// </summary>
        public static async Task<IntimateSecretsReadResult> ReadAsync(
            IChatProvider provider,
            string personaName,
            string personaPrompt,
            int kinks,
            int sensitive,
            int shy,
            bool gateOpen,
            CancellationToken cancellation,
            float timeoutSeconds = TimeoutSeconds)
        {
            if (!gateOpen)
                return Fail(GateClosed);
            if (provider == null || !provider.IsConfigured)
                return Fail(NoModel);
            if (!PisaraReader.MayUse(provider, true))
                return Fail(CloudRefused);

            ChatResult reply;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    var call = provider.StreamChat(BuildRequest(personaName, personaPrompt, kinks, sensitive, shy), _ => { }, linked.Token);
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

            var result = new IntimateSecretsReadResult();
            if (!TryParse(reply.Text, result))
                return Fail(BadReply);
            result.Ok = true;
            return result;
        }

        static IntimateSecretsReadResult Fail(string why)
        {
            return new IntimateSecretsReadResult { Ok = false, Failure = why ?? string.Empty };
        }
    }
}
