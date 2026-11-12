using System;

namespace AICompanion.AI
{
    public static class PrivateEndpointTypes
    {
        public const string Local = "local";
        public const string Custom = "custom";

        public static bool IsCustom(string value)
        {
            return string.Equals(value, Custom, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string value)
        {
            return IsCustom(value) ? Custom : Local;
        }
    }

    /// <summary>
    /// Rules for a user-provided OpenAI-compatible endpoint in Private mode. The commercial
    /// host list is the single place to extend when a new provider must be refused.
    /// </summary>
    public static class PrivateEndpointRules
    {
        public static readonly string[] CommercialHosts =
        {
            "api.x.ai",
            "api.openai.com",
            "api.anthropic.com",
            "generativelanguage.googleapis.com",
            "api.deepseek.com",
            "openrouter.ai",
            "api.groq.com",
            "api.mistral.ai",
            "api.together.xyz",
        };

        public const string MissingAddress =
            "No custom server address is set. Add it in Settings > AI.";
        public const string MissingModel =
            "No custom model name is set. Add it in Settings > AI.";

        /// <summary>Null when the address can be used as a Private endpoint.</summary>
        public static string ValidateAddress(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return MissingAddress;
            Uri uri;
            if (!TryParse(baseUrl, out uri))
                return "The custom server address must be a full http:// or https:// URL.";
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return "The custom server address must not contain a username or password.";
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return "Put the API key in the key field, not in the server address.";
            if (IsCommercialHost(uri))
                return "That address is a commercial cloud provider. Private mode needs a local or self-hosted endpoint.";
            return null;
        }

        /// <summary>Null when the address and model can be used for chat.</summary>
        public static string ValidateForChat(string baseUrl, string model)
        {
            var address = ValidateAddress(baseUrl);
            if (address != null)
                return address;
            if (string.IsNullOrWhiteSpace(model))
                return MissingModel;
            return null;
        }

        public static bool IsCommercialHost(string baseUrl)
        {
            Uri uri;
            return TryParse(baseUrl, out uri) && IsCommercialHost(uri);
        }

        public static bool IsCommercialHost(Uri uri)
        {
            var host = Host(uri);
            for (var i = 0; i < CommercialHosts.Length; i++)
            {
                var blocked = CommercialHosts[i];
                if (host.Equals(blocked, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + blocked, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>True for http to anything except localhost, 127.0.0.1 and ::1.</summary>
        public static bool IsPlainHttpNonLocal(string baseUrl)
        {
            Uri uri;
            if (!TryParse(baseUrl, out uri))
                return false;
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
                return false;
            return !IsLoopbackHost(uri);
        }

        public static bool IsLoopbackHost(Uri uri)
        {
            var host = Host(uri);
            return host == "localhost" || host == "127.0.0.1" || host == "::1" || host == "[::1]";
        }

        public static string HttpWarning(string baseUrl)
        {
            return IsPlainHttpNonLocal(baseUrl)
                ? "This address uses plain HTTP, so the API key and the chat can be read on the network. Use HTTPS or a trusted local network."
                : string.Empty;
        }

        public static string ModelsUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return string.Empty;
            var trimmed = baseUrl.Trim().TrimEnd('/');
            if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "/chat/completions".Length);
            if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            return trimmed + "/models";
        }

        static bool TryParse(string baseUrl, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return false;
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var parsed))
                return false;
            if (!string.Equals(parsed.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrEmpty(parsed.Host))
                return false;
            uri = parsed;
            return true;
        }

        static string Host(Uri uri)
        {
            return uri == null ? string.Empty : uri.Host.TrimEnd('.').ToLowerInvariant();
        }
    }
}
