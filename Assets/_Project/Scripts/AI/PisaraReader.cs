using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Core;

namespace AICompanion.AI
{
    /// <summary>What one reading gave.</summary>
    public sealed class PisaraReadResult
    {
        /// <summary>Never null: the model's sheet, or the fallback. Stamped and clamped.</summary>
        public CharacterSheet Sheet;

        /// <summary>True when a request was sent to a model.</summary>
        public bool Called;

        /// <summary>Empty when the model's sheet was used; otherwise why it was not.</summary>
        public string Failure = string.Empty;

        /// <summary>The model's raw reply, for the tuning tool only. Never saved or logged by the app.</summary>
        public string RawReply = string.Empty;

        public bool FromModel
        {
            get { return Sheet != null && Sheet.source == CharacterSheet.SourceModel; }
        }
    }

    /// <summary>
    /// The reading (JUH-118, docs/PISARA.md): asks a model once for the character sheet and
    /// parses it. No model, a refusal by the privacy rule, a timeout, an error or bad JSON all
    /// end in the fallback sheet, never in an exception.
    /// <para>
    /// Privacy: when <c>keepsLocal</c> is true (Private mode, or an open intimate gate, see
    /// <see cref="IntimateExpressions.KeepsLocal"/>) only the local model or the user's own
    /// private server may be asked. A cloud provider is refused before anything is sent.
    /// </para>
    /// </summary>
    public static class PisaraReader
    {
        /// <summary>A full sheet is about 900 to 1400 tokens of JSON.</summary>
        public const int MaxTokens = 2200;
        public const float Temperature = 0.5f;
        public const float DefaultTimeoutSeconds = 180f;

        public const string NoModel = "no model is set up";
        public const string CloudRefused = "private: a cloud model may not read this persona";
        public const string TimedOut = "the model took too long";
        public const string BadJson = "the reply was not a usable sheet";

        /// <summary>True when this provider may be sent the persona prompt under the privacy rule.</summary>
        public static bool MayUse(IChatProvider provider, bool keepsLocal)
        {
            if (provider == null)
                return false;
            if (!keepsLocal)
                return true;
            return string.Equals(provider.ProviderId, ChatDefaults.LocalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider.ProviderId, ChatDefaults.CustomId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The request. The consent rule (<see cref="IntimateBoundaries"/>) is part of the system
        /// prompt, like in every other prompt.
        /// </summary>
        public static ChatRequest BuildRequest(string personaName, string personaPrompt, bool keepsLocal, bool intimateActive, string systemOverride = null)
        {
            var system = string.IsNullOrWhiteSpace(systemOverride) ? CharacterSheetPrompt.System : systemOverride.Trim();
            return new ChatRequest
            {
                SystemPrompt = system + "\n" + IntimateBoundaries.PromptLine(intimateActive),
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, CharacterSheetPrompt.UserMessage(personaName, personaPrompt)),
                },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                BlockCloud = keepsLocal,
            };
        }

        public static async Task<PisaraReadResult> ReadAsync(
            IChatProvider provider,
            string personaName,
            string personaPrompt,
            bool keepsLocal,
            bool intimateActive,
            Func<string, SheetTemperament> keywords,
            CancellationToken cancellation,
            float timeoutSeconds = DefaultTimeoutSeconds,
            string systemOverride = null)
        {
            if (provider == null || !provider.IsConfigured)
                return Fail(personaPrompt, keywords, NoModel, false, string.Empty);
            if (!MayUse(provider, keepsLocal))
                return Fail(personaPrompt, keywords, CloudRefused, false, string.Empty);

            var request = BuildRequest(personaName, personaPrompt, keepsLocal, intimateActive, systemOverride);
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
                        Forget(call);
                        return Fail(personaPrompt, keywords, cancellation.IsCancellationRequested ? "cancelled" : TimedOut, true, string.Empty);
                    }

                    reply = await call;
                    linked.Cancel();
                }
                catch (OperationCanceledException)
                {
                    return Fail(personaPrompt, keywords, "cancelled", true, string.Empty);
                }
                catch (Exception e)
                {
                    return Fail(personaPrompt, keywords, "the call failed (" + e.GetType().Name + ")", true, string.Empty);
                }
            }

            if (!reply.Ok)
                return Fail(personaPrompt, keywords, "the model did not answer (" + reply.Error + ")", true, string.Empty);

            var sheet = CharacterSheet.Parse(reply.Text);
            if (sheet == null || IsEmpty(sheet))
                return Fail(personaPrompt, keywords, BadJson, true, reply.Text);
            PisaraReading.ApplyKinship(sheet);
            PisaraReading.ApplySharedHistory(sheet);

            return new PisaraReadResult
            {
                Sheet = PisaraReading.Finish(sheet, personaPrompt, CharacterSheet.SourceModel, DateTime.UtcNow),
                Called = true,
                RawReply = reply.Text,
            };
        }

        /// <summary>
        /// An object that parsed but says nothing (for example "{}") is not a reading: without
        /// this it would be cached as the model's answer and never asked again.
        /// </summary>
        public static bool IsEmpty(CharacterSheet sheet)
        {
            if (sheet == null)
                return true;
            var t = sheet.temperament;
            var anyWeight = t != null && (t.calm > 0f || t.cheerful > 0f || t.shy > 0f || t.tsundere > 0f || t.moody > 0f);
            var w = sheet.world;
            var anyWorld = w != null && ((w.backstory != null && w.backstory.Count > 0)
                || (w.lastWeek != null && w.lastWeek.Count > 0)
                || (w.openThreads != null && w.openThreads.Count > 0)
                || !string.IsNullOrWhiteSpace(w.rightNow));
            var r = sheet.relationship;
            var anyRelationship = r != null && (r.affection != 0f || r.familiarity != 0f
                || !string.IsNullOrWhiteSpace(r.label) || r.role != RelationshipRoles.Stranger);
            return !anyWeight && !anyWorld && !anyRelationship;
        }

        static PisaraReadResult Fail(string personaPrompt, Func<string, SheetTemperament> keywords, string why, bool called, string raw)
        {
            SheetTemperament guess = null;
            try
            {
                guess = keywords == null ? null : keywords(personaPrompt);
            }
            catch (Exception)
            {
                guess = null;
            }

            return new PisaraReadResult
            {
                Sheet = PisaraReading.Fallback(personaPrompt, guess, DateTime.UtcNow),
                Called = called,
                Failure = why ?? string.Empty,
                RawReply = raw ?? string.Empty,
            };
        }

        /// <summary>An abandoned call may still fault later; observe it so it is not an unobserved exception.</summary>
        static void Forget(Task task)
        {
            if (task == null)
                return;
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
