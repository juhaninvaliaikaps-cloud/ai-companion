using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-117: scripted conversations through the real relationship math and mood state.
    /// Each test checks one JUH-115 expectation and writes the trajectory to the test log,
    /// so tuning <see cref="RelationshipMath"/> shows what changed.
    /// </summary>
    public class PisaraScenarioTests
    {
        static ScenarioResult Run(string name, SheetRelationship start, params ScenarioStep[] steps)
        {
            var result = new PisaraScenario(name, start, PersonaTemperaments.DefaultId, steps).Run();
            TestContext.Out.WriteLine(result.Report());
            return result;
        }

        static ScenarioResult Run(string name, SheetRelationship start, LandingEvent landing, int count)
        {
            return Run(name, start, PisaraScenario.Repeat(landing, count));
        }

        [Test]
        public void AStranger_IsNotWonOverByTenCompliments()
        {
            var result = Run("stranger, ten compliments", PisaraScenarios.Stranger(), LandingEvent.Complimented, 10);
            Assert.Greater(result.Last.Affection, result.First.Affection, "compliments help");
            Assert.Less(result.Last.Affection, 20f, "but she has no strong feelings yet");
        }

        [Test]
        public void TeasingLandsWell_OnlyWithSomeoneClose()
        {
            var sister = Run("stepsister, ten teases", PisaraScenarios.Stepsister(), LandingEvent.Teased, 10);
            Assert.Greater(sister.Last.Affection, sister.First.Affection, "a stepsister takes teasing as affection");

            var stranger = Run("stranger, ten teases", PisaraScenarios.Stranger(), LandingEvent.Teased, 10);
            Assert.Less(stranger.Last.Affection, stranger.First.Affection, "a stranger takes it as rude");
        }

        [Test]
        public void AnInsult_HurtsMoreFromAStranger()
        {
            var stranger = Run("stranger, one insult", PisaraScenarios.Stranger(), LandingEvent.Insulted, 1);
            var partner = Run("partner, one insult", PisaraScenarios.Partner(), LandingEvent.Insulted, 1);

            var fromStranger = stranger.Last.Affection - stranger.First.Affection;
            var fromPartner = partner.Last.Affection - partner.First.Affection;
            Assert.Less(fromStranger, 0f);
            Assert.Less(fromPartner, 0f, "it still hurts");
            Assert.Less(fromStranger, fromPartner * 2f, "at least twice as much from a stranger");
        }

        [Test]
        public void AnApology_RepairsPart_NotAll()
        {
            var result = Run("stranger, insult then apology", PisaraScenarios.Stranger(),
                new ScenarioStep(LandingEvent.Insulted), new ScenarioStep(LandingEvent.Apologized));
            var hurt = result.Rows[1].Affection;
            Assert.Less(hurt, result.First.Affection);
            Assert.Greater(result.Last.Affection, hurt, "the apology helps");
            Assert.Less(result.Last.Affection, result.First.Affection, "but does not undo it");
        }

        [Test]
        public void ARival_InsultedOften_CannotStandThem_AndStaysInRange()
        {
            var five = Run("rival, five insults", PisaraScenarios.Rival(), LandingEvent.Insulted, 5);
            Assert.AreEqual("she can't stand you right now", RelationshipCard.Feeling(five.Last.Affection));

            var many = Run("rival, forty insults", PisaraScenarios.Rival(), LandingEvent.Insulted, 40);
            Assert.GreaterOrEqual(many.Last.Affection, -100f);
        }

        [Test]
        public void Familiarity_NeverDrops_AndGrowsWithTime()
        {
            var steps = new ScenarioStep[]
            {
                new ScenarioStep(LandingEvent.Insulted),
                new ScenarioStep(LandingEvent.Dismissed),
                new ScenarioStep(LandingEvent.Neutral, 0.5f, 3600d),
                new ScenarioStep(LandingEvent.Insulted),
                new ScenarioStep(LandingEvent.Complimented, 0.5f, 3600d),
            };
            var stranger = Run("stranger, rough two hours", PisaraScenarios.Stranger(), steps);
            Assert.IsTrue(stranger.FamiliarityNeverDrops(), "familiarity never drops, even after insults");
            Assert.Greater(stranger.Last.Familiarity, stranger.First.Familiarity, "time together is time knowing each other");

            var sister = Run("stepsister, rough two hours", PisaraScenarios.Stepsister(), steps);
            Assert.IsTrue(sister.FamiliarityNeverDrops());
        }

        [Test]
        public void EarnedAffection_DoesNotDriftBack()
        {
            var steps = new ScenarioStep[8];
            for (var i = 0; i < 4; i++)
                steps[i] = new ScenarioStep(LandingEvent.Comforted);
            for (var i = 4; i < 8; i++)
                steps[i] = new ScenarioStep(LandingEvent.Neutral, 0.5f, 3600d);
            var result = Run("stranger, comforted, then four quiet hours", PisaraScenarios.Stranger(), steps);
            Assert.AreEqual(result.Rows[4].Affection, result.Last.Affection, 0.0001f, "what was earned stays");
        }

        [Test]
        public void ComplimentsFromAPartner_MakeHerMoreAffectionate_ThanFromAStranger()
        {
            var partner = Run("partner, five compliments", PisaraScenarios.Partner(), LandingEvent.Complimented, 5);
            var stranger = Run("stranger, five compliments", PisaraScenarios.Stranger(), LandingEvent.Complimented, 5);
            Assert.Greater(partner.Mood.Strength(CompanionMood.Affectionate), stranger.Mood.Strength(CompanionMood.Affectionate));
            Assert.AreEqual(0f, partner.Mood.Strength(CompanionMood.Intimate), "Intimate is never pushed from here");
        }

        [Test]
        public void Report_HasOneLinePerStep_WithReasons()
        {
            var result = Run("stranger, insult", PisaraScenarios.Stranger(), LandingEvent.Insulted, 1);
            var report = result.Report();
            StringAssert.StartsWith("Scenario: stranger, insult\n", report);
            StringAssert.Contains("An insult stung", report);
            Assert.AreEqual(result.Rows.Count + 2, report.Split('\n').Length - 1, "a title, a header and one line per row");
        }
    }
}
