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
            bool keyReady,
            bool usesOwnKey = false)
        {
            settings = settings ?? CompanionSettings.CreateDefault();
            if (!settings.PisaraCloud)
                return ChatPath(settings, privateMode, gateOpen, "Pisara cloud is off");

            if (privateMode)
                return ChatPath(settings, true, gateOpen, "Private mode keeps Pisara readings local");

            if (!keyReady)
                return ChatPath(settings, false, gateOpen, "No Pisara API key or Grok API key is set");

            var baseUrl = First(settings.PisaraCloudBaseUrl, settings.BaseUrl);
            var model = First(settings.PisaraCloudModel, settings.Model);
            return new PisaraModelChoice(
                true,
                CloudProviderId,
                baseUrl,
                model,
                true,
                usesOwnKey ? PisaraKeySource.Pisara : PisaraKeySource.Grok,
                "Pisara reading uses the cloud service");
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
