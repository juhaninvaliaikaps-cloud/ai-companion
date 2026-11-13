namespace AICompanion.Core
{
    /// <summary>
    /// The request that asks a model for a <see cref="CharacterSheet"/> (JUH-117). This is the
    /// starting text; JUH-118 tunes the wording against the local model. The JSON shape is the
    /// contract and must keep matching <see cref="CharacterSheet"/>.
    /// The caller decides which model gets it: a private persona, Private mode or an open
    /// intimate gate means the local model only (<see cref="IntimateExpressions.KeepsLocal"/>).
    /// </summary>
    public static class CharacterSheetPrompt
    {
        public const string System =
            "You read a character description for a desktop companion and fill in a character sheet. " +
            "Reply with one JSON object and nothing else. Use exactly this shape:\n" +
            "{\n" +
            "  \"temperament\": { \"calm\": 0.0, \"cheerful\": 0.0, \"shy\": 0.0, \"tsundere\": 0.0, \"moody\": 0.0, \"reason\": \"\" },\n" +
            "  \"relationship\": { \"role\": \"stranger\", \"label\": \"\", \"affection\": 0, \"familiarity\": 0, \"sharedHistory\": false, \"reason\": \"\" },\n" +
            "  \"landing\": { \"touchy\": [], \"winsHerOver\": [] },\n" +
            "  \"world\": {\n" +
            "    \"backstory\": [],\n" +
            "    \"lastWeek\": [ { \"daysAgo\": 2, \"text\": \"\" } ],\n" +
            "    \"openThreads\": [ { \"text\": \"\", \"goodOutcome\": \"\", \"badOutcome\": \"\" } ],\n" +
            "    \"rightNow\": \"\"\n" +
            "  }\n" +
            "}\n" +
            "Rules:\n" +
            "- temperament: each value from 0 to 1 says how well that word fits her. Leave all at 0 when the description gives no hint. reason is one short line.\n" +
            "- relationship.role is one of: stranger, acquaintance, friend, close_friend, family, partner, rival. label is the description's own word, such as \"stepsister\" or \"coworker\", or empty.\n" +
            "- relationship.affection is how much she likes the user when they first talk, from -100 to 100. relationship.familiarity is how well they already know each other, from 0 to 100. " +
            "Take both from the description. With no hint: stranger, 0 and 0.\n" +
            "- relationship.sharedHistory is true only when the description says they share a past.\n" +
            "- landing.touchy: up to 5 short phrases for what annoys or hurts her. landing.winsHerOver: up to 5 for what she warms to.\n" +
            "- world.backstory: 10 to 20 short facts about her life, each one sentence, in the third person.\n" +
            "- world.lastWeek: 5 to 8 small things that happened to her in the last week, with daysAgo from 0 to 7. Ordinary life, not drama.\n" +
            "- world.openThreads: 2 or 3 things she is in the middle of, waiting for or dreading, each with one line for how it could go well and one for how it could go badly.\n" +
            "- world.rightNow: one sentence for what she is doing at the moment the user first talks to her.\n" +
            "- Stay inside the description. Do not contradict it. Invent only what fits it.\n" +
            "- Do not mention the user in backstory, lastWeek or openThreads unless sharedHistory is true.\n" +
            "- Keep everything non-sexual. Nobody in her world is forced, pressured or distressed for someone's enjoyment.\n" +
            "- Plain numbers, no plus signs. No comments. No text outside the JSON object.";

        /// <summary>The user message of the request: her name and the persona prompt, as data.</summary>
        public static string UserMessage(string personaName, string personaPrompt)
        {
            var name = string.IsNullOrWhiteSpace(personaName) ? "the companion" : personaName.Trim();
            var prompt = string.IsNullOrWhiteSpace(personaPrompt) ? "(no description)" : personaPrompt.Trim();
            return "Character name: " + name + "\nCharacter description:\n" + prompt;
        }
    }
}
