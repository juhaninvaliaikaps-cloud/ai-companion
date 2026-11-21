using System;
using System.Text;

namespace AICompanion.Settings
{
    /// <summary>Which model may describe her picture.</summary>
    public enum PictureDescribeRoute
    {
        /// <summary>No model can do it now; <see cref="PictureDescribeGate.Choice.Reason"/> says why.</summary>
        None = 0,

        /// <summary>The cloud AI API, whose model reads pictures.</summary>
        Cloud = 1,

        /// <summary>The local vision model (Ollama on this PC).</summary>
        Local = 2,
    }

    /// <summary>
    /// JUH-154: "Describe her from the picture". The vision model writes a short appearance
    /// paragraph; the user decides whether it goes into her description.
    /// <para>
    /// Privacy: in Private mode or with the 18+ gate open
    /// (<see cref="AICompanion.Core.IntimateExpressions.KeepsLocal"/>) a cloud model is never
    /// used, only the local vision model; without one, nothing is sent.
    /// </para>
    /// </summary>
    public static class PictureDescribeGate
    {
        public const string Prompt =
            "Describe the person in this picture for a character description, in 2 to 4 short sentences, in the third person, starting with \"She\". " +
            "Cover what is visible: hair, eyes, face, build, clothes and style. Do not guess her name, her age in years or anything you can't see. " +
            "Keep it plain and non-explicit. Reply with the sentences only.";

        public const string PrivateNoLocal = "In Private mode or with 18+ open, only the local vision model may look at her picture, and none is set. Set one on the Screen tab (for example qwen2.5vl:3b). Nothing was sent.";
        public const string NoModel = "No model here can read pictures: the cloud model doesn't take images and no local vision model is set. Nothing was sent.";
        public const string Heading = "Appearance";

        public struct Choice
        {
            public PictureDescribeRoute Route;
            public string Reason;
        }

        /// <summary>
        /// The route: never the cloud while <paramref name="keepsLocal"/>; otherwise the cloud when
        /// its model reads pictures, else the local vision model when one is set.
        /// </summary>
        public static Choice Choose(bool keepsLocal, bool cloudSeesImages, bool localVisionSet)
        {
            if (keepsLocal)
            {
                return localVisionSet
                    ? new Choice { Route = PictureDescribeRoute.Local, Reason = string.Empty }
                    : new Choice { Route = PictureDescribeRoute.None, Reason = PrivateNoLocal };
            }

            if (cloudSeesImages)
                return new Choice { Route = PictureDescribeRoute.Cloud, Reason = string.Empty };
            if (localVisionSet)
                return new Choice { Route = PictureDescribeRoute.Local, Reason = string.Empty };
            return new Choice { Route = PictureDescribeRoute.None, Reason = NoModel };
        }

        /// <summary>The model's reply, cleaned: no cue tags, quotes or code fences; at most 1,000 characters.</summary>
        public static string Clean(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return string.Empty;
            var text = reply.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            text = text.Replace("```", string.Empty);
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

            text = sb.ToString().Trim().Trim('"').Trim();
            while (text.Contains("\n\n\n"))
                text = text.Replace("\n\n\n", "\n\n");
            return text.Length > 1000 ? text.Substring(0, 1000).TrimEnd() : text;
        }

        /// <summary>Her description with the accepted appearance paragraph added at the end.</summary>
        public static string Append(string description, string appearance)
        {
            var add = Clean(appearance);
            var text = (description ?? string.Empty).TrimEnd();
            if (add.Length == 0)
                return text;
            var paragraph = Heading + ":\n" + add;
            var combined = text.Length == 0 ? paragraph : text + "\n\n" + paragraph;
            return combined.Length > PersonaDefinition.MaxDescriptionLength
                ? combined.Substring(0, PersonaDefinition.MaxDescriptionLength)
                : combined;
        }
    }
}
