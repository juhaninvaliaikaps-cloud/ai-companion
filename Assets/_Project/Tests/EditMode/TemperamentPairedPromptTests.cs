using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-222: the prompt-level half of the paired Assertiveness/Directness chat test.
    /// These tests pin what each path can and cannot say before any model is called:
    /// the no-cached-voice path gets the manner line, while the cached-voice path gets
    /// the voice block and only Assertiveness/Confidence can add the no-stammer rule.
    /// </summary>
    public sealed class TemperamentPairedPromptTests
    {
        const int LowStep = 11;
        const int MiddleStep = 44;
        const int HighStep = 89;

        [Test]
        public void Assertiveness_LowAndHigh_DifferInMannerAndNoStammer()
        {
            var low = PersonaManner.Line(Axes(TemperamentAxes.Assertiveness, LowStep));
            var high = PersonaManner.Line(Axes(TemperamentAxes.Assertiveness, HighStep));

            StringAssert.Contains("lets others lead", low);
            StringAssert.DoesNotContain(PersonaManner.NotTimid, low);
            StringAssert.Contains("takes the lead", high);
            StringAssert.Contains(PersonaManner.NotTimid, high);

            var cachedLow = CachedVoice(Axes(TemperamentAxes.Assertiveness, LowStep));
            var cachedHigh = CachedVoice(Axes(TemperamentAxes.Assertiveness, HighStep));
            Assert.IsFalse(cachedLow.Contains(VoicePrompt.NoStammer));
            Assert.IsTrue(cachedHigh.Contains(VoicePrompt.NoStammer), "high assertiveness must keep the cached-voice no-stammer rule");
        }

        [Test]
        public void Directness_LowAndHigh_DifferInMannerButNotInCachedVoice()
        {
            var low = PersonaManner.Line(Axes(TemperamentAxes.Directness, LowStep));
            var high = PersonaManner.Line(Axes(TemperamentAxes.Directness, HighStep));

            StringAssert.Contains("speaks in hints", low);
            StringAssert.Contains("says what she means", high);
            Assert.AreNotEqual(low, high);

            // The cached VoicePrompt.Block reads Confidence and Assertiveness for
            // NoStammer; Directness has no line there. This is the expected gap.
            Assert.AreEqual(
                CachedVoice(Axes(TemperamentAxes.Directness, LowStep)),
                CachedVoice(Axes(TemperamentAxes.Directness, HighStep)));
        }

        [Test]
        public void Curiosity_IsTheNegativeControl_AndLeavesBothPathsUnchanged()
        {
            Assert.AreEqual(string.Empty, PersonaManner.Line(Axes(TemperamentAxes.Curiosity, LowStep)));
            Assert.AreEqual(string.Empty, PersonaManner.Line(Axes(TemperamentAxes.Curiosity, HighStep)));
            Assert.AreEqual(
                CachedVoice(Axes(TemperamentAxes.Curiosity, LowStep)),
                CachedVoice(Axes(TemperamentAxes.Curiosity, HighStep)));
        }

        [Test]
        public void AllOtherAxesStayAtTheMiddle_WhenOneAxisMoves()
        {
            foreach (var axis in new[]
                     {
                         TemperamentAxes.Assertiveness, TemperamentAxes.Directness, TemperamentAxes.Curiosity,
                     })
            {
                var low = Axes(axis, LowStep);
                var high = Axes(axis, HighStep);
                for (var i = 0; i < TemperamentAxes.Count; i++)
                {
                    if (i == axis)
                        continue;
                    Assert.AreEqual(MiddleStep, low.Value(i), TemperamentAxes.Names[i] + " low");
                    Assert.AreEqual(MiddleStep, high.Value(i), TemperamentAxes.Names[i] + " high");
                }
            }
        }

        static TemperamentAxes Axes(int axis, int value)
        {
            var axes = TemperamentAxes.Neutral();
            for (var i = 0; i < TemperamentAxes.Count; i++)
                axes.Set(i, MiddleStep);
            axes.Set(axis, value);
            return axes;
        }

        /// <summary>A fixed synthetic voice sheet, never real history. Only the axes argument changes.</summary>
        static string CachedVoice(TemperamentAxes axes)
        {
            var voice = new VoiceSheet
            {
                summary = "Concise, dry and a little teasing.",
                register = "Casual.",
                rhythm = "Short sentences.",
                vocabulary = "Plain words.",
                habits = new[] { "Names the next step." },
                never = new[] { "Generic small talk." },
                stranger = "Friendly, but not overly familiar yet.",
                close = "Warm and direct with someone she trusts.",
            };
            return VoicePrompt.Block(
                "Mira",
                voice,
                null,
                CompanionMood.Content,
                0f,
                40f,
                false,
                0f,
                axes);
        }
    }
}
