using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-123: her secrets, found by talking with her.</summary>
    public sealed class HerSecretsTests
    {
        static SheetLanding Landing(string[] touchy, string[] wins)
        {
            return new SheetLanding { touchy = new List<string>(touchy), winsHerOver = new List<string>(wins) };
        }

        static readonly SheetLanding Mira = Landing(
            new[] { "being called childish", "", "being rushed." },
            new[] { "being trusted with a secret", "honest compliments" });

        // ---- Ids and keys -----------------------------------------------------------------------

        [Test]
        public void From_GivesIdsInSheetOrder_StingsFirst_AndSkipsBlanks()
        {
            var items = HerSecrets.From(Mira);
            Assert.AreEqual(4, items.Count);
            Assert.AreEqual("t1", items[0].Id);
            Assert.AreEqual("being called childish", items[0].Text);
            Assert.AreEqual("t2", items[1].Id, "the blank phrase gets no id");
            Assert.AreEqual("being rushed", items[1].Text, "the closing full stop goes");
            Assert.AreEqual(SecretGroup.Stings, items[1].Group);
            Assert.AreEqual("w1", items[2].Id);
            Assert.AreEqual("w2", items[3].Id);
            Assert.AreEqual(SecretGroup.WinsHerOver, items[3].Group);

            Assert.AreEqual(0, HerSecrets.From(null).Count);
            Assert.AreEqual(0, HerSecrets.From(new SheetLanding()).Count);
        }

        [Test]
        public void From_TakesAtMostFivePerList()
        {
            var items = HerSecrets.From(Landing(new[] { "a", "b", "c", "d", "e", "f", "g" }, new string[0]));
            Assert.AreEqual(CharacterSheet.MaxLandingPhrases, items.Count);
            Assert.AreEqual("t5", items[4].Id);
        }

        [Test]
        public void Key_IsStable_AndIgnoresCaseSpacesAndTheFullStop()
        {
            // Pinned: the key must never change between runs, machines or .NET versions.
            Assert.AreEqual("277f0a1e", HerSecrets.Key("being trusted with a secret"));
            Assert.AreEqual(HerSecrets.Key("being trusted with a secret"), HerSecrets.Key("  Being  trusted with a SECRET. "));
            Assert.AreNotEqual(HerSecrets.Key("being trusted"), HerSecrets.Key("being rushed"));
        }

        // ---- The cue tag -----------------------------------------------------------------------

        [Test]
        public void CueTag_CarriesSecret_HiddenFromTheText_AndJunkIsDropped()
        {
            var hit = AnimationCueParser.Parse("<animation emotion=\"surprised\" intensity=\"0.7\" landed=\"complimented\" secret=\"W1\" />\nHow did you know?");
            Assert.AreEqual(CueParseStatus.Parsed, hit.Status);
            Assert.AreEqual("w1", hit.Cue.Secret);
            Assert.AreEqual(LandingEvent.Complimented, hit.Cue.Landed);
            StringAssert.DoesNotContain("secret", hit.Text);

            Assert.AreEqual(string.Empty, AnimationCueParser.Parse("<animation emotion=\"happy\" />\nHi.").Cue.Secret);
            Assert.AreEqual(string.Empty, AnimationCueParser.Parse("<animation emotion=\"happy\" secret=\"being trusted\" />\nHi.").Cue.Secret);
            Assert.AreEqual("t2", AnimationCueParser.Parse("<animation emotion=\"happy\" secret=\"(t2)\" />\nHi.").Cue.Secret, "the brackets from the prompt are fine");

            var badFace = AnimationCueParser.Parse("<animation emotion=\"zzz\" secret=\"t1\" />\nHm.");
            Assert.AreEqual("t1", badFace.Cue.Secret, "a bad emotion does not lose the secret");
        }

        // ---- Finding them --------------------------------------------------------------------------

        [Test]
        public void Check_FirstThenAgain_AndAnUnknownIdIsNone()
        {
            var items = HerSecrets.From(Mira);
            var state = new SecretsState();
            SecretItem item;
            Assert.AreEqual(SecretHitKind.First, HerSecrets.Check(state, items, "w1", out item));
            Assert.AreEqual("being trusted with a secret", item.Text);
            Assert.AreEqual(SecretHitKind.First, HerSecrets.Check(state, items, "w1", out item), "Check changes nothing");

            Assert.IsTrue(state.Reveal(item.Key));
            Assert.IsFalse(state.Reveal(item.Key), "a find counts once");
            Assert.AreEqual(SecretHitKind.Again, HerSecrets.Check(state, items, "w1", out item));
            Assert.AreEqual(item.Key, state.NewestKey);

            Assert.AreEqual(SecretHitKind.None, HerSecrets.Check(state, items, "t9", out item));
            Assert.AreEqual(SecretHitKind.None, HerSecrets.Check(state, items, string.Empty, out item));
            Assert.AreEqual(SecretHitKind.None, HerSecrets.Check(state, null, "t1", out item));
        }

        [Test]
        public void AFind_SurvivesAReRead_ThatKeepsThePhrase()
        {
            var state = new SecretsState();
            SecretItem item;
            HerSecrets.Check(state, HerSecrets.From(Mira), "w2", out item);
            state.Reveal(item.Key);

            // A new reading reorders the list and drops another phrase: w2 is now w1, still found.
            var reread = HerSecrets.From(Landing(new[] { "being rushed" }, new[] { "Honest compliments.", "a good cup of tea" }));
            Assert.AreEqual(SecretHitKind.Again, HerSecrets.Check(state, reread, "w1", out item));
            Assert.AreEqual(1, state.FoundAmong(reread));

            // A reading without the phrase no longer counts it.
            Assert.AreEqual(0, state.FoundAmong(HerSecrets.From(Landing(new[] { "being rushed" }, new string[0]))));
        }

        [Test]
        public void FirstFind_IsACriticalHit_AndAFindAgainIsOrdinary()
        {
            var cue = new AnimationCue(Emotion.Happy, 0.5f, string.Empty, 0f, null, null, LandingEvent.Complimented, "w1");
            var first = RelationshipNow.Land(cue, 20f, 50f, true);
            var again = RelationshipNow.Land(cue, 20f, 50f, false);
            Assert.IsTrue(first.Critical);
            Assert.IsFalse(again.Critical, "intensity 0.5 is no crit on its own");
            Assert.AreEqual(again.AffectionDelta * RelationshipMath.CritMultiplier, first.AffectionDelta, 0.001f);

            var neutral = new AnimationCue(Emotion.Neutral, 0.5f, string.Empty, 0f, null, null, LandingEvent.Neutral, "w1");
            Assert.IsFalse(RelationshipNow.Land(neutral, 20f, 50f, true).Critical, "a neutral landing moves nothing, secret or not");
        }

        // ---- What it looks like ------------------------------------------------------------------

        [Test]
        public void AFind_FloatsAGoldSecretPiece_WithTheAffectionPiece()
        {
            var change = new RelationshipChange
            {
                Landing = LandingEvent.Complimented,
                Affection = 24f,
                AffectionDelta = 4f,
                Familiarity = 50f,
                Critical = true,
            };
            var feedback = new RelationshipFeedback();
            Assert.AreEqual(2, feedback.Add(RelationshipNotice.From(change, 2, "being trusted with a secret"), 0f, true));
            Assert.AreEqual(FeedbackKind.CritUp, feedback.Piece(0).Kind);
            Assert.AreEqual(FeedbackKind.Secret, feedback.Piece(1).Kind);
            Assert.AreEqual("Secret found: being trusted with a secret", feedback.Piece(1).Label);

            var none = new RelationshipFeedback();
            Assert.AreEqual(1, none.Add(RelationshipNotice.From(change, 2), 0f, true), "no secret, no secret piece");
            Assert.AreEqual(0, new RelationshipFeedback().Add(RelationshipNotice.From(change, 2, "x"), 0f, false), "nothing while the pieces are off");
        }

        [Test]
        public void Profile_ShowsFoundOnes_LocksTheRest_AndMarksTheNewest()
        {
            var items = HerSecrets.From(Mira);
            var state = new SecretsState();
            state.Reveal(items[0].Key);
            state.Reveal(items[3].Key);

            var view = ProfileSecrets.Build(items, state);
            Assert.AreEqual("Her secrets · 2 of 4 found", view.Title);
            Assert.AreEqual(4, view.Items.Count);
            Assert.IsTrue(view.Items[0].Found);
            Assert.AreEqual("being called childish", view.Items[0].Text);
            Assert.IsFalse(view.Items[0].Newest);
            Assert.IsFalse(view.Items[1].Found);
            Assert.AreEqual(ProfileSecrets.Locked, view.Items[1].Text, "a locked one never shows its text");
            Assert.IsTrue(view.Items[3].Newest);

            Assert.AreEqual(0, ProfileSecrets.Build(null, state).Total);
            Assert.AreEqual(0, ProfileSecrets.Build(items, null).Found);
        }

        [Test]
        public void PromptLine_ListsEverySecretWithItsId()
        {
            var line = RelationshipPrompt.Line(new SheetRelationship { role = "friend" }, Mira, 25f, 50f);
            StringAssert.Contains("Touchy about: (t1) being called childish, (t2) being rushed.", line);
            StringAssert.Contains("Warms to: (w1) being trusted with a secret, (w2) honest compliments.", line);

            var five = RelationshipPrompt.Line(null, Landing(new[] { "a", "b", "c", "d", "e" }, new string[0]), 0f, 50f);
            StringAssert.Contains("(t5) e.", five, "all five are listed, not only three");
            StringAssert.Contains("secret=", HerSecrets.Instruction);
            StringAssert.Contains("secret=", LandingEvents.TurnReminder, "local models follow the reminder more than the system prompt");
        }

        // ---- Saving ---------------------------------------------------------------------------------

        [Test]
        public void State_RoundTripsThroughJson_AndJunkIsEmpty()
        {
            var state = new SecretsState();
            state.Reveal("277f0a1e");
            state.Reveal("0badf00d");
            var back = SecretsState.FromJson(state.ToJson());
            Assert.AreEqual(2, back.FoundCount);
            Assert.IsTrue(back.IsFound("277f0a1e"));
            Assert.AreEqual("0badf00d", back.NewestKey);

            Assert.AreEqual(0, SecretsState.FromJson("not json").FoundCount);
            Assert.AreEqual(0, SecretsState.FromJson(null).FoundCount);
            Assert.AreEqual(string.Empty, SecretsState.FromJson("{\"version\":1,\"found\":[\"aa\"],\"newest\":\"bb\"}").NewestKey, "the newest must be one that was found");

            state.Clear();
            Assert.AreEqual(0, state.FoundCount);
            Assert.AreEqual(string.Empty, state.NewestKey);
        }

        [Test]
        public void Files_ArePerPersona_Encrypted_AndNotTheApiKeyFile()
        {
            Assert.AreEqual("her-secrets.json", Path.GetFileName(SecretsStore.FileFor("data", string.Empty)));
            StringAssert.StartsWith("her-secrets-", Path.GetFileName(SecretsStore.FileFor("data", "mira")));
            Assert.IsNull(SecretsStore.FileFor(null, "mira"));

            Assert.IsTrue(LocalDataInventory.IsHerSecretsName("her-secrets.json"));
            Assert.IsTrue(LocalDataInventory.IsHerSecretsName("her-secrets-mira.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("her-secrets-mira.json"));
            Assert.IsFalse(LocalDataInventory.IsHerSecretsName("secrets.json"));
            Assert.IsTrue(LocalDataInventory.IsSecretFile("secrets.json"), "the API key file is unchanged");
            Assert.IsFalse(LocalDataInventory.IsSecretFile("her-secrets.json"));
        }
    }
}
