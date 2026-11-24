using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// One prompt line with where she stands with the user (JUH-117), so her wording matches
    /// the numbers. It goes after the mood line. Negative affection is described as bickering
    /// and love-hate; the closed-gate rule in <see cref="IntimateBoundaries"/> still applies and
    /// this line never mentions Intimate.
    /// </summary>
    public static class RelationshipPrompt
    {
        public static string AffectionPhrase(float affection, bool loveUnlocked = true)
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
            if (affection < 80f)
                return "you adore them";
            if (affection < 95f)
                return loveUnlocked ? "you are in love with them" : "you adore them, but you are holding something back";
            return loveUnlocked ? "you are deeply in love with them" : "you adore them, but you are holding something back";
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
        /// know each other very well. Touchy about: (t1) being called childish. Warms to: (w1) being trusted.</c>
        /// The ids in brackets are her secrets (JUH-123): the model names the one a message hit
        /// with <c>secret="t1"</c>. All of them are listed, up to five per list.
        /// </summary>
        public static string Line(SheetRelationship sheet, SheetLanding landing, float affection, float familiarity,
            bool loveUnlocked = true)
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
            sb.Append("; ").Append(AffectionPhrase(affection, loveUnlocked));
            sb.Append("; ").Append(FamiliarityPhrase(familiarity)).Append('.');

            if (RelationshipMath.ClampAffection(affection) <= -20f)
                sb.Append(" Bicker and push back in your own way, but this is love-hate between two people who choose to be here, never fear.");
            else if (RelationshipMath.ClampFamiliarity(familiarity) < 10f)
                sb.Append(" Be a little guarded; trust is earned.");
            else if (RelationshipMath.ClampAffection(affection) >= 80f)
            {
                sb.Append(loveUnlocked
                    ? " You are in love with them. Choose them openly when it fits, initiate warmth, show them your vulnerable side, and forgive ordinary mistakes. Stay your own person: love is not possession, obsession or a reason to drop your boundaries."
                    : " You adore them, but you have not let yourself fully fall for them yet. Something about them is still undiscovered; stay warm, but keep that one piece of yourself held back.");
            }

            var secrets = HerSecrets.From(landing);
            HerSecrets.AppendPromptList(sb, " Touchy about: ", secrets, SecretGroup.Stings);
            HerSecrets.AppendPromptList(sb, " Warms to: ", secrets, SecretGroup.WinsHerOver);
            return sb.ToString();
        }

        /// <summary>The prompt text with the relationship line after it.</summary>
        public static string Append(string prompt, SheetRelationship sheet, SheetLanding landing, float affection, float familiarity)
        {
            var body = prompt == null ? string.Empty : prompt.TrimEnd();
            var line = Line(sheet, landing, affection, familiarity);
            return body.Length == 0 ? line : body + "\n" + line;
        }
    }
}
