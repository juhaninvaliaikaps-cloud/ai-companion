using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-224: Assertiveness and Directness must survive the cached-voice path. The old
    /// manner line is skipped when a cached voice block exists, so the new directive is
    /// appended after that block and carries the same two controls into both paths.
    /// </summary>
    public sealed class PersonaMannerDirectiveTests
    {
        [TearDown]
        public void TearDown()
        {
            PersonaMannerNow.Reset();
        }

        [Test]
        public void Assertiveness_LowLetsTheUserLead_HighOffersAConcreteStep()
        {
            var low = PersonaManner.Directive(Axes(TemperamentAxes.Assertiveness, 11));
            var high = PersonaManner.Directive(Axes(TemperamentAxes.Assertiveness, 89));

            StringAssert.Contains("Let the user name the next step", low);
            StringAssert.DoesNotContain("Offer one concrete next step", low);
            StringAssert.Contains("Offer one concrete next step", high);
            StringAssert.DoesNotContain("Let the user name the next step", high);
        }

        [Test]
        public void Directness_LowHints_HighStatesTheTakeEarly()
        {
            var low = PersonaManner.Directive(Axes(TemperamentAxes.Directness, 11));
            var high = PersonaManner.Directive(Axes(TemperamentAxes.Directness, 89));

            StringAssert.Contains("Hint at your take", low);
            StringAssert.DoesNotContain("State your take plainly", low);
            StringAssert.Contains("State your take plainly and early", high);
            StringAssert.DoesNotContain("Hint at your take", high);
        }

        [Test]
        public void MiddleValues_AndCuriosity_ProduceNoDirective()
        {
            Assert.AreEqual(string.Empty, PersonaManner.Directive(Axes(TemperamentAxes.Assertiveness, 44)));
            Assert.AreEqual(string.Empty, PersonaManner.Directive(Axes(TemperamentAxes.Directness, 44)));
            Assert.AreEqual(string.Empty, PersonaManner.Directive(Axes(TemperamentAxes.Curiosity, 89)));
        }

        [Test]
        public void Directive_IsAppendedAfterTheCachedVoiceBlock()
        {
            PersonaMannerNow.Set(Axes(TemperamentAxes.Assertiveness, 89));
            Assert.IsNotEmpty(PersonaMannerNow.PromptLine);
            var prompt = PersonaMannerNow.AppendDirective("base\nHow she talks: cached voice");
            Assert.IsTrue(prompt.StartsWith("base\nHow she talks: cached voice"), prompt);
            Assert.IsTrue(prompt.EndsWith(PersonaMannerNow.DirectiveLine), prompt);
            StringAssert.Contains("Offer one concrete next step", prompt);
        }

        [Test]
        public void NoCachedVoice_StillGetsBothTheMannerAndTheDirective()
        {
            PersonaMannerNow.Set(Axes(TemperamentAxes.Directness, 89));
            var prompt = PersonaMannerNow.Append("base");
            prompt = PersonaMannerNow.AppendDirective(prompt);
            StringAssert.Contains("says what she means", prompt);
            StringAssert.Contains("State your take plainly and early", prompt);
        }

        [Test]
        public void Reset_ClearsBothLines()
        {
            PersonaMannerNow.Set(Axes(TemperamentAxes.Assertiveness, 89));
            Assert.IsNotEmpty(PersonaMannerNow.PromptLine);
            Assert.IsNotEmpty(PersonaMannerNow.DirectiveLine);
            PersonaMannerNow.Reset();
            Assert.AreEqual(string.Empty, PersonaMannerNow.PromptLine);
            Assert.AreEqual(string.Empty, PersonaMannerNow.DirectiveLine);
            Assert.AreEqual("base", PersonaMannerNow.AppendDirective("base"));
        }

        static TemperamentAxes Axes(int axis, int value)
        {
            var axes = TemperamentAxes.Neutral();
            for (var i = 0; i < TemperamentAxes.Count; i++)
                axes.Set(i, 44);
            axes.Set(axis, value);
            return axes;
        }
    }
}
