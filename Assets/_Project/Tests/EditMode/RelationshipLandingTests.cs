using System.Collections.Generic;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-117 / JUH-115: how a landed message changes the relationship. The same event lands
    /// differently by familiarity and standing, familiarity only grows, negative affection stays
    /// love-hate, and Intimate is never pushed from here.
    /// </summary>
    public class RelationshipLandingTests
    {
        const float Mid = 0.5f;

        static float Delta(LandingEvent landing, float affection, float familiarity, float intensity = Mid)
        {
            return RelationshipMath.Land(landing, intensity, affection, familiarity).AffectionDelta;
        }

        static float Gain(RelationshipChange change, CompanionMood mood)
        {
            for (var i = 0; i < change.MoodPushes.Length; i++)
            {
                if (change.MoodPushes[i].Mood == mood)
                    return change.MoodPushes[i].Gain;
            }

            return 0f;
        }

        // ---- the landed word -----------------------------------------------------------------

        [TestCase("complimented", LandingEvent.Complimented)]
        [TestCase(" Praised ", LandingEvent.Complimented)]
        [TestCase("thanked", LandingEvent.Thanked)]
        [TestCase("comforted", LandingEvent.Comforted)]
        [TestCase("teased", LandingEvent.Teased)]
        [TestCase("INSULTED", LandingEvent.Insulted)]
        [TestCase("hurt", LandingEvent.Insulted)]
        [TestCase("dismissed", LandingEvent.Dismissed)]
        [TestCase("ignored", LandingEvent.Dismissed)]
        [TestCase("apologized", LandingEvent.Apologized)]
        [TestCase("apologised to", LandingEvent.Apologized)]
        [TestCase("flirted", LandingEvent.Flirted)]
        [TestCase("neutral", LandingEvent.Neutral)]
        [TestCase("banana", LandingEvent.Neutral)]
        [TestCase("", LandingEvent.Neutral)]
        [TestCase(null, LandingEvent.Neutral)]
        public void Parse_ReadsTheWord_AndUnknownIsNeutral(string word, LandingEvent expected)
        {
            Assert.AreEqual(expected, LandingEvents.Parse(word));
        }

        [Test]
        public void Instruction_NamesEveryWord_AndEveryNameParsesBack()
        {
            foreach (LandingEvent landing in System.Enum.GetValues(typeof(LandingEvent)))
            {
                var name = LandingEvents.Name(landing);
                StringAssert.Contains(name, LandingEvents.Instruction);
                Assert.AreEqual(landing, LandingEvents.Parse(name));
            }

            StringAssert.Contains("landed=", LandingEvents.Instruction);
        }

        [Test]
        public void CueTag_CarriesLanded_AndABadWordNeverBreaksTheTag()
        {
            var teased = AnimationCueParser.Parse("<animation emotion=\"happy\" intensity=\"0.6\" landed=\"teased\" />\nOh, you.");
            Assert.AreEqual(CueParseStatus.Parsed, teased.Status);
            Assert.AreEqual(LandingEvent.Teased, teased.Cue.Landed);
            Assert.AreEqual(Emotion.Happy, teased.Cue.Emotion);
            StringAssert.DoesNotContain("landed", teased.Text);

            var none = AnimationCueParser.Parse("<animation emotion=\"happy\" intensity=\"0.6\" />\nHi.");
            Assert.AreEqual(LandingEvent.Neutral, none.Cue.Landed);

            var odd = AnimationCueParser.Parse("<animation emotion=\"sad\" intensity=\"0.5\" landed=\"banana\" />\nHm.");
            Assert.AreEqual(CueParseStatus.Parsed, odd.Status);
            Assert.AreEqual(LandingEvent.Neutral, odd.Cue.Landed);
            Assert.AreEqual(Emotion.Sad, odd.Cue.Emotion);

            // A tag whose emotion cannot be read still says how the message landed.
            var badFace = AnimationCueParser.Parse("<animation emotion=\"zzz\" landed=\"insulted\" />\nWow.");
            Assert.AreEqual(LandingEvent.Insulted, badFace.Cue.Landed);

            Assert.AreEqual(LandingEvent.Neutral, AnimationCueParser.NeutralCue.Landed);
        }

        // ---- JUH-115: the same words land differently ---------------------------------------

        [Test]
        public void Insult_CostsMostFromAStranger_AndLeastFromSomeoneClose()
        {
            Assert.AreEqual(-10f, Delta(LandingEvent.Insulted, 0f, 0f), 0.001f);
            Assert.AreEqual(-3f, Delta(LandingEvent.Insulted, 0f, 100f), 0.001f);
            Assert.Less(Delta(LandingEvent.Insulted, 0f, 0f), Delta(LandingEvent.Insulted, 0f, 60f));
            Assert.Less(Delta(LandingEvent.Insulted, 0f, 60f), Delta(LandingEvent.Insulted, 0f, 100f));
        }

        [Test]
        public void Compliment_StrangerIsSuspicious_FriendIsPleased_LovedOneMost()
        {
            var stranger = RelationshipMath.Land(LandingEvent.Complimented, Mid, 0f, 0f);
            var friend = RelationshipMath.Land(LandingEvent.Complimented, Mid, 0f, 50f);
            var loved = RelationshipMath.Land(LandingEvent.Complimented, Mid, 0f, 100f);

            Assert.AreEqual(1f, stranger.AffectionDelta, 0.001f);
            Assert.AreEqual(2.5f, friend.AffectionDelta, 0.001f);
            Assert.AreEqual(4f, loved.AffectionDelta, 0.001f);

            Assert.Greater(Gain(stranger, CompanionMood.Anxious), 0f, "a stranger's compliment raises suspicion");
            Assert.AreEqual(0f, Gain(stranger, CompanionMood.Affectionate));
            Assert.AreEqual(0f, Gain(loved, CompanionMood.Anxious));
            Assert.AreEqual(0.1f, Gain(loved, CompanionMood.Affectionate), 0.0001f);
            Assert.Greater(Gain(friend, CompanionMood.Happy), 0f);
            Assert.Greater(Gain(friend, CompanionMood.Anxious), 0f);
            Assert.Less(Gain(friend, CompanionMood.Anxious), Gain(stranger, CompanionMood.Anxious));
        }

        [Test]
        public void Tease_ChangesSign_AndBeingDismissedHurtsMoreWhenClose()
        {
            Assert.AreEqual(-2f, Delta(LandingEvent.Teased, 0f, 0f), 0.001f);
            Assert.AreEqual(0f, Delta(LandingEvent.Teased, 0f, 50f), 0.001f);
            Assert.AreEqual(2f, Delta(LandingEvent.Teased, 0f, 100f), 0.001f);

            Assert.AreEqual(-3f, Delta(LandingEvent.Dismissed, 0f, 0f), 0.001f);
            Assert.AreEqual(-6f, Delta(LandingEvent.Dismissed, 0f, 100f), 0.001f);
            Assert.Greater(Gain(RelationshipMath.Land(LandingEvent.Dismissed, Mid, 0f, 100f), CompanionMood.Sad), 0f);
        }

        [Test]
        public void Standing_ShadesKindnessAndSlights()
        {
            // Kindness: diminishing near the top, weaker while she dislikes them.
            Assert.AreEqual(4f, Delta(LandingEvent.Complimented, 0f, 100f), 0.001f);
            Assert.AreEqual(3f, Delta(LandingEvent.Complimented, 50f, 100f), 0.001f);
            Assert.AreEqual(3f, Delta(LandingEvent.Complimented, -50f, 100f), 0.001f);
            Assert.AreEqual(0.5f, RelationshipMath.Standing(1f, 100f), 0.0001f);
            Assert.AreEqual(0.5f, RelationshipMath.Standing(1f, -100f), 0.0001f);

            // Slights: forgiven when she likes them, sharper when she does not.
            Assert.AreEqual(0.6f, RelationshipMath.Standing(-1f, 100f), 0.0001f);
            Assert.AreEqual(1.3f, RelationshipMath.Standing(-1f, -100f), 0.0001f);
            Assert.Greater(Delta(LandingEvent.Insulted, 80f, 50f), Delta(LandingEvent.Insulted, 0f, 50f));
            Assert.Less(Delta(LandingEvent.Insulted, -80f, 50f), Delta(LandingEvent.Insulted, 0f, 50f));
            Assert.AreEqual(1f, RelationshipMath.Standing(0f, 70f));
        }

        [Test]
        public void Intensity_ScalesFromHalfToOneAndAHalf()
        {
            Assert.AreEqual(-5f, Delta(LandingEvent.Insulted, 0f, 0f, 0f), 0.001f);
            Assert.AreEqual(-10f, Delta(LandingEvent.Insulted, 0f, 0f, 0.5f), 0.001f);
            Assert.AreEqual(-15f, Delta(LandingEvent.Insulted, 0f, 0f, 1f), 0.001f);
            Assert.AreEqual(-15f, Delta(LandingEvent.Insulted, 0f, 0f, 9f), 0.001f);
            Assert.AreEqual(-5f, Delta(LandingEvent.Insulted, 0f, 0f, float.NaN), 0.001f);
        }

        [Test]
        public void Neutral_ChangesNothingButFamiliarity_AndBadInputIsSafe()
        {
            var neutral = RelationshipMath.Land(LandingEvent.Neutral, Mid, 12f, 0f);
            Assert.AreEqual(0f, neutral.AffectionDelta);
            Assert.AreEqual(12f, neutral.Affection);
            Assert.AreEqual(0.1f, neutral.FamiliarityDelta, 0.0001f);
            Assert.AreEqual(0, neutral.MoodPushes.Length);

            var unknown = RelationshipMath.Land((LandingEvent)99, Mid, float.NaN, float.NaN);
            Assert.AreEqual(LandingEvent.Neutral, unknown.Landing);
            Assert.AreEqual(0f, unknown.Affection);
            Assert.IsNotNull(unknown.MoodPushes);
        }

        [Test]
        public void Results_StayInRange()
        {
            var low = RelationshipMath.Land(LandingEvent.Insulted, 1f, -99f, 0f);
            Assert.AreEqual(-100f, low.Affection);
            Assert.AreEqual(-1f, low.AffectionDelta, 0.001f);

            var high = RelationshipMath.Land(LandingEvent.Comforted, 1f, 99.5f, 100f);
            Assert.AreEqual(100f, high.Affection);
            Assert.AreEqual(100f, high.Familiarity);
            Assert.AreEqual(0f, high.FamiliarityDelta);

            foreach (LandingEvent landing in System.Enum.GetValues(typeof(LandingEvent)))
            {
                foreach (var affection in new[] { -100f, -40f, 0f, 40f, 100f })
                {
                    foreach (var familiarity in new[] { 0f, 30f, 70f, 100f })
                    {
                        var change = RelationshipMath.Land(landing, 1f, affection, familiarity);
                        Assert.GreaterOrEqual(change.Affection, -100f);
                        Assert.LessOrEqual(change.Affection, 100f);
                        Assert.GreaterOrEqual(change.FamiliarityDelta, 0f, "familiarity only grows");
                        for (var i = 0; i < change.MoodPushes.Length; i++)
                        {
                            Assert.AreNotEqual(CompanionMood.Intimate, change.MoodPushes[i].Mood);
                            Assert.Greater(change.MoodPushes[i].Gain, 0f);
                            Assert.LessOrEqual(change.MoodPushes[i].Gain, 0.3f);
                        }
                    }
                }
            }
        }

        [Test]
        public void NegativeAffection_IsLoveHate_NotAClosedDoor()
        {
            // She is at odds with someone she knows well: flirting and an apology still land well.
            Assert.Greater(Delta(LandingEvent.Flirted, -50f, 80f), 0f);
            Assert.Greater(Delta(LandingEvent.Apologized, -50f, 80f), 0f);
            Assert.Greater(Delta(LandingEvent.Comforted, -90f, 80f), 0f);
            // From a stranger, flirting is a little unwelcome.
            Assert.Less(Delta(LandingEvent.Flirted, 0f, 0f), 0f);
        }

        // ---- familiarity ---------------------------------------------------------------------

        [Test]
        public void Familiarity_GrowsWithTime_SlowsNearTheTop_AndNeverDrops()
        {
            Assert.AreEqual(0f, RelationshipMath.FamiliarityAfter(0f, 0d));
            Assert.AreEqual(50f, RelationshipMath.FamiliarityAfter(50f, -5d));
            Assert.AreEqual(50f, RelationshipMath.FamiliarityAfter(50f, double.NaN));
            Assert.AreEqual(63.21f, RelationshipMath.FamiliarityAfter(0f, RelationshipMath.FamiliaritySeconds), 0.01f);
            Assert.AreEqual(81.61f, RelationshipMath.FamiliarityAfter(50f, RelationshipMath.FamiliaritySeconds), 0.01f);
            Assert.AreEqual(100f, RelationshipMath.FamiliarityAfter(100f, 1e9));
            Assert.Less(RelationshipMath.FamiliarityAfter(0f, 3600d), 5f, "one hour is under 5 points");

            // Adding time in steps matches adding it at once.
            var stepped = 0f;
            for (var i = 0; i < 100; i++)
                stepped = RelationshipMath.FamiliarityAfter(stepped, 720d);
            Assert.AreEqual(RelationshipMath.FamiliarityAfter(0f, 72000d), stepped, 0.05f);
        }

        // ---- the start, from the sheet -------------------------------------------------------

        [Test]
        public void Start_KeepsTheSheetsNumbers_InsideWhatTheRoleAllows()
        {
            float affection, familiarity;
            RelationshipMath.Start(null, out affection, out familiarity);
            Assert.AreEqual(0f, affection);
            Assert.AreEqual(0f, familiarity);

            RelationshipMath.Start(new SheetRelationship { role = "family", affection = 25f, familiarity = 0f }, out affection, out familiarity);
            Assert.AreEqual(25f, affection);
            Assert.AreEqual(60f, familiarity, "family cannot start as strangers");

            RelationshipMath.Start(new SheetRelationship { role = "stranger", affection = 10f, familiarity = 80f }, out affection, out familiarity);
            Assert.AreEqual(RelationshipMath.StrangerMaxFamiliarity, familiarity);

            RelationshipMath.Start(new SheetRelationship { role = "partner", affection = 500f, familiarity = 90f }, out affection, out familiarity);
            Assert.AreEqual(100f, affection);
            Assert.AreEqual(90f, familiarity);

            RelationshipMath.Start(new SheetRelationship { role = "rival", affection = -40f, familiarity = 5f }, out affection, out familiarity);
            Assert.AreEqual(-40f, affection);
            Assert.AreEqual(25f, familiarity);

            foreach (var role in RelationshipRoles.All)
                Assert.LessOrEqual(RelationshipMath.RoleFamiliarityFloor(role), 100f);
            Assert.AreEqual(0f, RelationshipMath.RoleFamiliarityFloor("nonsense"));
        }

        // ---- mood ----------------------------------------------------------------------------

        [Test]
        public void PushMoods_GoesThroughTheMoodState_SoTemperamentScalesIt()
        {
            var change = RelationshipMath.Land(LandingEvent.Complimented, Mid, 0f, 100f);

            var balanced = new CompanionMoodState();
            RelationshipMath.PushMoods(balanced, change, Mid);
            Assert.AreEqual(0.1f, balanced.Strength(CompanionMood.Affectionate), 0.0001f);
            Assert.AreEqual(0.1f, balanced.Strength(CompanionMood.Happy), 0.0001f);

            var tsundere = new CompanionMoodState { Temperament = PersonaTemperaments.Get(PersonaTemperaments.TsundereId) };
            RelationshipMath.PushMoods(tsundere, change, Mid);
            Assert.AreEqual(0.07f, tsundere.Strength(CompanionMood.Affectionate), 0.0001f);

            Assert.IsFalse(RelationshipMath.PushMoods(null, change, Mid));
            Assert.IsFalse(RelationshipMath.PushMoods(balanced, default(RelationshipChange), Mid));
        }

        // ---- the prompt line and the log -----------------------------------------------------

        [Test]
        public void PromptLine_DescribesTheStanding_OnOneLine()
        {
            var sheet = new SheetRelationship { role = "family", label = "stepsister" };
            var landing = new SheetLanding
            {
                touchy = new List<string> { "being called childish" },
                winsHerOver = new List<string> { "being trusted with a secret." },
            };

            Assert.AreEqual(
                "Your relationship with the user: stepsister (family); you like them; you know each other very well. " +
                "Touchy about: being called childish. Warms to: being trusted with a secret.",
                RelationshipPrompt.Line(sheet, landing, 25f, 70f));

            Assert.AreEqual(
                "Your relationship with the user: stranger; you have no strong feelings about them yet; you have only just met. " +
                "Be a little guarded; trust is earned.",
                RelationshipPrompt.Line(null, null, 0f, 0f));

            var atOdds = RelationshipPrompt.Line(sheet, null, -45f, 70f);
            StringAssert.Contains("you are at odds with them", atOdds);
            StringAssert.Contains("love-hate", atOdds);
            StringAssert.Contains("never fear", atOdds);
            StringAssert.Contains("you can't stand them right now", RelationshipPrompt.Line(sheet, null, -80f, 70f));
            StringAssert.Contains("you adore them", RelationshipPrompt.Line(sheet, null, 80f, 70f));
            StringAssert.Contains("close friend;", RelationshipPrompt.Line(new SheetRelationship { role = "close_friend", label = "Close Friend" }, null, 30f, 40f));

            foreach (var affection in new[] { -100f, -30f, 0f, 30f, 100f })
            {
                var line = RelationshipPrompt.Line(sheet, landing, affection, 50f);
                Assert.IsFalse(line.Contains("\n"));
                StringAssert.DoesNotContain("intimate", line.ToLowerInvariant());
            }

            Assert.AreEqual("Persona\n" + RelationshipPrompt.Line(sheet, landing, 25f, 70f), RelationshipPrompt.Append("Persona  ", sheet, landing, 25f, 70f));
        }

        [Test]
        public void LogLine_NamesTheEventAndTheNewNumbers()
        {
            var change = RelationshipMath.Land(LandingEvent.Insulted, Mid, 25f, 70f);
            // raw -10 + 7 * 0.7 = -5.1; standing 1 - 0.4 * 0.25 = 0.9
            Assert.AreEqual(-4.59f, change.AffectionDelta, 0.001f);
            StringAssert.StartsWith("Relationship: insulted → affection -4.6 (now 20.4), familiarity ", change.LogLine());
        }
    }
}
