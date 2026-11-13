using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// One prompt line with where she stands with the user (JUH-117), so her wording matches
    /// the numbers. It goes after the mood line. Negative affection is described as bickering
    /// and love-hate; the consent rule in <see cref="IntimateBoundaries"/> still applies and
    /// this line never mentions Intimate.
    /// </summary>
    public static class RelationshipPrompt
    {
        public static string AffectionPhrase(float affection)
        {
            affection = RelationshipMath.ClampAffection(affection);
            if (affection <= -60f)
                return "you can't stand them right now";
            if (affection <= -20f)
                return "you are at odds with them";
            if (affection < 20f)
                return "you have no strong feelings about them yet";
            if (affection < 60f)
                return "you like them";
            return "you adore them";
        }

        public static string FamiliarityPhrase(float familiarity)
        {
            familiarity = RelationshipMath.ClampFamiliarity(familiarity);
            if (familiarity < 10f)
                return "you have only just met";
            if (familiarity < 35f)
                return "you know each other a little";
            if (familiarity < 70f)
                return "you know each other well";
            return "you know each other very well";
        }

        /// <summary>
        /// For example: <c>Your relationship with the user: stepsister (family); you like them; you
        /// know each other very well. Touchy about: being called childish. Warms to: being trusted.</c>
        /// </summary>
        public static string Line(SheetRelationship sheet, SheetLanding landing, float affection, float familiarity)
        {
            var sb = new StringBuilder(200);
            sb.Append("Your relationship with the user: ");
            var role = sheet == null ? RelationshipRoles.Stranger : RelationshipRoles.Normalize(sheet.role);
            var label = sheet == null || string.IsNullOrWhiteSpace(sheet.label) ? string.Empty : sheet.label.Trim();
            var roleWord = role.Replace('_', ' ');
            if (label.Length > 0 && !string.Equals(label, roleWord, System.StringComparison.OrdinalIgnoreCase))
                sb.Append(label).Append(" (").Append(roleWord).Append(')');
            else
                sb.Append(roleWord);
            sb.Append("; ").Append(AffectionPhrase(affection));
            sb.Append("; ").Append(FamiliarityPhrase(familiarity)).Append('.');

            if (RelationshipMath.ClampAffection(affection) <= -20f)
                sb.Append(" Bicker and push back in your own way, but this is love-hate between two people who choose to be here, never fear.");
            else if (RelationshipMath.ClampFamiliarity(familiarity) < 10f)
                sb.Append(" Be a little guarded; trust is earned.");

            AppendList(sb, " Touchy about: ", landing == null ? null : landing.touchy);
            AppendList(sb, " Warms to: ", landing == null ? null : landing.winsHerOver);
            return sb.ToString();
        }

        /// <summary>The prompt text with the relationship line after it.</summary>
        public static string Append(string prompt, SheetRelationship sheet, SheetLanding landing, float affection, float familiarity)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            var line = Line(sheet, landing, affection, familiarity);
            return body.Length == 0 ? line : body + "\n" + line;
        }

        static void AppendList(StringBuilder sb, string lead, List<string> items)
        {
            if (items == null || items.Count == 0)
                return;
            var any = false;
            for (var i = 0; i < items.Count && i < 3; i++)
            {
                if (string.IsNullOrWhiteSpace(items[i]))
                    continue;
                sb.Append(any ? ", " : lead).Append(items[i].Trim().TrimEnd('.'));
                any = true;
            }

            if (any)
                sb.Append('.');
        }
    }
}
