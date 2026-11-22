using System.Collections.Generic;
using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-171: how she talks, the last Pisara reading.</summary>
    public sealed class VoiceTests
    {
        const string Reply = "Here you go:\n```json\n{\"summary\": \"Clipped, commanding, a soldier's bluntness.\", \"register\": \"casual but sharp\", " +
                             "\"rhythm\": \"short sentences\", \"vocabulary\": \"sword terms\", \"habits\": [\"calls the user 'rookie'\", \"\", \"answers questions with orders\"], " +
                             "\"never\": [\"begs\"], \"happy\": \"teases with a grin\", \"annoyed\": \"goes cold and curt\", \"flustered\": \"covers it with a sharp order\", " +
                             "\"sad\": \"goes quiet\", \"affectionate\": \"gruff kindness\", \"stranger\": \"tests them\", \"close\": \"drops her guard a little\", " +
                             "\"lines\": {\"content\": [\"Keep up, rookie.\", \"Hah. Not bad.\", \"Keep up the pace.\", \"Eyes front.\", \"A fifth.\"], " +
                             "\"flustered\": [\"Wh- that's an order, not a compliment!\", \"Stop staring and march.\"], \"happy\": [\"Ha! Again!\"]}}\n```";

        static VoiceSheet Sheet()
        {
            return VoiceSheet.Parse(Reply);
        }

        static TemperamentAxes Confident()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Assertiveness, 85f);
            axes.Set(TemperamentAxes.Confidence, 90f);
            return axes;
        }

        // ---- Parsing ---------------------------------------------------------------------------

        [Test]
        public void TheReading_IsParsedAndTrimmed()
        {
            var sheet = Sheet();
            Assert.IsNotNull(sheet);
            Assert.AreEqual("short sentences", sheet.rhythm);
            Assert.AreEqual(2, sheet.habits.Length, "empty items are dropped");
            CollectionAssert.AreEqual(new[] { "Keep up, rookie.", "Hah. Not bad.", "Eyes front." }, sheet.lines.content,
                "at most 3 per mood, and \"Keep up the pace.\" starts like \"Keep up, rookie.\" so it is dropped");
            Assert.AreEqual(2, sheet.lines.flustered.Length);
            Assert.IsNull(VoiceSheet.Parse("no json here"));
            Assert.IsNull(VoiceSheet.Parse("{}"), "an empty reading is no reading");
        }

        [Test]
        public void TheIntimateReading_IsParsed()
        {
            var voice = IntimateVoice.Parse("{\"holdingBack\": \"short breathless orders\", \"lettingGo\": \"demands, explicit\", \"habits\": [\"praises\"], \"never\": [\"giggles\"], " +
                                            "\"lines\": {\"holdingBack\": [\"Not... not yet.\"], \"lettingGo\": [\"Here. Now.\"]}}");
            Assert.IsNotNull(voice);
            Assert.AreEqual("short breathless orders", voice.holdingBack);
            CollectionAssert.AreEqual(new[] { "Here. Now." }, voice.lines.lettingGo);
            Assert.IsNull(IntimateVoice.Parse("{}"));
        }

        // ---- The user's text wins ----------------------------------------------------------------

        [Test]
        public void TheUsersText_WinsFieldByField()
        {
            var user = new VoiceSheet { rhythm = "long, flowing sentences", habits = new[] { "hums" } };
            var merged = VoiceSheet.Merge(user, Sheet());
            Assert.AreEqual("long, flowing sentences", merged.rhythm);
            CollectionAssert.AreEqual(new[] { "hums" }, merged.habits);
            Assert.AreEqual("sword terms", merged.vocabulary, "an empty field keeps the reading");
            Assert.AreEqual(3, merged.lines.content.Length);

            var ownLines = new VoiceSheet();
            ownLines.lines.flustered = new[] { "My own line." };
            var withOwn = VoiceSheet.Merge(ownLines, Sheet());
            CollectionAssert.AreEqual(new[] { "My own line." }, withOwn.lines.flustered, "the user's lines win mood by mood");
            Assert.AreEqual(3, withOwn.lines.content.Length);
        }

        [Test]
        public void TheEditorsListField_SplitsOnSemicolons()
        {
            CollectionAssert.AreEqual(new[] { "a", "b" }, VoiceText.Split(" a ; ; b ", 5));
            Assert.AreEqual("a; b", VoiceText.Join(new[] { "a", "b" }));
            Assert.AreEqual(0, VoiceText.Split(null, 5).Length);
        }

        // ---- What it is given -----------------------------------------------------------------

        [Test]
        public void TheInput_CarriesWhatTheOtherReadingsFound()
        {
            var sheet = CharacterSheet.Neutral("A swordswoman.");
            sheet.temperament.tsundere = 0.7f;
            sheet.relationship.label = "her new squire";
            var input = VoiceInput.Everyday("A swordswoman.", Confident(), sheet);
            StringAssert.Contains("A swordswoman.", input);
            StringAssert.Contains("assertiveness 85", input);
            StringAssert.Contains("confidence 90", input);
            StringAssert.Contains("mostly tsundere", input);
            StringAssert.Contains("her new squire", input);
            StringAssert.DoesNotContain("Innocent", input, "the everyday input is gate-closed");

            var intimate = VoiceInput.Intimate("A swordswoman.", Confident(), sheet, 85f);
            StringAssert.Contains("Innocent to perverted: 85", intimate);
            Assert.AreNotEqual(VoiceInput.Hash(input), VoiceInput.Hash(intimate));
        }

        [Test]
        public void TheIntimateRequest_NeverGoesToTheCloud_AndTheEverydayOneMay()
        {
            Assert.IsTrue(VoiceReader.BuildIntimate("Ghislaine", "x").BlockCloud);
            Assert.IsFalse(VoiceReader.BuildEveryday("Ghislaine", "x").BlockCloud, "outside private mode it follows her description reading");
            StringAssert.Contains("What the user wrote comes first", VoiceReader.EverydaySystemPrompt);
            StringAssert.Contains("What the user wrote comes first", VoiceReader.IntimateSystemPrompt);
            StringAssert.Contains("Character name: Ghislaine", VoiceReader.BuildEveryday("Ghislaine", "x").Messages[0].Content);
        }

        [Test]
        public void TheIntimateReading_IsRefusedWhileTheGateIsClosed()
        {
            var result = VoiceReader.ReadIntimateAsync(null, false, "Mira", "x", System.Threading.CancellationToken.None).Result;
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(VoiceReader.GateClosed, result.Failure);
        }

        // ---- The block for chat --------------------------------------------------------------

        [Test]
        public void TheBlock_PicksTheLineForHerMoodAndClosenessNow()
        {
            var block = VoicePrompt.Block("Ghislaine", Sheet(), null, CompanionMood.Flustered, 0.6f, 10f, false, 50f, Confident());
            StringAssert.StartsWith("How Ghislaine talks", block);
            StringAssert.Contains("Now, flustered: covers it with a sharp order.", block);
            StringAssert.DoesNotContain("teases with a grin", block, "only her mood now");
            StringAssert.Contains("still getting to know them): tests them.", block);
            StringAssert.Contains("Habits: calls the user 'rookie'; answers questions with orders.", block);
            StringAssert.DoesNotContain("Keep up, rookie.", block, "her lines go mid-prompt, not in the block");
            StringAssert.Contains(VoicePrompt.NoStammer, block, "a confident one is told not to stammer");

            var close = VoicePrompt.Block("Ghislaine", Sheet(), null, CompanionMood.Content, 0.6f, 80f, false, 50f, TemperamentAxes.Neutral());
            StringAssert.Contains("someone close): drops her guard a little.", close);
            StringAssert.DoesNotContain("Now, ", close, "content has no mood line");
            StringAssert.DoesNotContain(VoicePrompt.NoStammer, close, "a shy one keeps her stammer");
        }

        [Test]
        public void TheIntimatePart_OnlyWhileTheGateIsOpen_ByHerInnocence()
        {
            var intimate = new IntimateVoice { holdingBack = "whispers, embarrassed", lettingGo = "bold demands" }.Normalize();
            Assert.AreEqual(-1, VoicePrompt.Block("M", Sheet(), intimate, CompanionMood.Content, 0f, 10f, false, 50f, null).IndexOf("intimate", System.StringComparison.Ordinal));

            var innocent = VoicePrompt.Block("M", Sheet(), intimate, CompanionMood.Content, 0f, 10f, true, 5f, null);
            StringAssert.Contains("In intimate scenes: whispers, embarrassed.", innocent);
            StringAssert.DoesNotContain("bold demands", innocent);

            var perverted = VoicePrompt.Block("M", Sheet(), intimate, CompanionMood.Content, 0f, 10f, true, 95f, null);
            StringAssert.Contains("In intimate scenes: bold demands.", perverted);

            var between = VoicePrompt.Block("M", Sheet(), intimate, CompanionMood.Content, 0f, 10f, true, 50f, null);
            StringAssert.Contains("When she lets go: bold demands.", between);

            Assert.AreNotEqual(VoicePrompt.Key(Sheet(), CompanionMood.Content, 0f, 10f, true, 5f), VoicePrompt.Key(Sheet(), CompanionMood.Content, 0f, 10f, true, 95f));
            Assert.AreEqual(string.Empty, VoicePrompt.Block("M", new VoiceSheet(), null, CompanionMood.Happy, 1f, 10f, false, 50f, null), "nothing read, no block");
        }

        [Test]
        public void HighDesire_MovesEvenAShyOneTowardLettingGo()
        {
            Assert.AreEqual(0, VoicePrompt.IntimateStage(5f, -1f));
            Assert.AreEqual(0, VoicePrompt.IntimateStage(5f, 30f), "warm is not enough");
            Assert.AreEqual(1, VoicePrompt.IntimateStage(5f, 80f), "very aroused: both lines");
            Assert.AreEqual(2, VoicePrompt.IntimateStage(50f, 80f));
            Assert.AreEqual(2, VoicePrompt.IntimateStage(95f, 5f));

            var intimate = new IntimateVoice { holdingBack = "whispers, embarrassed", lettingGo = "bold demands" }.Normalize();
            var aroused = VoicePrompt.Block("M", Sheet(), intimate, CompanionMood.Content, 0f, 10f, true, 5f, null, 80f);
            StringAssert.Contains("When she lets go: bold demands.", aroused);
            Assert.AreNotEqual(VoicePrompt.Key(Sheet(), CompanionMood.Content, 0f, 10f, true, 5f, 10f), VoicePrompt.Key(Sheet(), CompanionMood.Content, 0f, 10f, true, 5f, 80f));
        }

        [Test]
        public void TheIntimateInput_CarriesHerLibido_AndReReadsOnlyInANewBand()
        {
            var sheet = CharacterSheet.Neutral("A swordswoman.");
            StringAssert.Contains("Libido, how relentless her needs are: 85 of 100 (relentless)", VoiceInput.Intimate("x", Confident(), sheet, 50f, 85f));
            StringAssert.DoesNotContain("Libido", VoiceInput.Intimate("x", Confident(), sheet, 50f));
            Assert.AreEqual(VoiceInput.IntimateHash("x", Confident(), sheet, 50f, 85f), VoiceInput.IntimateHash("x", Confident(), sheet, 52f, 88f), "same bands");
            Assert.AreNotEqual(VoiceInput.IntimateHash("x", Confident(), sheet, 50f, 85f), VoiceInput.IntimateHash("x", Confident(), sheet, 90f, 85f));
        }

        [Test]
        public void VoiceNow_GoesLast_AndCarriesTheSamples()
        {
            VoiceNow.Set("How M talks: ...", new[] { "Keep up, rookie.", "Eyes front." }, new[] { "Keep up, rookie.", "Eyes front." });
            Assert.AreEqual("Persona.\n\nHow M talks: ...", VoiceNow.Append("Persona."));
            Assert.AreEqual(2, VoiceNow.Samples.Count);
            StringAssert.Contains("\"Keep up, rookie.\" / \"Eyes front.\"", VoiceNow.AppendLines("Persona.", 0, new List<string>()));
            var shown = new List<string>();
            var echoed = new List<string>();
            Assert.IsTrue(VoiceNow.TakeUsage(shown, echoed));
            Assert.AreEqual(2, shown.Count, "the showing is counted");
            Assert.IsFalse(VoiceNow.TakeUsage(shown, echoed), "and cleared");
            VoiceNow.AppendLines("Persona.", 0, new List<string>(), false);
            Assert.IsFalse(VoiceNow.TakeUsage(shown, echoed), "a request that is not a chat message counts nothing");
            VoiceNow.Reset();
            Assert.AreEqual("Persona.", VoiceNow.Append("Persona."));
            Assert.AreEqual(0, VoiceNow.Samples.Count);
        }

        // ---- The rotating pool -------------------------------------------------------------

        [Test]
        public void ThePool_IsHerMoodsLinesFirst_ThenHerEverydayOnes()
        {
            var pool = VoicePrompt.Pool(Sheet(), null, CompanionMood.Flustered, 0.6f, false, 50f, -1f);
            Assert.AreEqual("Wh- that's an order, not a compliment!", pool[0]);
            Assert.AreEqual(5, pool.Count);
            Assert.AreEqual(3, VoicePrompt.Pool(Sheet(), null, CompanionMood.Flustered, 0.1f, false, 50f, -1f).Count, "a faint mood: everyday lines only");
        }

        [Test]
        public void WhenAroused_ThePoolIsHerIntimateLines_ByStage()
        {
            var intimate = new IntimateVoice { holdingBack = "h", lettingGo = "l" };
            intimate.lines.holdingBack = new[] { "Not... not yet." };
            intimate.lines.lettingGo = new[] { "Here. Now." };
            intimate.Normalize();
            CollectionAssert.AreEqual(new[] { "Not... not yet." }, VoicePrompt.Pool(Sheet(), intimate, CompanionMood.Content, 0f, true, 5f, 50f), "shy and aroused: holding back");
            CollectionAssert.AreEqual(new[] { "Here. Now." }, VoicePrompt.Pool(Sheet(), intimate, CompanionMood.Content, 0f, true, 95f, 50f));
            Assert.AreEqual(2, VoicePrompt.Pool(Sheet(), intimate, CompanionMood.Content, 0f, true, 50f, 50f).Count, "in between: both");
            Assert.AreEqual(3, VoicePrompt.Pool(Sheet(), intimate, CompanionMood.Content, 0f, true, 95f, 10f).Count, "calm: her everyday lines");
            Assert.AreEqual(3, VoicePrompt.Pool(Sheet(), intimate, CompanionMood.Content, 0f, false, 95f, 80f).Count, "never while the gate is closed");
        }

        [Test]
        public void ThePick_Rotates_AndSkipsWhatSheEchoed()
        {
            var pool = new List<string> { "a1", "b2", "c3", "d4", "e5" };
            CollectionAssert.AreEqual(new[] { "a1", "b2" }, VoicePrompt.Pick(pool, 0, null));
            CollectionAssert.AreEqual(new[] { "c3", "d4" }, VoicePrompt.Pick(pool, 1, null));
            CollectionAssert.AreEqual(new[] { "e5", "a1" }, VoicePrompt.Pick(pool, 2, null));
            CollectionAssert.AreEqual(new[] { "b2", "d4" }, VoicePrompt.Pick(pool, 0, l => l == "a1" || l == "c3"));
            Assert.AreEqual(0, VoicePrompt.Pick(pool, 0, l => true).Count);
            Assert.AreEqual(string.Empty, VoicePrompt.LinesLine(new List<string>()));
        }

        [Test]
        public void ALine_RetiresAfterAFewReplies_OrAtOnceWhenEchoed()
        {
            Assert.IsFalse(VoicePool.ShouldRetire(VoicePool.RetireAfter - 1, false));
            Assert.IsTrue(VoicePool.ShouldRetire(VoicePool.RetireAfter, false));
            Assert.IsTrue(VoicePool.ShouldRetire(0, true));
            Assert.IsTrue(VoicePool.NeedsRefresh(new[] { "last one" }, null));
            Assert.IsFalse(VoicePool.NeedsRefresh(new[] { "a", "b" }, null));
            Assert.IsFalse(VoicePool.NeedsRefresh(new string[0], new[] { "the user's" }), "the user's lines are never refreshed");
            CollectionAssert.AreEqual(new[] { "b" }, VoicePool.Without(new[] { "a", "b" }, "a"));
        }

        [Test]
        public void ARefresh_AddsNewLines_NeverARetiredOne()
        {
            var refilled = VoicePool.Refill(new[] { "Eyes front." }, new[] { "Keep up, rookie.", "March, now.", "Faster!", "One more." }, new[] { "Keep up, rookie." });
            CollectionAssert.AreEqual(new[] { "Eyes front.", "March, now.", "Faster!" }, refilled);
            var retired = VoicePool.Retire(new string[0], "x");
            CollectionAssert.AreEqual(new[] { "x" }, retired);
            CollectionAssert.AreEqual(new[] { "x" }, VoicePool.Retire(retired, "x"), "once");
        }

        [Test]
        public void TheRefreshRequest_IsHerVoiceAndTheLinesSheHad()
        {
            var input = VoiceReader.RefreshInput(Sheet(), VoiceReader.Moment("flustered"), true, new List<string> { "Eyes front." });
            StringAssert.Contains("Clipped, commanding", input);
            StringAssert.Contains("Write 3 new lines for: when she is flustered", input);
            StringAssert.Contains("someone close", input);
            StringAssert.Contains("- Eyes front.", input);
            Assert.IsTrue(VoiceReader.BuildRefresh("M", input, true).BlockCloud, "an 18+ refresh is local only");
            Assert.IsFalse(VoiceReader.BuildRefresh("M", input, false).BlockCloud);
            CollectionAssert.AreEqual(new[] { "One.", "Two." }, VoiceReader.ParseLines("{\"lines\": [\"One.\", \"\", \"Two.\"]}"));
            Assert.AreEqual(0, VoiceReader.ParseLines("nope").Length);
            var refused = VoiceReader.RefreshAsync(null, false, true, false, "M", input, System.Threading.CancellationToken.None).Result;
            Assert.AreEqual(0, refused.Length, "an 18+ refresh with the gate closed sends nothing");
        }

        // ---- Copied samples ----------------------------------------------------------------

        [Test]
        public void ACopiedSampleLine_IsNamedAtOnce()
        {
            var found = new List<string>();
            RepeatedPhrases.AddCopied(new List<string> { "\"Keep up with me, rookie,\" she says." }, new[] { "Keep up with me, rookie." }, found);
            CollectionAssert.Contains(found, "keep up with me");

            var none = new List<string>();
            RepeatedPhrases.AddCopied(new List<string> { "Faster, please." }, new[] { "Keep up with me, rookie." }, none);
            Assert.AreEqual(0, none.Count);
        }

        // ---- Storage -----------------------------------------------------------------------

        [Test]
        public void TheFile_IsPartOfWipeAndExport()
        {
            Assert.IsTrue(LocalDataInventory.IsVoiceName("voice.json"));
            Assert.IsTrue(LocalDataInventory.IsVoiceName("voice-mira.json"));
            Assert.IsFalse(LocalDataInventory.IsVoiceName("voices.json"));
            StringAssert.EndsWith("voice-mira.json", VoiceFile.FileFor("folder", "mira"));
            var file = new VoiceFile { everyday = Sheet() };
            file.UserEveryday.rhythm = "slow";
            Assert.AreEqual("slow", file.Everyday.rhythm);
            Assert.AreEqual("sword terms", file.Everyday.vocabulary);
        }
    }
}
