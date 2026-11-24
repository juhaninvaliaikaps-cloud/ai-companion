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

        /// <summary>JUH-170: her libido (0 to 100), or -1 when the reply had none.</summary>
        public float Libido = -1f;

        public string Reason = string.Empty;
        public string Failure = string.Empty;
    }

    /// <summary>
    /// JUH-116: reads where she most likely sits between Innocent (0) and Perverted (100)
    /// from her persona: explicit experience and behaviour, plus personality traits. Age is only
    /// a small negative prior for the youngest adults, never proof. A separate request from the
    /// character sheet, because the sheet may be read
    /// by a cloud model and this one never is: it runs only while the 18+ gate is open, and only
    /// on the local model or the user's private server (<see cref="PisaraReader.MayUse"/> with
    /// keepsLocal). The call skips the memory layer.
    /// </summary>
    public static class InnocenceReader
    {
        public const int MaxTokens = 200;
        public const float Temperature = 0.3f;
        public const float TimeoutSeconds = PisaraReader.RequestTimeoutSeconds;
        public const int MaxReasonLength = 160;

        public const string GateClosed = "18+ is off";
        public const string NoModel = PisaraReader.NoModel;
        public const string CloudRefused = "only the local model may read this";
        public const string BadReply = "the reply had no number";

        public const string SystemPrompt =
            "You read a character description for an adults-only companion app and judge two traits.\n" +
            "First, innocence: where she sits between innocent and perverted.\n" +
            "0 = completely innocent: little or no sexual experience, easily embarrassed, never makes the first move.\n" +
            "50 = in between: some experience, curious, neither shy nor bold.\n" +
            "100 = perverted: very experienced and used to sexual things, bold, often makes the first move.\n" +
            "Judge what fits her most likely from two signals:\n" +
            "- 65% from explicit experience and behaviour in the description: what she has done, how she talks about it, whether she initiates, her relationships and her stated history.\n" +
            "- 35% from personality traits: how assertive, open, playful, confident and curious she is, how comfortable she is with desire, and how well a lot of sex fits who she is.\n" +
            "Age is only a small prior, and only when the description does not explicitly say otherwise. Apply at most: 18-19 = -5, 20-22 = -3, 23-24 = -1, 25+ = 0. Never add points for being older. " +
            "A young persona can be highly experienced and a older persona can be inexperienced; explicit experience outweighs age.\n" +
            "A shy, reserved persona who has never dated is near 10; a bold, teasing persona who is comfortable with desire and takes the lead is near 70. " +
            "When the description says little, stay near 50.\n" +
            "What the user wrote comes first: when her 18+ notes say how experienced, shy or bold she is, that decides it.\n" +
            "Second, libido: how strong and relentless her sexual needs are. This is separate from experience: a shy virgin can have a high libido. " +
            "0 = she rarely feels desire; 50 = ordinary; 100 = insatiable, her needs come back again and again. " +
            "When the description says little, stay near 50; when her 18+ notes say it, that decides it.\n" +
            "Reply with only this JSON object and nothing else: {\"innocence\": <0 to 100>, \"libido\": <0 to 100>, \"reason\": \"<one short sentence>\"}";

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
            public float libido = -1f;
            public string reason;
        }

        /// <summary>The value and reason in a model reply, or false. Accepts code fences and text around the object.</summary>
        public static bool TryParse(string reply, out float value, out string reason)
        {
            float libido;
            return TryParse(reply, out value, out libido, out reason);
        }

        /// <summary>As above, with her libido (JUH-170), or -1 when the reply had none.</summary>
        public static bool TryParse(string reply, out float value, out float libido, out string reason)
        {
            value = -1f;
            libido = -1f;
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
            if (!float.IsNaN(parsed.libido) && parsed.libido >= 0f && json.IndexOf("\"libido\"", StringComparison.Ordinal) >= 0)
            {
                var rawLibido = parsed.libido > 0f && parsed.libido < 1f ? parsed.libido * 100f : parsed.libido;
                libido = DesireMath.ClampLibido(rawLibido);
            }

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
            float libido;
            string reason;
            if (!TryParse(reply.Text, out value, out libido, out reason))
                return Fail(BadReply);
            return new InnocenceReadResult { Ok = true, Value = value, Libido = libido, Reason = reason };
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
