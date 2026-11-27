using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-202: the Settings sliders use ten whole steps without changing stored precision.</summary>
    public sealed class TemperamentStepsTests
    {
        [Test]
        public void EndpointsMapExactly()
        {
            Assert.AreEqual(1, TemperamentSteps.DisplayStep(0f));
            Assert.AreEqual(10, TemperamentSteps.DisplayStep(100f));
            Assert.AreEqual(0, TemperamentSteps.ValueForStep(1));
            Assert.AreEqual(100, TemperamentSteps.ValueForStep(10));
        }

        [Test]
        public void BaselineAndMidpointUseAwayFromZeroRounding()
        {
            Assert.AreEqual(6, TemperamentSteps.DisplayStep(50f));
            Assert.AreEqual(56, TemperamentSteps.ValueForStep(6));
        }

        [Test]
        public void ExistingElevenAndFifteenBothDisplayAsTwo()
        {
            Assert.AreEqual(2, TemperamentSteps.DisplayStep(11f));
            Assert.AreEqual(2, TemperamentSteps.DisplayStep(15f));
            Assert.IsFalse(TemperamentSteps.IsNewStep(11f, 2), "reselected step 2 must not edit");
            Assert.IsFalse(TemperamentSteps.IsNewStep(15f, 2), "reselected step 2 must not edit");
            Assert.IsTrue(TemperamentSteps.IsNewStep(11f, 3), "a different step is an edit");
        }

        [Test]
        public void EveryStepRoundTrips()
        {
            for (var step = TemperamentSteps.Min; step <= TemperamentSteps.Max; step++)
            {
                var stored = TemperamentSteps.ValueForStep(step);
                Assert.AreEqual(step, TemperamentSteps.DisplayStep(stored), "step " + step + " -> " + stored);
            }
        }

        [Test]
        public void DisplayAndStoredValuesAreMonotonic()
        {
            var previousStep = 0;
            var previousStored = -1;
            for (var value = 0; value <= 100; value++)
            {
                var step = TemperamentSteps.DisplayStep(value);
                Assert.GreaterOrEqual(step, previousStep);
                previousStep = step;
                Assert.GreaterOrEqual(TemperamentSteps.ValueForStep(step), previousStored);
                previousStored = TemperamentSteps.ValueForStep(step);
            }
        }

        [Test]
        public void StepsStayInsideTheSavedRange()
        {
            Assert.AreEqual(1, TemperamentSteps.DisplayStep(-20f));
            Assert.AreEqual(10, TemperamentSteps.DisplayStep(150f));
            Assert.AreEqual(0, TemperamentSteps.ValueForStep(-4));
            Assert.AreEqual(100, TemperamentSteps.ValueForStep(99));
        }

        [Test]
        public void LabelKeepsTheExistingHintAndNamesTheStep()
        {
            Assert.AreEqual("Curiosity 4 - 0 reacts, 100 asks",
                TemperamentSteps.Label(TemperamentAxes.Curiosity, 4));
        }
    }
}
