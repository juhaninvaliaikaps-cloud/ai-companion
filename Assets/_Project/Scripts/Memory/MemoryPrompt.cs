using System.Collections.Generic;
using System.Text;

namespace AICompanion.Memory
{
    /// <summary>
    /// Renders retrieved memories and the stored personality as prompt text. Kept
    /// separate from the store so the wording is testable without a file.
    /// </summary>
    public static class MemoryPrompt
    {
        public const string Header = "What you remember from earlier sessions:";
        public const string PersonaHeader = "Your continuing persona:";

        public static string Build(IReadOnlyList<MemoryEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(Header);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;

                builder.Append("\n- ").Append(entry.text.Trim());
            }

            return builder.Length <= Header.Length ? string.Empty : builder.ToString();
        }

        public static string BuildPersona(PersonalityState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.personaName))
                return string.Empty;

            var builder = new StringBuilder(PersonaHeader);
            builder.Append("\n- Name: ").Append(state.personaName.Trim());
            if (!string.IsNullOrWhiteSpace(state.mood))
                builder.Append("\n- Mood: ").Append(state.mood.Trim());
            return builder.ToString();
        }
    }
}
