using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-157: what the user wrote comes first; the readings fill only what is left.</summary>
    public sealed class UserFirstTests
    {
        static string Source(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/" + relative));
        }

        [TearDown]
        public void TearDown()
        {
            AdultNotesNow.Reset();
        }

        [Test]
        public void The18PlusReadings_GetHerNotesAfterHerPrompt()
        {
            const string prompt = "Mira is a ferry captain.";
            AdultNotesNow.Reset();
            Assert.AreSame(prompt, AdultNotesNow.ForReading(prompt), "no notes: the same input and hash as before");

            AdultNotesNow.Set("She is very shy about kissing.");
            var input = AdultNotesNow.ForReading(prompt);
            StringAssert.StartsWith(prompt, input);
            StringAssert.Contains(AdultNotesNow.ReadingLabel + "\nShe is very shy about kissing.", input);
        }

        [Test]
        public void The18PlusPrompts_PutTheUsersWordsFirst()
        {
            StringAssert.Contains("What the user wrote comes first", IntimateSecretsReader.SystemPrompt(3, 2, 2));
            StringAssert.Contains("18+ notes", IntimateSecretsReader.SystemPrompt(3, 2, 2));
            StringAssert.Contains("Only when they say nothing", IntimateSecretsReader.SystemPrompt(3, 2, 2));
            StringAssert.Contains("when her 18+ notes say how experienced, shy or bold she is, that decides it", InnocenceReader.SystemPrompt);
        }

        [Test]
        public void TheKeepers_ReadTheNotes_AndStayLocal()
        {
            foreach (var file in new[] { "Settings/InnocenceKeeper.cs", "Settings/IntimateSecretsKeeper.cs" })
            {
                var source = Source(file);
                StringAssert.Contains("_readInput = AdultNotesNow.ForReading(prompt);", source, file);
                StringAssert.Contains("_hash = CharacterSheet.HashPrompt(_readInput);", source, file);
                StringAssert.Contains("Read(_personaId, _readInput,", source, file);
                StringAssert.Contains("LocalProvider(", source, file);
            }
        }

        [Test]
        public void Pisara_ReadsTheLorebook_AndAn18PlusOneOnlyLocally()
        {
            var book = new Lorebook();
            book.Entries.Add(new LoreEntry { Content = "The harbor floods every spring.", Order = 20 });
            book.Entries.Add(new LoreEntry { Content = "Mira's mother runs the inn.", Constant = true, Order = 50 });
            book.Entries.Add(new LoreEntry { Content = "Off.", Enabled = false });

            var text = LoreReading.Append("Mira.", book, false);
            StringAssert.Contains(LoreReading.Heading + "\n- Mira's mother runs the inn.\n- The harbor floods every spring.", text, "constant first, then order");
            StringAssert.DoesNotContain("Off.", text);
            Assert.AreEqual("Mira.", LoreReading.Append("Mira.", null, true));
            Assert.AreEqual("Mira.", LoreReading.Append("Mira.", book, false, 10), "nothing fits the budget");

            book.Adult = true;
            Assert.AreEqual("Mira.", LoreReading.Append("Mira.", book, false), "an 18+ lorebook never goes to a cloud or gate-closed reading");
            StringAssert.Contains(LoreReading.Heading, LoreReading.Append("Mira.", book, true));

            StringAssert.StartsWith("Her lorebook", LoreReading.Heading);
            StringAssert.Contains("A part headed \"Her lorebook\"", CharacterSheetPrompt.System);
        }

        [Test]
        public void PisaraAndTheStudio_PassTheLorebook()
        {
            var keeper = Source("Settings/PisaraReadingKeeper.cs");
            StringAssert.Contains("LoreReading.Append(prompt, LoreNow.Book, keepsLocal && intimateActive)", keeper);
            StringAssert.Contains("text = LoreReading.Append(text, lore, keepsLocal && intimateActive);", keeper);
            StringAssert.Contains("ReadDraftAsync(name, PersonaText.Build(input, name, RelationshipRoles.Friend), _lore, cancel.Token)", Source("UI/NewUI/PersonaStudioView.cs"));
        }
    }
}
