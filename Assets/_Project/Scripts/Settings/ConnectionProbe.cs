using System;
using AICompanion.AI;
using AICompanion.Voice;

namespace AICompanion.Settings
{
    public sealed class ConnectionProbePlan
    {
        public string Url = string.Empty;
        public bool SendAuthorization;
        public bool Allowed;
        public string Summary = string.Empty;
    }

    /// <summary>
    /// Builds a models request that never carries the API key in a log line.
    /// The header is attached by the caller and is not part of this plan.
    /// </summary>
    public static class ConnectionProbe
    {
        public static ConnectionProbePlan Plan(bool privateMode, string providerId, string grokBaseUrl, string localBaseUrl)
        {
            var plan = new ConnectionProbePlan();
            var local = !OutboundPrivacy.AllowsCloudChat(privateMode)
                || string.Equals(OutboundPrivacy.ChatProviderId(privateMode, providerId), ChatDefaults.LocalId, StringComparison.Ordinal);
            var safe = SafeBase(local ? localBaseUrl : grokBaseUrl);
            plan.SendAuthorization = !local && OutboundPrivacy.AllowsCloudChat(privateMode);
            if (safe == null)
            {
                plan.Allowed = false;
                plan.Summary = "Connection test was not sent. The address looks unsafe.";
                return plan;
            }

            plan.Allowed = true;
            plan.Url = safe.TrimEnd('/') + "/models";
            plan.Summary = local
                ? "Testing the local model. No API key is sent."
                : "Testing Grok. The API key stays in the request header and is not logged.";
            return plan;
        }

        public static string FormatResult(int status, string error, string body)
        {
            var raw = status == 0 || string.IsNullOrEmpty(body) ? error : body;
            if (string.IsNullOrEmpty(raw))
                raw = body ?? string.Empty;
            var detail = ChatErrors.Scrub(raw).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (detail.Length > 180)
                detail = detail.Substring(0, 180);
            if (status >= 200 && status < 300)
                return "Connection test succeeded (HTTP " + status.ToString() + ").";
            if (status == 0 && detail.Length == 0)
                return "Connection test failed before a response.";
            if (detail.Length == 0)
                return "Connection test HTTP " + status.ToString() + ".";
            return "Connection test HTTP " + status.ToString() + ": " + detail;
        }

        public static string SafeBase(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return null;

            var trimmed = baseUrl.Trim();
            if (trimmed.IndexOf("xai-", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;
            if (trimmed.IndexOf('@') >= 0 || trimmed.IndexOf('?') >= 0 || trimmed.IndexOf('#') >= 0 || trimmed.IndexOf(' ') >= 0)
                return null;

            var lower = trimmed.ToLowerInvariant();
            if (!lower.StartsWith("https://") && !lower.StartsWith("http://"))
                return null;
            if (lower.IndexOf("api_key", StringComparison.Ordinal) >= 0 || lower.IndexOf("apikey", StringComparison.Ordinal) >= 0)
                return null;
            return trimmed;
        }
    }
}
