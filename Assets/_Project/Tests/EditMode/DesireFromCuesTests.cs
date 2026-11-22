using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-179: desire builds from the cue's other signs when the model writes no heat.</summary>
    public sealed class DesireFromCuesTests
    {
        static AnimationCue Cue(string emotion, string reaction, LandingEvent landed, DesireHeat heat = DesireHeat.None)
        {
            return new AnimationCue(Emotion.Neutral, 0.8f, string.Empty, 0f, emotion, reaction, landed, null, heat);
        }

        [Test]
        public void TheModelsHeat_Wins()
        {
            Assert.AreEqual(DesireHeat.Release, DesireHeats.Of(Cue("neutral", "strip", LandingEvent.Flirted, DesireHeat.Release)));
            Assert.AreEqual(DesireHeat.Warm, DesireHeats.Of(Cue("neutral", "strip", LandingEvent.Neutral, DesireHeat.Warm)));
        }

        [Test]
        public void UndressingIsHot_AndAFlirtIsWarm_WhenTheModelWroteNoHeat()
        {
            // Jussi's log, 2026-11-22: <animation emotion="flustered_heavy" reaction="strip" secret="k1" />, no heat.
            Assert.AreEqual(DesireHeat.Hot, DesireHeats.Of(Cue("flustered_heavy", "strip", LandingEvent.Neutral)));
            Assert.AreEqual(DesireHeat.Warm, DesireHeats.Of(Cue("flustered_heavy", null, LandingEvent.Flirted)));
            Assert.AreEqual(DesireHeat.None, DesireHeats.Of(Cue("surprised", null, LandingEvent.Neutral)));
            Assert.AreEqual(DesireHeat.None, DesireHeats.Of(Cue("happy", null, LandingEvent.Complimented)), "a compliment is not a sexual moment");
        }

        [Test]
        public void AStripRaisesDesire_WithoutAHeatTag()
        {
            var heat = DesireHeats.Of(Cue("flustered_heavy", "strip", LandingEvent.Neutral));
            var after = DesireMath.AfterMoment(30f, heat, 0.95f, 20f, -7.3f, false, IntimateSecretGroup.Kink);
            var secretOnly = DesireMath.AfterMoment(30f, DesireHeat.None, 0.95f, 20f, -7.3f, false, IntimateSecretGroup.Kink);
            Assert.Greater(after, secretOnly + 5f, "the strip counts as hot, on top of the kink");
        }

        [Test]
        public void AClimaxClipOrExpression_IsARelease_WithoutTheTag()
        {
            Assert.AreEqual(DesireHeat.Release, DesireHeats.Of(Cue("neutral", "climax_shiver", LandingEvent.Neutral)));
            Assert.AreEqual(DesireHeat.Release, DesireHeats.Of(Cue("x_orgasm", null, LandingEvent.Neutral)));
            Assert.AreEqual(9f, DesireMath.AfterRelease(60f), 0.001f, "a release keeps 15 %");
        }

        [Test]
        public void AFulfilledKink_RelievesHerALittle_ATeasedOneArousesHer()
        {
            Assert.IsTrue(DesireMath.Fulfils(60f, DesireHeat.Hot, IntimateSecretGroup.Kink), "aroused, and the kink is done");
            Assert.IsFalse(DesireMath.Fulfils(30f, DesireHeat.Hot, IntimateSecretGroup.Kink), "only warm: it still arouses her");
            Assert.IsFalse(DesireMath.Fulfils(60f, DesireHeat.Warm, IntimateSecretGroup.Kink), "only talked about");
            Assert.IsFalse(DesireMath.Fulfils(60f, DesireHeat.Hot, IntimateSecretGroup.Sensitive), "a sensitive spot arouses");
            Assert.IsFalse(DesireMath.Fulfils(60f, DesireHeat.Hot, null));
            Assert.AreEqual(51f, DesireMath.AfterFulfilled(60f), 0.001f);
            Assert.Less(DesireMath.AfterFulfilled(60f), 60f);
        }

        [Test]
        public void TheTurnReminder_AsksForHeat()
        {
            var reminder = DesireHeats.WithReminder(LandingEvents.TurnReminder);
            StringAssert.Contains("heat=\"hot\"", reminder);
            StringAssert.Contains("landed", reminder, "the landing request stays");
            StringAssert.EndsWith(")", reminder);
            StringAssert.EndsWith(DesireHeats.Reminder, DesireHeats.WithReminder("no parenthesis"));
        }
    }
}
