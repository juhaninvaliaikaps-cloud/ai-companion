using System;
using System.Collections.Generic;
using AICompanion.Core;

namespace AICompanion.Memory
{
    /// <summary>
    /// The shared-history guard for seeding (JUH-118, docs/PISARA.md "Guards"): her world may
    /// mention the user only when the persona prompt says they share a past. Models still slip,
    /// so before seeding every backstory fact, week entry and open thread that mentions the user
    /// is dropped when <c>sheet.relationship.sharedHistory</c> is false. The cached sheet itself
    /// is not changed.
    /// </summary>
    public static class WorldGuard
    {
        static readonly string[] UserWords = { "user", "you", "your", "yours", "yourself" };

        /// <summary>
        /// True when a line talks about the user: "the user", "the user's", or the second person.
        /// Whole words only, so "youth" or "superuser" do not count.
        /// </summary>
        public static bool MentionsUser(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var lower = text.ToLowerInvariant();
            var start = -1;
            for (var i = 0; i <= lower.Length; i++)
            {
                var letter = i < lower.Length && char.IsLetter(lower[i]);
                if (letter)
                {
                    if (start < 0)
                        start = i;
                    continue;
                }

                if (start < 0)
                    continue;
                var word = lower.Substring(start, i - start);
                start = -1;
                for (var k = 0; k < UserWords.Length; k++)
                {
                    if (word == UserWords[k])
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A copy of the sheet that is safe to seed. With a shared past it is the sheet as read;
        /// without one, every world line that mentions the user is left out.
        /// <paramref name="dropped"/> gets the lines that were left out (may be null).
        /// </summary>
        public static CharacterSheet ForSeeding(CharacterSheet sheet, List<string> dropped = null)
        {
            if (sheet == null)
                return null;
            var copy = CharacterSheet.FromJson(sheet.ToJson());
            if (copy == null)
                return null;
            var world = copy.world;
            Punctuate(world);
            if (copy.relationship != null && copy.relationship.sharedHistory)
                return copy;

            for (var i = world.backstory.Count - 1; i >= 0; i--)
            {
                if (!MentionsUser(world.backstory[i]))
                    continue;
                Note(dropped, world.backstory[i]);
                world.backstory.RemoveAt(i);
            }

            for (var i = world.lastWeek.Count - 1; i >= 0; i--)
            {
                if (!MentionsUser(world.lastWeek[i].text))
                    continue;
                Note(dropped, world.lastWeek[i].text);
                world.lastWeek.RemoveAt(i);
            }

            for (var i = world.openThreads.Count - 1; i >= 0; i--)
            {
                var thread = world.openThreads[i];
                if (!MentionsUser(thread.text) && !MentionsUser(thread.goodOutcome) && !MentionsUser(thread.badOutcome))
                    continue;
                Note(dropped, thread.text);
                world.openThreads.RemoveAt(i);
            }

            return copy;
        }

        /// <summary>
        /// The first-scene line, or empty. Without a shared past, a "right now" that already has
        /// the user in it is not used: the scene is what she was doing by herself. With a shared
        /// past the line does not say they have never talked: they know each other already.
        /// </summary>
        public static string FirstScene(CharacterSheet sheet)
        {
            if (sheet == null || sheet.world == null || sheet.source != CharacterSheet.SourceModel)
                return string.Empty;
            var now = Sentence(sheet.world.rightNow);
            if (now.Length == 0)
                return string.Empty;
            var shared = sheet.relationship != null && sheet.relationship.sharedHistory;
            if (!shared && MentionsUser(now))
                return string.Empty;
            if (shared)
                return "The user has just come to talk to you. You were in the middle of this: " + now
                    + " Start from there, in your own words, as someone who knows them.";
            var copy = new CharacterSheet();
            copy.world.rightNow = now;
            return WorldMemory.FirstScene(copy);
        }

        /// <summary>
        /// A line as a sentence: trimmed, with a full stop when it ends in a letter or digit.
        /// Models often leave it out, and the lines are joined into longer text (a thread and its
        /// outcome, the first scene).
        /// </summary>
        public static string Sentence(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;
            var text = line.Trim();
            return char.IsLetterOrDigit(text[text.Length - 1]) ? text + "." : text;
        }

        static void Punctuate(SheetWorld world)
        {
            for (var i = 0; i < world.backstory.Count; i++)
                world.backstory[i] = Sentence(world.backstory[i]);
            for (var i = 0; i < world.lastWeek.Count; i++)
                world.lastWeek[i].text = Sentence(world.lastWeek[i].text);
            for (var i = 0; i < world.openThreads.Count; i++)
            {
                var thread = world.openThreads[i];
                thread.text = Sentence(thread.text);
                thread.goodOutcome = Sentence(thread.goodOutcome);
                thread.badOutcome = Sentence(thread.badOutcome);
            }

            world.rightNow = Sentence(world.rightNow);
        }

        static void Note(List<string> dropped, string line)
        {
            if (dropped != null && !string.IsNullOrWhiteSpace(line))
                dropped.Add(line.Trim());
        }
    }
}
