namespace AICompanion.Core
{
    /// <summary>
    /// The request that asks a model for a <see cref="CharacterSheet"/> (JUH-117). The wording was
    /// tuned against local 12B models in JUH-118 (docs/PISARA.md, "Tuning the reading prompt"):
    /// exact counts instead of ranges, what each temperament word means, anchors for the numbers,
    /// and a placeholder list of the wanted length. The JSON shape is the contract and must keep
    /// matching <see cref="CharacterSheet"/>.
    /// The caller decides which model gets it: a private persona, Private mode or an open
    /// intimate gate means the local model only (<see cref="IntimateExpressions.KeepsLocal"/>).
    /// </summary>
    public static class CharacterSheetPrompt
    {
        public const string System =
            "You read a character description for a desktop companion and fill in a character sheet about her. Reply with one JSON object and nothing else. Use exactly these keys:\n" +
            "{\n" +
            "  \"temperament\": { \"calm\": 0.0, \"cheerful\": 0.0, \"shy\": 0.0, \"tsundere\": 0.0, \"moody\": 0.0, \"reason\": \"\" },\n" +
            "  \"relationship\": { \"role\": \"stranger\", \"label\": \"\", \"affection\": 0, \"familiarity\": 0, \"sharedHistory\": false, \"reason\": \"\" },\n" +
            "  \"landing\": { \"touchy\": [\"\", \"\"], \"winsHerOver\": [\"\", \"\"] },\n" +
            "  \"world\": {\n" +
            "    \"backstory\": [\"\", \"\", \"\", \"\", \"\", \"\", \"\", \"\", \"\", \"\", \"\", \"\"],\n" +
            "    \"lastWeek\": [ { \"daysAgo\": 0, \"text\": \"\" }, { \"daysAgo\": 1, \"text\": \"\" }, { \"daysAgo\": 2, \"text\": \"\" }, { \"daysAgo\": 3, \"text\": \"\" }, { \"daysAgo\": 5, \"text\": \"\" }, { \"daysAgo\": 6, \"text\": \"\" } ],\n" +
            "    \"openThreads\": [ { \"text\": \"\", \"goodOutcome\": \"\", \"badOutcome\": \"\" }, { \"text\": \"\", \"goodOutcome\": \"\", \"badOutcome\": \"\" } ],\n" +
            "    \"rightNow\": \"\"\n" +
            "  }\n" +
            "}\n" +
            "Rules:\n" +
            "- temperament: each value from 0 to 1 says how well that word fits her. calm: steady, composed, patient, hard to rattle (a nervous or timid character is not calm). cheerful: sunny, upbeat, playful. shy: timid, easily flustered, reserved. tsundere: prickly, teasing or bratty on the surface, caring underneath. moody: gloomy, brooding, low or changeable. Give the one word that fits best a high value. A second word gets more than 0.3 only when the description clearly shows that side too; the rest stay at 0.2 or below. Leave all at 0 when the description gives no hint. reason is one short line.\n" +
            "- relationship.role is who she is to the user. Decide first whether they are related by blood, marriage or adoption: if so, the role is family, however close or distant they are. Exactly one of: stranger (they have never met), acquaintance (they barely know each other), friend, close_friend (best or oldest friends), family (any relative: a sister, stepsister, cousin, mother, aunt, in-law; a relative is always family, even a close one), partner (girlfriend, wife, lover), rival. label is the description's own word, such as \"stepsister\" or \"coworker\", or empty.\n" +
            "- relationship.affection is how much she likes the user when they first talk, from -100 to 100: 0 for a stranger (between -10 and 10 unless the description says she already likes or dislikes them), about 20 for someone she bickers with but cares about, about 50 for a good friend, 80 or more for someone she loves openly, below 0 only when she dislikes them. relationship.familiarity is how well they already know each other, from 0 to 100: 0 never met, about 30 acquaintances, about 60 friends, 80 or more when they have known each other for years or live together. Take both from the description. With no hint: stranger, 0 and 0.\n" +
            "- relationship.sharedHistory is true only when the description says they already share a past: relatives, old friends, partners, people who have lived or worked together. For a stranger, or someone she has only just met, it is false.\n" +
            "- landing.touchy: 2 to 5 short phrases for what annoys or hurts her. landing.winsHerOver: 2 to 5 short phrases for what she warms to.\n" +
            "- world.backstory: exactly 12 facts about her life, each one short full sentence in the third person that starts with her name or \"She\". Count them: 12. Cover different things: where she lives, family, work or studies, a friend, hobbies, likes and dislikes, a habit, a memory, something she wants. At most 5 facts repeat the description; the others are new small details that fit it, such as a named friend or relative, her home, a pet peeve, a favourite thing or a memory.\n" +
            "- world.lastWeek: exactly 6 small things that happened to her in the last week, each with daysAgo from 0 to 7 (0 is today). Count them: 6. Ordinary life, not drama. Each one is a different event.\n" +
            "- world.openThreads: exactly 2 things she is in the middle of, waiting for or dreading, each with one line for how it could go well and one for how it could go badly.\n" +
            "- world.rightNow: one sentence for what she is doing by herself at the moment the user first talks to her. The user is not in it.\n" +
            "- Stay inside the description. Do not contradict it or yourself. Invent only what fits it. Invent nothing about the user: no pets, jobs, friends or habits the description does not give them.\n" +
            "- Her world is her own life. Do not mention the user in backstory, lastWeek or openThreads unless sharedHistory is true. When it is true, at most 2 backstory facts and at most 2 lastWeek entries may involve the user, called \"the user\"; everything else, and every open thread, is about her own life without the user.\n" +
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
