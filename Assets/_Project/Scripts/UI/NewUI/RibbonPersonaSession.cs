using System;
using System.Collections.Generic;
using AICompanion.Settings;

namespace AICompanion.NewUI
{
    /// <summary>In-memory drafts and quick choices for the chat persona picker.</summary>
    public sealed class RibbonPersonaSession
    {
        public const int MaxChoices = 4;
        readonly Dictionary<string, string> _drafts = new(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _recent = new(MaxChoices);

        public string ActiveId { get; private set; }

        public RibbonPersonaSession(string activeId)
        {
            ActiveId = activeId ?? string.Empty;
        }

        public bool TryChange(string nextId, string currentDraft, out string restoredDraft)
        {
            nextId ??= string.Empty;
            restoredDraft = currentDraft;
            if (Same(ActiveId, nextId))
                return false;

            if (string.IsNullOrEmpty(currentDraft))
                _drafts.Remove(ActiveId);
            else
                _drafts[ActiveId] = currentDraft;

            var previous = _recent.FindIndex(id => Same(id, ActiveId));
            if (previous >= 0)
                _recent.RemoveAt(previous);
            _recent.Insert(0, ActiveId);
            if (_recent.Count > MaxChoices)
                _recent.RemoveAt(MaxChoices);

            ActiveId = nextId;
            if (!_drafts.TryGetValue(nextId, out restoredDraft))
                restoredDraft = string.Empty;
            return true;
        }

        public List<PersonaCard> Choices(IReadOnlyList<PersonaCard> cards, string setupName)
        {
            var choices = new List<PersonaCard>(MaxChoices);
            if (ActiveId.Length == 0)
                choices.Add(new PersonaCard { Name = setupName, Active = true });
            else
                Add(choices, Find(cards, ActiveId));
            foreach (var id in _recent)
                Add(choices, Find(cards, id));
            foreach (var card in cards)
                if (card.BuiltIn)
                    Add(choices, card);
            foreach (var card in cards)
                Add(choices, card);

            return choices;
        }

        static PersonaCard Find(IReadOnlyList<PersonaCard> cards, string id)
        {
            foreach (var card in cards)
                if (Same(card.Id, id))
                    return card;
            return null;
        }

        static void Add(List<PersonaCard> choices, PersonaCard card)
        {
            if (card == null || choices.Count >= MaxChoices)
                return;
            foreach (var entry in choices)
                if (Same(entry.Id, card.Id))
                    return;
            choices.Add(card);
        }

        static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
