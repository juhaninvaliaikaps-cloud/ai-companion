using System;

namespace AICompanion.AI
{
    /// <summary>
    /// Which models may receive a window image. Checked against the xAI docs on 2026-11-10.
    /// A name that merely contains "vision" is not enough. Local text models, including
    /// Rocinante, are not treated as vision models, and nothing here switches the provider.
    /// </summary>
    public static class VisionCapability
    {
        public const int MaxImageBytes = 400 * 1024;
        public const int MaxOutputTokens = 800;
        public const float MaxTimeoutSeconds = 45f;
        public const int MaxResponseChars = 1500000;

        public const string LocalRefusal = "The local model cannot look at a picture. Nothing was sent, and the app did not switch to a cloud model.";
        public const string PrivateRefusal = "Private mode is on. The window image stays off the cloud, and cloud speech stays off. The local model cannot look at a picture, so nothing was sent.";
        public const string ModelRefusal = "The selected cloud model is not documented as a vision model. Nothing was sent.";
        public const string PayloadRefusal = "The capture was empty or larger than the size limit. Nothing was sent.";

        /// <summary>
        /// grok-4.7: text and image input on the Responses API
        /// (docs.x.ai/developers/grok-4-7 and the image understanding guide).
        /// grok-4: the legacy chat-completions image example.
        /// </summary>
        public static bool CloudModelAcceptsImages(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;

            var name = model.Trim();
            return name.Equals("grok-4.7", StringComparison.OrdinalIgnoreCase)
                || name.Equals("grok-4", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// No local model is on the vision allowlist. Rocinante is a text model.
        /// </summary>
        public static bool LocalModelAcceptsImages(string model)
        {
            return false;
        }

        public static bool AcceptsPayload(ChatImageAttachment image)
        {
            if (image == null || image.JpegBytes == null)
                return false;
            var bytes = image.JpegBytes;
            if (bytes.Length < 4 || bytes.Length > MaxImageBytes)
                return false;
            return bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        }
    }
}
