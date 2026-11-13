using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-117 ("Pisara"): the character sheet format, its tolerant parser and clamps, the
    /// prompt hash, the encrypted store, and the temperament blend (JUH-114).
    /// </summary>
    public class CharacterSheetTests
    {
        const string Reply =
            "Sure! Here is the sheet:\n```json\n" +
            "{\n" +
            "  \"temperament\": { \"calm\": 0.1, \"cheerful\": 0.2, \"shy\": 0.0, \"tsundere\": 0.9, \"moody\": 0.3, \"reason\": \"sharp-tongued but caring\" },\n" +
            "  \"relationship\": { \"role\": \"Family\", \"label\": \"stepsister\", \"affection\": 25, \"familiarity\": 70, \"sharedHistory\": true, \"reason\": \"grew up together\" },\n" +
            "  \"landing\": { \"touchy\": [\"being called childish\", \"  \", \"being called childish\"], \"winsHerOver\": [\"being trusted with a secret\"], },\n" +
            "  \"world\": {\n" +
            "    \"backstory\": [\"She studies design.\", \"She has a {curly} cat named Miso.\"],\n" +
            "    \"lastWeek\": [ { \"daysAgo\": 2, \"text\": \"Failed a quiz.\" }, { \"daysAgo\": 99, \"text\": \"Bought new boots.\" }, { \"daysAgo\": 1, \"text\": \"\" } ],\n" +
            "    \"openThreads\": [ { \"text\": \"Portfolio review on Friday.\", \"goodOutcome\": \"It went well.\", \"badOutcome\": \"It went badly.\" }, ],\n" +
            "    \"rightNow\": \"Sketching at her desk,\\nheadphones on.\"\n" +
            "  }\n" +
            "}\n```\nHope that helps.";

        [TearDown]
        public void TearDown()
        {
            InstallKeyFile.KeyForPath = null;
        }

        // ---- parsing -------------------------------------------------------------------------

        [Test]
        public void Parse_ReadsASheetOutOfProseAndACodeFence()
        {
            var sheet = CharacterSheet.Parse(Reply);
            Assert.IsNotNull(sheet);
            Assert.AreEqual(CharacterSheet.SourceModel, sheet.source);
            Assert.AreEqual(string.Empty, sheet.promptHash);

            Assert.AreEqual(0.9f, sheet.temperament.tsundere, 0.0001f);
            Assert.AreEqual(0.9f, sheet.temperament.Weight(PersonaTemperaments.TsundereId), 0.0001f);
            Assert.AreEqual("sharp-tongued but caring", sheet.temperament.reason);

            Assert.AreEqual(RelationshipRoles.Family, sheet.relationship.role);
            Assert.AreEqual("stepsister", sheet.relationship.label);
            Assert.AreEqual(25f, sheet.relationship.affection);
            Assert.AreEqual(70f, sheet.relationship.familiarity);
            Assert.IsTrue(sheet.relationship.sharedHistory);

            // Blank and repeated phrases are dropped.
            CollectionAssert.AreEqual(new[] { "being called childish" }, sheet.landing.touchy);
            CollectionAssert.AreEqual(new[] { "being trusted with a secret" }, sheet.landing.winsHerOver);

            Assert.AreEqual(2, sheet.world.backstory.Count);
            Assert.AreEqual("She has a {curly} cat named Miso.", sheet.world.backstory[1]);
            Assert.AreEqual(2, sheet.world.lastWeek.Count, "the entry without text is dropped");
            Assert.AreEqual(CharacterSheet.MaxDaysAgo, sheet.world.lastWeek[1].daysAgo);
            Assert.AreEqual(1, sheet.world.openThreads.Count);
            Assert.AreEqual("It went badly.", sheet.world.openThreads[0].badOutcome);
            Assert.AreEqual("Sketching at her desk, headphones on.", sheet.world.rightNow);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("I cannot do that.")]
        [TestCase("{ \"temperament\": { \"calm\": 0.5 ")]
        [TestCase("} {")]
        public void Parse_ReturnsNull_WhenThereIsNoUsableObject(string reply)
        {
            Assert.IsNull(CharacterSheet.Parse(reply));
        }

        [Test]
        public void Parse_FillsMissingParts_AndTheModelCannotSetTheHashOrSource()
        {
            var sheet = CharacterSheet.Parse("{ \"promptHash\": \"abc\", \"source\": \"keywords\", \"createdAtUtc\": \"x\", \"version\": 7, \"temperament\": { \"shy\": 0.8 } }");
            Assert.IsNotNull(sheet);
            Assert.AreEqual(string.Empty, sheet.promptHash);
            Assert.AreEqual(CharacterSheet.SourceModel, sheet.source);
            Assert.AreEqual(string.Empty, sheet.createdAtUtc);
            Assert.AreEqual(CharacterSheet.CurrentVersion, sheet.version);
            Assert.AreEqual(0.8f, sheet.temperament.shy, 0.0001f);
            Assert.AreEqual(RelationshipRoles.Stranger, sheet.relationship.role);
            Assert.AreEqual(0f, sheet.relationship.affection);
            Assert.AreEqual(0, sheet.world.backstory.Count);
            Assert.AreEqual(0, sheet.world.lastWeek.Count);
            Assert.AreEqual(string.Empty, sheet.world.rightNow);
        }

        [Test]
        public void ExtractObject_SkipsBracesInStrings_AndDropsTrailingCommas()
        {
            Assert.AreEqual("{\"a\":\"}{\",\"b\":[1,2]}", CharacterSheet.ExtractObject("x {\"a\":\"}{\",\"b\":[1,2,],} y {\"c\":1}"));
            Assert.AreEqual("{\"a\":\"say \\\"hi\\\" }\"}", CharacterSheet.ExtractObject("{\"a\":\"say \\\"hi\\\" }\"}"));
            Assert.IsNull(CharacterSheet.ExtractObject("no object"));
            Assert.IsNull(CharacterSheet.ExtractObject("{ \"a\": [1, 2 "));
        }

        // ---- clamps --------------------------------------------------------------------------

        [Test]
        public void Normalize_ClampsNumbers_CapsLists_AndCleansText()
        {
            var sheet = new CharacterSheet();
            sheet.temperament.calm = 5f;
            sheet.temperament.moody = -1f;
            sheet.temperament.shy = float.NaN;
            sheet.relationship.role = "Close Friend";
            sheet.relationship.affection = -500f;
            sheet.relationship.familiarity = 500f;
            sheet.relationship.label = new string('x', 100);
            for (var i = 0; i < 40; i++)
                sheet.world.backstory.Add("Fact " + i + ".");
            for (var i = 0; i < 20; i++)
                sheet.world.lastWeek.Add(new SheetWeekEntry { daysAgo = -3, text = "Day " + i });
            for (var i = 0; i < 9; i++)
                sheet.world.openThreads.Add(new SheetThread { text = "Thread " + i });
            sheet.world.openThreads.Insert(0, null);
            sheet.world.rightNow = new string('y', 500);
            sheet.source = "something else";

            sheet.Normalize();

            Assert.AreEqual(1f, sheet.temperament.calm);
            Assert.AreEqual(0f, sheet.temperament.moody);
            Assert.AreEqual(0f, sheet.temperament.shy);
            Assert.AreEqual(RelationshipRoles.CloseFriend, sheet.relationship.role);
            Assert.AreEqual(-100f, sheet.relationship.affection);
            Assert.AreEqual(100f, sheet.relationship.familiarity);
            Assert.AreEqual(CharacterSheet.MaxLabelLength, sheet.relationship.label.Length);
            Assert.AreEqual(CharacterSheet.MaxBackstory, sheet.world.backstory.Count);
            Assert.AreEqual(CharacterSheet.MaxWeekEntries, sheet.world.lastWeek.Count);
            Assert.AreEqual(0, sheet.world.lastWeek[0].daysAgo);
            Assert.AreEqual(CharacterSheet.MaxThreads, sheet.world.openThreads.Count);
            Assert.AreEqual(CharacterSheet.MaxLineLength, sheet.world.rightNow.Length);
            Assert.AreEqual(CharacterSheet.SourceDefault, sheet.source);
        }

        [Test]
        public void Normalize_SurvivesNullParts_AndAStrangerNeverSharesHistory()
        {
            var sheet = new CharacterSheet { temperament = null, relationship = null, landing = null, world = null };
            sheet.Normalize();
            Assert.IsNotNull(sheet.temperament);
            Assert.IsNotNull(sheet.world.openThreads);

            sheet.relationship.role = "best buddies forever";
            sheet.relationship.sharedHistory = true;
            sheet.Normalize();
            Assert.AreEqual(RelationshipRoles.Stranger, sheet.relationship.role);
            Assert.IsFalse(sheet.relationship.sharedHistory);

            sheet.relationship.role = RelationshipRoles.Family;
            sheet.relationship.sharedHistory = true;
            sheet.Normalize();
            Assert.IsTrue(sheet.relationship.sharedHistory);
        }

        [TestCase("stranger", RelationshipRoles.Stranger)]
        [TestCase(" FRIEND ", RelationshipRoles.Friend)]
        [TestCase("close-friend", RelationshipRoles.CloseFriend)]
        [TestCase("Close Friend", RelationshipRoles.CloseFriend)]
        [TestCase("partner", RelationshipRoles.Partner)]
        [TestCase("rival", RelationshipRoles.Rival)]
        [TestCase("acquaintance", RelationshipRoles.Acquaintance)]
        [TestCase("", RelationshipRoles.Stranger)]
        [TestCase(null, RelationshipRoles.Stranger)]
        [TestCase("nemesis", RelationshipRoles.Stranger)]
        public void Roles_Normalize(string value, string expected)
        {
            Assert.AreEqual(expected, RelationshipRoles.Normalize(value));
        }

        // ---- the prompt hash -----------------------------------------------------------------

        [Test]
        public void Hash_IgnoresLineEndingsAndOuterSpace_AndChangesOnAnyEdit()
        {
            var hash = CharacterSheet.HashPrompt("You are Lola.\nShy and quiet.");
            Assert.AreEqual(64, hash.Length);
            Assert.AreEqual(hash, CharacterSheet.HashPrompt("  You are Lola.\r\nShy and quiet.\n\n"));
            Assert.AreNotEqual(hash, CharacterSheet.HashPrompt("You are Lola.\nShy and loud."));
            Assert.AreEqual(CharacterSheet.HashPrompt(string.Empty), CharacterSheet.HashPrompt(null));

            var sheet = CharacterSheet.Neutral("You are Lola.\nShy and quiet.");
            Assert.IsTrue(sheet.IsFor("You are Lola.\r\nShy and quiet."));
            Assert.IsFalse(sheet.IsFor("You are Lola."));
            Assert.AreEqual(CharacterSheet.SourceDefault, sheet.source);
            Assert.IsFalse(new CharacterSheet().IsFor(string.Empty), "a sheet without a hash is never current");
        }

        // ---- json and the store --------------------------------------------------------------

        [Test]
        public void Json_RoundTrips_KeepingTheHashSourceAndDate()
        {
            var sheet = CharacterSheet.Parse(Reply);
            sheet.promptHash = CharacterSheet.HashPrompt("prompt");
            sheet.createdAtUtc = "2026-11-13T20:00:00Z";

            var back = CharacterSheet.FromJson(sheet.ToJson());
            Assert.IsNotNull(back);
            Assert.AreEqual(sheet.promptHash, back.promptHash);
            Assert.AreEqual(CharacterSheet.SourceModel, back.source);
            Assert.AreEqual("2026-11-13T20:00:00Z", back.createdAtUtc);
            Assert.AreEqual(sheet.relationship.label, back.relationship.label);
            Assert.AreEqual(sheet.world.backstory.Count, back.world.backstory.Count);
            Assert.AreEqual(sheet.world.openThreads[0].goodOutcome, back.world.openThreads[0].goodOutcome);

            Assert.IsNull(CharacterSheet.FromJson(null));
            Assert.IsNull(CharacterSheet.FromJson("not json"));
        }

        [Test]
        public void Store_SavesEncrypted_PerPersona_AndOnlyReturnsASheetForTheSamePrompt()
        {
            var folder = Path.Combine(Path.GetTempPath(), "happi-sheet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = (byte)(i * 7 + 1);
            InstallKeyFile.KeyForPath = _ => key;
            try
            {
                Assert.AreEqual(Path.Combine(folder, "sheet.json"), CharacterSheetStore.FileFor(folder, string.Empty));
                var path = CharacterSheetStore.FileFor(folder, "Lola Bee");
                Assert.AreEqual(Path.Combine(folder, "sheet-lola-bee.json"), path);
                Assert.IsNull(CharacterSheetStore.FileFor(null, "lola"));

                Assert.IsNull(CharacterSheetStore.Load(path));
                var sheet = CharacterSheet.Parse(Reply);
                sheet.promptHash = CharacterSheet.HashPrompt("the prompt");
                Assert.IsTrue(CharacterSheetStore.Save(path, sheet));
                Assert.IsFalse(CharacterSheetStore.Save(null, sheet));
                Assert.IsFalse(CharacterSheetStore.Save(path, null));

                var bytes = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path));
                StringAssert.DoesNotContain("stepsister", bytes);

                var loaded = CharacterSheetStore.LoadFor(path, "the prompt");
                Assert.IsNotNull(loaded);
                Assert.AreEqual("stepsister", loaded.relationship.label);
                Assert.IsNull(CharacterSheetStore.LoadFor(path, "an edited prompt"));
                Assert.IsNull(CharacterSheetStore.Load(CharacterSheetStore.FileFor(folder, "kiki")));
            }
            finally
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [Test]
        public void Inventory_CoversSheetFiles_SoWipeAndExportIncludeThem()
        {
            Assert.IsTrue(LocalDataInventory.IsSheetName("sheet.json"));
            Assert.IsTrue(LocalDataInventory.IsSheetName("sheet-lola.json"));
            Assert.IsFalse(LocalDataInventory.IsSheetName("sheets.json"));
            Assert.IsFalse(LocalDataInventory.IsSheetName("sheet-lola.txt"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("sheet-lola.json"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("portraits/sheet-lola.json"));
        }

        // ---- the prompt ----------------------------------------------------------------------

        [Test]
        public void Prompt_NamesEveryFieldTheParserReads_AndCarriesTheGuards()
        {
            foreach (var field in new[]
            {
                "temperament", "calm", "cheerful", "shy", "tsundere", "moody", "reason",
                "relationship", "role", "label", "affection", "familiarity", "sharedHistory",
                "landing", "touchy", "winsHerOver",
                "world", "backstory", "lastWeek", "daysAgo", "openThreads", "goodOutcome", "badOutcome", "rightNow",
            })
                StringAssert.Contains("\"" + field + "\"", CharacterSheetPrompt.System, field);
            foreach (var role in RelationshipRoles.All)
                StringAssert.Contains(role, CharacterSheetPrompt.System);
            StringAssert.Contains("unless sharedHistory is true", CharacterSheetPrompt.System);
            StringAssert.Contains("non-sexual", CharacterSheetPrompt.System);
            StringAssert.DoesNotContain("intimate", CharacterSheetPrompt.System.ToLowerInvariant());

            // The example in the prompt is itself a sheet the parser accepts.
            Assert.IsNotNull(CharacterSheet.Parse(CharacterSheetPrompt.System));

            Assert.AreEqual("Character name: Lola\nCharacter description:\nShy.", CharacterSheetPrompt.UserMessage(" Lola ", " Shy. "));
            StringAssert.Contains("the companion", CharacterSheetPrompt.UserMessage(null, null));
        }

        // ---- the temperament blend (JUH-114) -------------------------------------------------

        static SheetTemperament Weights(float calm = 0f, float cheerful = 0f, float shy = 0f, float tsundere = 0f, float moody = 0f)
        {
            return new SheetTemperament { calm = calm, cheerful = cheerful, shy = shy, tsundere = tsundere, moody = moody };
        }

        [Test]
        public void Blend_BelowConfidence_OrNoWeights_IsBalanced()
        {
            foreach (var weights in new[] { null, Weights(), Weights(shy: 0.3f, moody: 0.34f) })
            {
                var reading = PersonaTemperamentBlend.From(weights);
                Assert.AreSame(PersonaTemperament.Default, reading.Temperament);
                Assert.IsFalse(reading.IsBlend);
                Assert.AreEqual("Balanced", reading.Summary);
            }
        }

        [Test]
        public void Blend_AClearLeader_IsThatPresetUnchanged()
        {
            var reading = PersonaTemperamentBlend.From(Weights(cheerful: 0.72f, shy: 0.2f, calm: 0.1f));
            Assert.AreSame(PersonaTemperaments.Get(PersonaTemperaments.CheerfulId), reading.Temperament);
            Assert.IsFalse(reading.IsBlend);
            Assert.AreEqual("Cheerful 72%", reading.Summary);

            // Second place under half of the leader does not blend.
            Assert.IsFalse(PersonaTemperamentBlend.From(Weights(tsundere: 0.9f, moody: 0.44f)).IsBlend);
        }

        [Test]
        public void Blend_TwoClosePresets_MixByWeight()
        {
            var shy = PersonaTemperaments.Get(PersonaTemperaments.ShyId);
            var cheerful = PersonaTemperaments.Get(PersonaTemperaments.CheerfulId);
            var reading = PersonaTemperamentBlend.From(Weights(shy: 0.6f, cheerful: 0.4f));

            Assert.IsTrue(reading.IsBlend);
            Assert.AreEqual("Shy 60%, Cheerful 40%", reading.Summary);
            var mixed = reading.Temperament;
            Assert.AreEqual(PersonaTemperamentBlend.BlendId, mixed.Id);
            Assert.AreEqual("Shy 60%, Cheerful 40%", mixed.Name);

            foreach (var mood in new[] { CompanionMood.Happy, CompanionMood.Flustered, CompanionMood.Sad, CompanionMood.Annoyed })
            {
                Assert.AreEqual(shy.Gain(mood, true) * 0.6f + cheerful.Gain(mood, true) * 0.4f, mixed.Gain(mood, true), 0.0001f, mood.ToString());
                Assert.AreEqual(shy.HalfLifeScale(mood) * 0.6f + cheerful.HalfLifeScale(mood) * 0.4f, mixed.HalfLifeScale(mood), 0.0001f, mood.ToString());
            }

            // Shy leads with 60 %: its bias is kept, scaled; shy has no baseline, so there is none.
            Assert.AreEqual(CompanionMood.Content, mixed.Baseline);
            Assert.AreEqual(CompanionMood.Flustered, mixed.RestBias);
            Assert.AreEqual(shy.RestBiasStrength * 0.6f, mixed.RestBiasStrength, 0.0001f);

            // The shared presets are not changed by a blend.
            Assert.AreEqual(CompanionMood.Happy, cheerful.Baseline);
            Assert.AreEqual(1.4f, shy.Gain(CompanionMood.Flustered, true), 0.0001f);
        }

        [Test]
        public void Blend_KeepsTheLeadersBaseline_OnlyWithAClearShare()
        {
            var strong = PersonaTemperamentBlend.From(Weights(cheerful: 0.9f, calm: 0.5f)).Temperament;
            Assert.AreEqual(CompanionMood.Happy, strong.Baseline, "0.9 of 1.4 is 64 %");

            var even = PersonaTemperamentBlend.From(Weights(cheerful: 0.6f, calm: 0.55f)).Temperament;
            Assert.AreEqual(CompanionMood.Content, even.Baseline, "52 % is not enough to rest on happy");
        }

        [Test]
        public void Blend_NeverTouchesIntimate_AndStaysInsideTheClamps()
        {
            foreach (var a in PersonaTemperaments.Ids)
            {
                foreach (var b in PersonaTemperaments.Ids)
                {
                    var mixed = PersonaTemperamentBlend.Mix(PersonaTemperaments.Get(a), PersonaTemperaments.Get(b), 0.5f, "t");
                    Assert.AreEqual(1f, mixed.Gain(CompanionMood.Intimate, true), a + "+" + b);
                    Assert.AreEqual(1f, mixed.HalfLifeScale(CompanionMood.Intimate), a + "+" + b);
                    Assert.AreEqual(0f, mixed.RestFloor(CompanionMood.Intimate), a + "+" + b);
                    Assert.AreNotEqual(CompanionMood.Intimate, mixed.Baseline);
                    Assert.LessOrEqual(mixed.RestBiasStrength, PersonaTemperament.MaxRestBias);
                    for (var i = 1; i < CompanionMoodNames.Count; i++)
                    {
                        var gain = mixed.Gain((CompanionMood)i, true);
                        Assert.GreaterOrEqual(gain, PersonaTemperament.MinScale);
                        Assert.LessOrEqual(gain, PersonaTemperament.MaxScale);
                    }
                }
            }

            // Two defaults mix to the phase 1/2 numbers.
            Assert.IsTrue(PersonaTemperamentBlend.Mix(null, null, 0.5f, "t").IsNeutral);
        }

        [Test]
        public void Blend_IsStable_ForTheSameWeights()
        {
            var a = PersonaTemperamentBlend.From(Weights(shy: 0.5f, moody: 0.5f));
            var b = PersonaTemperamentBlend.From(Weights(shy: 0.5f, moody: 0.5f));
            Assert.AreEqual(a.Summary, b.Summary);
            Assert.AreEqual("Shy 50%, Moody 50%", a.Summary, "picker order breaks the tie");
        }
    }
}
