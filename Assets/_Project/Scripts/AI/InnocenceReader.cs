using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>What one innocence reading gave. <see cref="Ok"/> is false when nothing usable came back.</summary>
    public sealed class InnocenceReadResult
    {
        public bool Ok;
        public float Value = -1f;
        public string Reason = string.Empty;
        public string Failure = string.Empty;
    }

    /// <summary>
    /// JUH-116: reads where she most likely sits between Innocent (0) and Perverted (100)
    /// from her persona: her age, personality and anything the description says about her
    /// experience. A separate request from the character sheet, because the sheet may be read
    /// by a cloud model and this one never is: it runs only while the 18+ gate is open, and only
    /// on the local model or the user's private server (<see cref="PisaraReader.MayUse"/> with
    /// keepsLocal). The call skips the memory layer.
    /// </summary>
    public static class InnocenceReader
    {
        public const int MaxTokens = 160;
        public const float Temperature = 0.3f;
        public const float TimeoutSeconds = PisaraReader.RequestTimeoutSeconds;
        public const int MaxReasonLength = 160;

        public const string GateClosed = "18+ is off";
        public const string NoModel = PisaraReader.NoModel;
        public const string CloudRefused = "only the local model may read this";
        public const string BadReply = "the reply had no number";

        public const string SystemPrompt =
            "You read a character description for an adults-only companion app and judge one trait: where she sits between innocent and perverted.\n" +
            "0 = completely innocent: little or no sexual experience, easily embarrassed, never makes the first move.\n" +
            "50 = in between: some experience, curious, neither shy nor bold.\n" +
            "100 = perverted: very experienced and used to sexual things, bold, often makes the first move.\n" +
            "Judge what fits her most likely. Use everything the description gives: her age, her personality and how confident or shy she is, " +
            "her relationships and anything it says about her experience. A shy 18-year-old who has never dated is near 10; " +
            "a confident 26-year-old who bullies and teases is near 70. When the description says little, stay near 50.\n" +
            "Reply with only this JSON object and nothing else: {\"innocence\": <0 to 100>, \"reason\": \"<one short sentence>\"}";

        public static ChatRequest BuildRequest(string personaName, string personaPrompt)
        {
            var name = string.IsNullOrWhiteSpace(personaName) ? "the companion" : personaName.Trim();
            return new ChatRequest
            {
                SystemPrompt = SystemPrompt,
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
            public float innocence = -1f;
            public string reason;
        }

        /// <summary>The value and reason in a model reply, or false. Accepts code fences and text around the object.</summary>
        public static bool TryParse(string reply, out float value, out string reason)
        {
            value = -1f;
            reason = string.Empty;
            var json = CharacterSheet.ExtractObject(reply);
            if (json == null || json.IndexOf("\"innocence\"", StringComparison.Ordinal) < 0)
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

            if (parsed == null || float.IsNaN(parsed.innocence) || parsed.innocence < 0f)
                return false;
            // Some models answer on a 0 to 1 scale: a fraction such as 0.7 means 70.
            var raw = parsed.innocence > 0f && parsed.innocence < 1f ? parsed.innocence * 100f : parsed.innocence;
            value = InnocenceMath.Clamp(raw);
            reason = (parsed.reason ?? string.Empty).Trim();
            if (reason.Length > MaxReasonLength)
                reason = reason.Substring(0, MaxReasonLength).TrimEnd();
            return true;
        }

        /// <summary>
        /// Asks the model once. Refused before anything is sent when the gate is closed or the
        /// provider is not the local model or the private server. Never throws.
        /// </summary>
        public static async Task<InnocenceReadResult> ReadAsync(
            IChatProvider provider,
            string personaName,
            string personaPrompt,
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
                    var call = provider.StreamChat(BuildRequest(personaName, personaPrompt), _ => { }, linked.Token);
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

            float value;
            string reason;
            if (!TryParse(reply.Text, out value, out reason))
                return Fail(BadReply);
            return new InnocenceReadResult { Ok = true, Value = value, Reason = reason };
        }

        static InnocenceReadResult Fail(string why)
        {
            return new InnocenceReadResult { Ok = false, Failure = why ?? string.Empty };
        }

        /// <summary>"Read from her description: 30 (innocent): shy, has never dated."</summary>
        public static string Describe(float value, string reason)
        {
            var line = "Read from her description: " + InnocenceMath.Clamp(value).ToString("0", CultureInfo.InvariantCulture)
                + " (" + InnocenceMath.BandName(value) + ")";
            return string.IsNullOrEmpty(reason) ? line : line + ": " + reason;
        }
    }
}
