using System;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>What the check said about one claimed secret (JUH-180).</summary>
    public enum SecretVerdict
    {
        /// <summary>No clear yes or no: the claim is dropped.</summary>
        Unclear = 0,
        Yes = 1,
        No = 2,
    }

    /// <summary>The secret a reply claims to have found for the first time: what to ask about it.</summary>
    public struct SecretClaim
    {
        public string Id;
        public string Text;

        /// <summary>An 18+ secret (k, s, y): about the scene, so the check also sees her reply.</summary>
        public bool Intimate;

        /// <summary>For an everyday secret: true for "what wins her over", false for "what stings her".</summary>
        public bool Likes;
    }

    /// <summary>
    /// Looks up a claimed secret id. True only for a secret of hers that is not found yet
    /// (a found one lands like any message and needs no check).
    /// </summary>
    public delegate bool SecretClaimLookup(string id, out SecretClaim claim);

    /// <summary>
    /// JUH-180: the reply model writes <c>secret="t4"</c> itself, and a small local model in the
    /// middle of a scene names secrets the user never touched (Jussi: "she thought I called her
    /// meathead"). Before a first find counts, one short request asks the same chat model a
    /// yes/no question about only the user's message (and, for an 18+ secret, her reply). A no,
    /// an unclear answer or a failed request drops the claim; the secret stays to be found.
    /// Pure, for chat and tests.
    /// </summary>
    public static class SecretCheck
    {
        public const float Temperature = 0f;
        public const int MaxTokens = 8;
        public const float TimeoutSeconds = 30f;

        /// <summary>The longest user message or reply put in the question, in characters.</summary>
        public const int MaxQuoted = 1500;

        public const string SystemPrompt =
            "You check one claim about a chat message. Answer with one word: yes or no. Say yes only when it clearly happened.";

        /// <summary>The question for one claim. Her reply is used only for an 18+ secret.</summary>
        public static string Question(SecretClaim claim, string userText, string replyText)
        {
            var sb = new StringBuilder(400);
            var thing = HerSecrets.Clean(claim.Text);
            if (claim.Intimate)
            {
                sb.Append("In an intimate scene, she has this secret: \"").Append(thing).Append("\".\n");
                sb.Append("The user's message: \"").Append(Quote(userText)).Append("\"\n");
                sb.Append("Her reply: \"").Append(Quote(StripTags(replyText))).Append("\"\n");
                sb.Append("Did this message and reply clearly do that? Answer yes or no.");
                return sb.ToString();
            }

            sb.Append(claim.Likes ? "She warms to: \"" : "She is touchy about: \"").Append(thing).Append("\".\n");
            sb.Append("The user's message: \"").Append(Quote(userText)).Append("\"\n");
            sb.Append("Did the user's message itself clearly do that? Only what the user wrote counts, ");
            sb.Append("not anything she says and not the story so far. Answer yes or no.");
            return sb.ToString();
        }

        /// <summary>The answer's first word: yes or no, in English or Finnish. Anything else is unclear.</summary>
        public static SecretVerdict Parse(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return SecretVerdict.Unclear;
            var text = StripTags(reply).Trim().TrimStart('*', '"', '\'', '(', '[', ' ').ToLowerInvariant();
            var end = 0;
            while (end < text.Length && char.IsLetter(text[end]))
                end++;
            var word = text.Substring(0, end);
            switch (word)
            {
                case "yes":
                case "kyllä":
                case "joo":
                    return SecretVerdict.Yes;
                case "no":
                case "ei":
                    return SecretVerdict.No;
                default:
                    return SecretVerdict.Unclear;
            }
        }

        /// <summary>The cue with no secret: how a dropped claim lands (like an ordinary message).</summary>
        public static AnimationCue WithoutSecret(AnimationCue cue)
        {
            return new AnimationCue(cue.Emotion, cue.Intensity, cue.Gesture, cue.Direction,
                cue.EmotionField, cue.Reaction, cue.Landed, null, cue.Heat);
        }

        /// <summary>Trimmed and cut to <see cref="MaxQuoted"/>, with double quotes made single so the question stays readable.</summary>
        static string Quote(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var t = text.Trim().Replace('"', '\'');
            return t.Length <= MaxQuoted ? t : t.Substring(0, MaxQuoted) + "...";
        }

        /// <summary>Removes &lt;...&gt; tags, such as the cue tag, from a reply.</summary>
        public static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
                return text ?? string.Empty;
            var sb = new StringBuilder(text.Length);
            var inTag = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (inTag)
                {
                    if (c == '>')
                        inTag = false;
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// JUH-180: where chat finds a claimed secret. The chat assembly cannot see the settings
    /// assembly, so the relationship keeper registers <see cref="Lookup"/> here. Main thread only.
    /// </summary>
    public static class SecretClaimNow
    {
        public static SecretClaimLookup Lookup;

        /// <summary>True when the cue names a secret of hers that is not found yet, so it needs a check.</summary>
        public static bool NeedsCheck(AnimationCue cue, out SecretClaim claim)
        {
            claim = default(SecretClaim);
            if (string.IsNullOrEmpty(cue.Secret))
                return false;
            var lookup = Lookup;
            if (lookup == null)
                return false;
            try
            {
                return lookup(cue.Secret, out claim);
            }
            catch (Exception)
            {
                claim = default(SecretClaim);
                return false;
            }
        }

        public static void Reset()
        {
            Lookup = null;
        }
    }
}
