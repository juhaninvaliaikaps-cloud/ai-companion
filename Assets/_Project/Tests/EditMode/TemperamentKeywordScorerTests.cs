using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-119: the offline temperament keyword scorer.</summary>
    public sealed class TemperamentKeywordScorerTests
    {
        [TestCase("She is cheerful, bubbly and playful.", PersonaTemperaments.CheerfulId)]
        [TestCase("A shy, timid girl who blushes easily.", PersonaTemperaments.ShyId)]
        [TestCase("A tsundere, sharp-tongued and secretly caring.", PersonaTemperaments.TsundereId)]
        [TestCase("Calm, gentle and patient.", PersonaTemperaments.CalmId)]
        [TestCase("Moody, unpredictable and temperamental.", PersonaTemperaments.MoodyId)]
        public void ClearPrompt_PicksTheExpectedPreset(string prompt, string expected)
        {
            var result = TemperamentKeywordScorer.Score(prompt);

            Assert.AreEqual(expected, result.BestId);
            Assert.Greater(result.Score(expected), TemperamentKeywordScorer.MinimumBestScore);
        }

        [Test]
        public void NegationWithinThreeWords_RemovesTheTrait()
        {
            var result = TemperamentKeywordScorer.Score("She is not shy and never timid.");

            Assert.AreEqual(0f, result.Score(PersonaTemperaments.ShyId), 0.0001f);
            Assert.IsTrue(result.IsBalanced);
            Assert.AreEqual(0f, TemperamentKeywordScorer.Score("She isn't shy.").Score(PersonaTemperaments.ShyId), 0.0001f);
        }

        [Test]
        public void NegationStopsAtCommasAndSentenceEnds()
        {
            Assert.AreEqual(PersonaTemperaments.ShyId, TemperamentKeywordScorer.Score("She isn't loud, she's shy.").BestId);
            Assert.AreEqual(PersonaTemperaments.CalmId, TemperamentKeywordScorer.Score("Don't worry. Calm and gentle.").BestId);
        }

        [Test]
        public void Intensity_ChangesTheScore()
        {
            var strong = TemperamentKeywordScorer.Score("She is very shy.");
            var weak = TemperamentKeywordScorer.Score("She is a bit shy.");

            Assert.Greater(strong.Score(PersonaTemperaments.ShyId), weak.Score(PersonaTemperaments.ShyId));
        }

        [Test]
        public void EmptyOrLiteralPrompt_IsBalanced()
        {
            foreach (var prompt in new[] { null, string.Empty, "   ", "A person with brown hair." })
            {
                var result = TemperamentKeywordScorer.Score(prompt);
                Assert.IsTrue(result.IsBalanced);
                Assert.AreEqual(string.Empty, result.BestId);
                Assert.AreEqual(0f, result.BestScore, 0.0001f);
            }
        }

        [Test]
        public void MixedPrompt_LeavesTwoNonZeroScores()
        {
            var result = TemperamentKeywordScorer.Score("She is cheerful but sometimes moody and unpredictable.");

            Assert.Greater(result.Score(PersonaTemperaments.CheerfulId), 0f);
            Assert.Greater(result.Score(PersonaTemperaments.MoodyId), 0f);
        }

        [Test]
        public void EveryScoreIsInRange_AndIntimateIsNeverScored()
        {
            var result = TemperamentKeywordScorer.Score("She is cheerful, shy, tsundere, calm and moody.");

            foreach (var id in PersonaTemperaments.Ids)
            {
                Assert.IsTrue(result.Scores.ContainsKey(id), id);
                var score = result.Score(id);
                Assert.GreaterOrEqual(score, 0f, id);
                Assert.LessOrEqual(score, 1f, id);
            }

            Assert.IsFalse(result.Scores.ContainsKey("intimate"));
        }
    }
}
