using System;
using AICompanion.AI;

namespace AICompanion.Settings
{
    public enum PisaraKeySource
    {
        None = 0,
        Pisara = 1,
        Grok = 2,
    }

    /// <summary>
    /// JUH-126: decides which service reads her character sheet. Off, Private mode or no key
    /// keep the chat path. On and not private use the separate Pisara cloud service, always
    /// with a gate-closed request. The choice is pure so the tests do not need a scene.
    /// </summary>
    public readonly struct PisaraModelChoice
    {
        public const string CloudProviderId = "pisara";

        public readonly bool UseCloud;
        public readonly string ProviderId;
        public readonly string BaseUrl;
        public readonly string Model;
        public readonly bool GateClosed;
        public readonly PisaraKeySource KeySource;
        public readonly string Reason;

        public PisaraModelChoice(
            bool useCloud,
            string providerId,
            string baseUrl,
            string model,
            bool gateClosed,
            PisaraKeySource keySource,
            string reason)
        {
            UseCloud = useCloud;
            ProviderId = providerId ?? string.Empty;
            BaseUrl = baseUrl ?? string.Empty;
            Model = model ?? string.Empty;
            GateClosed = gateClosed;
            KeySource = keySource;
            Reason = reason ?? string.Empty;
        }

        public static PisaraModelChoice For(
            CompanionSettings settings,
            bool privateMode,
            bool gateOpen,
            bool ownKeyReady,
            bool grokKeyReady)
        {
            settings = settings ?? CompanionSettings.CreateDefault();
            if (!settings.PisaraCloud)
                return ChatPath(settings, privateMode, gateOpen, "Pisara cloud is off");

            var baseConfigured = !string.IsNullOrWhiteSpace(settings.PisaraCloudBaseUrl);
            var baseUrl = First(settings.PisaraCloudBaseUrl, settings.BaseUrl);
            var sameHost = !baseConfigured || SameHost(baseUrl, settings.BaseUrl);

            var configuredModel = settings.PisaraCloudModel;
            if (string.IsNullOrWhiteSpace(configuredModel) && !sameHost)
                return ChatPath(settings, privateMode, gateOpen, "Set a Pisara model for this service");
            var model = First(configuredModel, settings.Model);

            PisaraKeySource keySource;
            if (ownKeyReady)
                keySource = PisaraKeySource.Pisara;
            else if (sameHost && grokKeyReady)
                keySource = PisaraKeySource.Grok;
            else if (sameHost)
                return ChatPath(settings, privateMode, gateOpen, "No Pisara API key or cloud AI API key is set");
            else
                return ChatPath(settings, privateMode, gateOpen, "Set a Pisara API key for this service");

            // JUH-126: private mode does not keep this one reading local. The gate only opens
            // while private mode is on, so the cloud must still be reachable here. The request
            // is always built gate-closed, and every other private-mode path stays local.
            return new PisaraModelChoice(
                true,
                CloudProviderId,
                baseUrl,
                model,
                true,
                keySource,
                "Pisara reading uses the cloud service");
        }

        static bool SameHost(string first, string second)
        {
            Uri a;
            Uri b;
            if (!Uri.TryCreate(first, UriKind.Absolute, out a) || !Uri.TryCreate(second, UriKind.Absolute, out b))
                return false;
            return string.Equals(a.Host, b.Host, System.StringComparison.OrdinalIgnoreCase)
                && a.Port == b.Port;
        }

        /// <summary>
        /// JUH-126: true when the reading went to a cloud model. The Pisara cloud always counts;
        /// the chat path counts only when the read did not have to keep local.
        /// </summary>
        public static bool ReadInCloud(PisaraModelChoice choice, bool keepsLocal)
        {
            return choice.UseCloud || !keepsLocal;
        }

        static PisaraModelChoice ChatPath(CompanionSettings settings, bool privateMode, bool gateOpen, string reason)
        {
            var keepsLocal = privateMode || gateOpen;
            var id = PrivateChatRouting.ProviderId(keepsLocal, settings.ProviderId, settings.PrivateEndpointType);
            var baseUrl = string.Equals(id, ChatDefaults.LocalId, System.StringComparison.OrdinalIgnoreCase)
                ? settings.LocalBaseUrl
                : string.Equals(id, ChatDefaults.CustomId, System.StringComparison.OrdinalIgnoreCase)
                    ? settings.PrivateBaseUrl
                    : settings.BaseUrl;
            var model = string.Equals(id, ChatDefaults.LocalId, System.StringComparison.OrdinalIgnoreCase)
                ? settings.LocalModel
                : string.Equals(id, ChatDefaults.CustomId, System.StringComparison.OrdinalIgnoreCase)
                    ? settings.PrivateModel
                    : settings.Model;
            return new PisaraModelChoice(false, id, baseUrl, model, false, PisaraKeySource.None, reason);
        }

        static string First(string configured, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim();
            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
        }
    }
}
