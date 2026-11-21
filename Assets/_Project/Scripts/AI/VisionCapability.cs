using System;

namespace AICompanion.AI
{
    /// <summary>
    /// Which cloud models may receive a window image (JUH-149: any API whose model reads
    /// pictures, not only xAI). <see cref="Mode"/> is the user's "Cloud model can see pictures"
    /// setting: Auto uses the known list below, Yes trusts any model, No sends no picture.
    /// A name that merely contains "vision" is not enough for Auto. Local text models,
    /// including Rocinante, are not treated as vision models, and nothing here switches the provider.
    /// </summary>
    public static class VisionCapability
    {
        public const int MaxImageBytes = 400 * 1024;
        public const int MaxOutputTokens = 800;
        public const float MaxTimeoutSeconds = 45f;
        public const int MaxResponseChars = 1500000;

        public const string ModeAuto = "auto";
        public const string ModeOn = "on";
        public const string ModeOff = "off";

        public const string LocalRefusal = "The local model cannot look at a picture. Nothing was sent, and the app did not switch to a cloud model.";
        public const string PrivateRefusal = "Private mode is on. The window image stays off the cloud, and cloud speech stays off. The local model cannot look at a picture, so nothing was sent.";
        public const string ModelRefusal = "The selected cloud model isn't known to read pictures. Nothing was sent. If it can, set \"Cloud model can see pictures\" to Yes on the AI tab.";
        public const string PayloadRefusal = "The capture was empty or larger than the size limit. Nothing was sent.";

        static string _mode = ModeAuto;

        public static string Mode
        {
            get { return _mode; }
        }

        public static void Configure(string mode)
        {
            _mode = NormalizeMode(mode);
        }

        public static void Reset()
        {
            _mode = ModeAuto;
        }

        public static string NormalizeMode(string mode)
        {
            var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
            return m == ModeOn || m == ModeOff ? m : ModeAuto;
        }

        /// <summary>The user's setting decides; Auto asks <see cref="KnownVisionModel"/>.</summary>
        public static bool CloudModelAcceptsImages(string model)
        {
            if (string.IsNullOrWhiteSpace(model) || _mode == ModeOff)
                return false;
            return _mode == ModeOn || KnownVisionModel(model);
        }

        static readonly string[] ExactModels =
        {
            // xAI: grok-4.7 on the Responses API, grok-4 the legacy image example.
            "grok-4.7", "grok-4",
            // DeepSeek V4.1 Flash reads pictures since 2026-09-10 (api-docs.deepseek.com/guides/vision).
            "deepseek-flash",
            // OpenAI reasoning models with image input (o3-mini has none).
            "o3", "o3-pro", "o4-mini",
        };

        static readonly string[] Prefixes =
        {
            "deepseek-v4-flash-vision", "deepseek-vl",
            "claude-3", "claude-opus", "claude-sonnet", "claude-haiku", "claude-fable", "claude-mythos",
            "gpt-4o", "chatgpt-4o", "gpt-4.1", "gpt-5",
            "gemini-", "pixtral", "llama-4", "grok-2-vision",
        };

        /// <summary>
        /// The models known to read pictures. A provider prefix such as <c>openai/</c>
        /// (OpenRouter) is ignored. Qwen counts only in its VL builds.
        /// </summary>
        public static bool KnownVisionModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;
            var name = model.Trim().ToLowerInvariant();
            var slash = name.LastIndexOf('/');
            if (slash >= 0 && slash < name.Length - 1)
                name = name.Substring(slash + 1);

            for (var i = 0; i < ExactModels.Length; i++)
            {
                if (name == ExactModels[i])
                    return true;
            }

            for (var i = 0; i < Prefixes.Length; i++)
            {
                if (name.StartsWith(Prefixes[i], StringComparison.Ordinal))
                    return true;
            }

            return name.StartsWith("qwen", StringComparison.Ordinal) && name.Contains("vl");
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
