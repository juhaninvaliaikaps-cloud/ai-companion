using System;
using System.Collections.Generic;

namespace AICompanion.Settings
{
    /// <summary>What the "⋯" menu on a persona card offers.</summary>
    [Flags]
    public enum PersonaCardActions
    {
        None = 0,
        OpenInStudio = 1,
        Duplicate = 2,
        Delete = 4,
    }

    /// <summary>One card in the persona grid.</summary>
    public sealed class PersonaCard
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Tagline = string.Empty;
        public bool Active;
        public bool HasPicture;
        public bool Round = true;
        public bool BuiltIn;
        public bool SavedCopy;
        public PersonaCardActions Actions;

        /// <summary>"Delete" for her own file, "Delete saved copy" for an edited built-in.</summary>
        public string DeleteLabel
        {
            get { return SavedCopy ? "Delete saved copy" : "Delete"; }
        }
    }

    /// <summary>
    /// JUH-154: the Persona tab's grid of cards (picture, name, tagline; the active one marked).
    /// Plain C#: the cards, their menu, and how they fill rows.
    /// </summary>
    public static class PersonaGrid
    {
        public const float MinCardWidth = 150f;
        public const int MaxColumns = 4;

        public static List<PersonaCard> Cards(IReadOnlyList<PersonaEntry> entries, string activeId)
        {
            var cards = new List<PersonaCard>();
            if (entries == null)
                return cards;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Persona == null || string.IsNullOrEmpty(entry.Persona.Id))
                    continue;
                var persona = entry.Persona;
                var actions = PersonaCardActions.OpenInStudio | PersonaCardActions.Duplicate;
                // A built-in file can't be deleted; a saved copy of one can (the built-in comes back).
                if (entry.UserFile)
                    actions |= PersonaCardActions.Delete;
                cards.Add(new PersonaCard
                {
                    Id = persona.Id,
                    Name = string.IsNullOrWhiteSpace(persona.DisplayName) ? persona.Id : persona.DisplayName.Trim(),
                    Tagline = (persona.Tagline ?? string.Empty).Trim(),
                    Active = string.Equals(persona.Id, activeId ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                    HasPicture = persona.HasPicture,
                    Round = PersonaPictureShapes.Normalize(persona.PictureShape) == PersonaPictureShapes.Round,
                    BuiltIn = entry.BuiltIn,
                    SavedCopy = entry.BuiltIn && entry.UserFile,
                    Actions = actions,
                });
            }

            return cards;
        }

        /// <summary>How many cards fit in a row of this width (at least 1, at most <see cref="MaxColumns"/>).</summary>
        public static int Columns(float width, float scale)
        {
            var min = MinCardWidth * Math.Max(0.5f, scale);
            if (float.IsNaN(width) || width <= min)
                return 1;
            return Math.Max(1, Math.Min(MaxColumns, (int)(width / min)));
        }

        /// <summary>How many rows <paramref name="count"/> cards take.</summary>
        public static int Rows(int count, int columns)
        {
            if (count <= 0)
                return 0;
            columns = Math.Max(1, columns);
            return (count + columns - 1) / columns;
        }

        /// <summary>The menu items for a card, in order, as labels.</summary>
        public static List<string> MenuLabels(PersonaCard card)
        {
            var labels = new List<string>();
            if (card == null)
                return labels;
            if ((card.Actions & PersonaCardActions.OpenInStudio) != 0)
                labels.Add("Open in Studio");
            if ((card.Actions & PersonaCardActions.Duplicate) != 0)
                labels.Add("Duplicate");
            if ((card.Actions & PersonaCardActions.Delete) != 0)
                labels.Add(card.DeleteLabel);
            return labels;
        }

        /// <summary>The name of a duplicate: "Mira copy", "Mira copy 2"… whichever is free.</summary>
        public static string CopyName(string name, Func<string, bool> taken)
        {
            var baseName = (string.IsNullOrWhiteSpace(name) ? "Persona" : name.Trim()) + " copy";
            if (taken == null || !taken(baseName))
                return baseName;
            for (var n = 2; n < 100; n++)
            {
                var candidate = baseName + " " + n;
                if (!taken(candidate))
                    return candidate;
            }

            return baseName;
        }
    }
}
