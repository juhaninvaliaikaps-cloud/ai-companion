namespace AICompanion.AI
{
    /// <summary>
    /// Which chat provider a turn goes to. Kept in the AI assembly so the chat UI can use
    /// the same rule without referencing the voice assembly.
    /// </summary>
    public static class PrivateChatRouting
    {
        /// <summary>
        /// Screen observations are always local unless Private mode is on with the Custom server.
        /// With Private mode off they must not follow the cloud chat provider to Grok.
        /// </summary>
        public static string ObservationProviderId(bool privateMode, string privateEndpointType)
        {
            return privateMode && PrivateEndpointTypes.IsCustom(privateEndpointType)
                ? ChatDefaults.CustomId
                : ChatDefaults.LocalId;
        }

        public static string ProviderId(bool privateMode, string selected, string privateEndpointType)
        {
            if (privateMode)
            {
                return PrivateEndpointTypes.IsCustom(privateEndpointType)
                    ? ChatDefaults.CustomId
                    : ChatDefaults.LocalId;
            }

            if (string.IsNullOrWhiteSpace(selected))
                return ChatDefaults.GrokId;
            var id = selected.Trim();
            if (string.Equals(id, ChatDefaults.LocalId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, ChatDefaults.GrokId, System.StringComparison.OrdinalIgnoreCase))
                return id;
            return ChatDefaults.GrokId;
        }
    }
}
