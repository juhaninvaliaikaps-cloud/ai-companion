using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.Core;

namespace AICompanion.AI
{
    public sealed class PersonaCondenseResult
    {
        public bool Ok;
        public string Text = string.Empty;
        public string Failure = string.Empty;
    }

    /// <summary>
    /// JUH-151: a Pisara generator. It condenses a description too long for the chat model
    /// (more than the budget, 4,000 characters) into one that fits, written as instructions to
    /// her. It uses the same model and privacy rule as the character-sheet reading; a request
    /// that must stay local never goes to a cloud provider.
    /// </summary>
    public static class PersonaCondenser
    {
        public const float TimeoutSeconds = 180f;
        public const int MaxTokens = 1600;
        public const float Temperature = 0.3f;

        public const string NoModel = "No model is ready to condense her description.";
        public const string CloudRefused = "This must stay on this PC, and only a cloud model is set up. Nothing was sent.";
        public const string Empty = "The model gave nothing back.";

        public static string System(int budget)
        {
            return "You condense a character description for a small chat model with little memory. " +
                   "Keep who she is, how she talks and behaves, her looks, her relationship to the user, and the facts that matter. " +
                   "Drop repetition, lists of trivia and instructions about formatting. Write it as instructions to her, in the second person " +
                   "(\"You are ...\"), as plain paragraphs with no headings. At most " + budget + " characters.";
        }

        public static ChatRequest BuildRequest(string name, string description, int budget, bool keepsLocal, bool intimateActive)
        {
            return new ChatRequest
            {
                SystemPrompt = System(budget) + "\n" + IntimateBoundaries.PromptLine(intimateActive),
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "Her name: " + (name ?? string.Empty) + "\nHer description:\n" + (description ?? string.Empty)),
                },
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                TimeoutSeconds = TimeoutSeconds,
                BlockCloud = keepsLocal,
                NoMemory = true,
            };
        }

        /// <summary>The reply as her chat text: quotes and a leading label removed, cut to the budget at a sentence end.</summary>
        public static string Clean(string reply, int budget)
        {
            var text = (reply ?? string.Empty).Replace("\r\n", "\n").Trim().Trim('"').Trim();
            if (text.StartsWith("Condensed description:", StringComparison.OrdinalIgnoreCase))
                text = text.Substring("Condensed description:".Length).Trim();
            if (budget <= 0 || text.Length <= budget)
                return text;
            var cut = text.LastIndexOf(". ", budget - 1, StringComparison.Ordinal);
            return (cut > budget / 2 ? text.Substring(0, cut + 1) : text.Substring(0, budget)).TrimEnd();
        }

        public static async Task<PersonaCondenseResult> CondenseAsync(IChatProvider provider, string name, string description, int budget,
            bool keepsLocal, bool intimateActive, CancellationToken cancellation)
        {
            if (provider == null || !provider.IsConfigured)
                return new PersonaCondenseResult { Failure = NoModel };
            if (!PisaraReader.MayUse(provider, keepsLocal))
                return new PersonaCondenseResult { Failure = CloudRefused };

            ChatResult reply;
            try
            {
                reply = await provider.StreamChat(BuildRequest(name, description, budget, keepsLocal, intimateActive), _ => { }, cancellation);
            }
            catch (OperationCanceledException)
            {
                return new PersonaCondenseResult { Failure = "Cancelled." };
            }
            catch (Exception ex)
            {
                return new PersonaCondenseResult { Failure = "The call failed (" + ex.GetType().Name + ")." };
            }

            if (!reply.Ok)
                return new PersonaCondenseResult { Failure = string.IsNullOrEmpty(reply.UserMessage) ? "The call failed." : reply.UserMessage };
            var text = Clean(reply.Text, budget);
            return text.Length == 0
                ? new PersonaCondenseResult { Failure = Empty }
                : new PersonaCondenseResult { Ok = true, Text = text };
        }
    }
}
