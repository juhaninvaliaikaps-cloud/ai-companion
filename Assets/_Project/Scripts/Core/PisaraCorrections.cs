using System;

namespace AICompanion.Core
{
    /// <summary>
    /// The user's corrections to a reading (JUH-117). The character sheet is the model's guess;
    /// when the user corrects who she is to them, that correction is locked and survives a
    /// re-read and an edited persona prompt, until the user asks for the reading again.
    /// Temperament has its own manual pick (<see cref="TemperamentModes"/>), so this covers
    /// the relationship.
    /// </summary>
    public static class PisaraCorrections
    {
        public const string CorrectedNote = " (your correction)";
        public const float Step = 10f;

        /// <summary>Sets and locks the relationship part of the sheet. Returns the sheet.</summary>
        public static CharacterSheet SetRelationship(CharacterSheet sheet, string role, string label, float affection, float familiarity, bool sharedHistory)
        {
            if (sheet == null)
                return null;
            if (sheet.relationship == null)
                sheet.relationship = new SheetRelationship();
            sheet.relationship.role = RelationshipRoles.Normalize(role);
            sheet.relationship.label = label ?? string.Empty;
            sheet.relationship.affection = affection;
            sheet.relationship.familiarity = familiarity;
            sheet.relationship.sharedHistory = sharedHistory;
            sheet.relationship.reason = string.Empty;
            sheet.relationshipLocked = true;
            sheet.Normalize();
            // The same limits a reading gets: a stranger stays near 0, family knows her well.
            float start;
            float known;
            RelationshipMath.Start(sheet.relationship, out start, out known);
            sheet.relationship.affection = start;
            sheet.relationship.familiarity = known;
            return sheet;
        }

        /// <summary>The role after <paramref name="role"/> in <see cref="RelationshipRoles.All"/>, wrapping. For a "next" button.</summary>
        public static string NextRole(string role)
        {
            var current = RelationshipRoles.Normalize(role);
            for (var i = 0; i < RelationshipRoles.All.Length; i++)
            {
                if (RelationshipRoles.All[i] == current)
                    return RelationshipRoles.All[(i + 1) % RelationshipRoles.All.Length];
            }

            return RelationshipRoles.Stranger;
        }

        /// <summary>Drops the lock, so the next reading decides the relationship again.</summary>
        public static void Unlock(CharacterSheet sheet)
        {
            if (sheet != null)
                sheet.relationshipLocked = false;
        }

        /// <summary>
        /// Call on every sheet a reading produced, before saving it: when the sheet it replaces
        /// carries the user's correction, the correction is copied into the new one. Returns
        /// <paramref name="fresh"/>.
        /// </summary>
        public static CharacterSheet CarryOver(CharacterSheet previous, CharacterSheet fresh)
        {
            if (fresh == null || previous == null || !previous.relationshipLocked || previous.relationship == null)
                return fresh;
            if (ReferenceEquals(previous, fresh))
                return fresh;
            var kept = previous.relationship;
            fresh.relationship = new SheetRelationship
            {
                role = kept.role,
                label = kept.label,
                affection = kept.affection,
                familiarity = kept.familiarity,
                sharedHistory = kept.sharedHistory,
                reason = string.Empty,
            };
            fresh.relationshipLocked = true;
            fresh.Normalize();
            return fresh;
        }

        /// <summary>" (your correction)" when the relationship is the user's, else empty. For the settings line.</summary>
        public static string Note(CharacterSheet sheet)
        {
            return sheet != null && sheet.relationshipLocked ? CorrectedNote : string.Empty;
        }
    }
}
