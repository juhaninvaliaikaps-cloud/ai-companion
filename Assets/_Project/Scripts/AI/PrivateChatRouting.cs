namespace AICompanion.AI
{
    /// <summary>
    /// Which chat provider a turn goes to. Kept in the AI assembly so the chat UI can use
    /// the same rule without referencing the voice assembly.
    /// </summary>
    public static class PrivateChatRouting
    {
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
