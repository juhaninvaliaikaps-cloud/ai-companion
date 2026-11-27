using AICompanion.Core;
using AICompanion.NewUI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonTemperamentLayoutTests
    {
        [TestCase(0)]
        [TestCase(8)]
        public void WidePreviewKeepsTrackBesideTheLabels(int axis)
        {
            var layout = new RibbonTemperamentLayout(470f, 1f, false);
            Assert.That(layout.Compact, Is.False);
            Assert.That(layout.TrackY(axis), Is.GreaterThanOrEqualTo(layout.LabelY(axis)));
            Assert.That(layout.TrackX + layout.Pitch * TemperamentSteps.Max, Is.LessThan(360f));
            Assert.That(layout.TrackY(axis) + layout.TickHeight, Is.LessThan(layout.Height));
        }

        [Test]
        public void CompactEditorPlacesTheFullTrackBelowEachLabel()
        {
            var layout = new RibbonTemperamentLayout(325f, 1f, true);
            Assert.That(layout.Compact, Is.True);
            Assert.That(layout.TrackY(0), Is.EqualTo(layout.LabelY(0) + layout.LabelHeight));
            Assert.That(layout.TrackY(0) + layout.TickHeight, Is.LessThan(layout.LabelY(1)));
            Assert.That(layout.TrackX + layout.TrackWidth, Is.LessThanOrEqualTo(layout.Width));
        }

        [TestCase(7, 10, false)]
        [TestCase(8, 7, false)]
        [TestCase(8, 8, true)]
        [TestCase(9, 8, false)]
        [TestCase(9, 10, true)]
        [TestCase(10, 10, true)]
        public void OnlyReachedUpperStepsArePink(int step, int reached, bool expected)
        {
            Assert.That(RibbonTemperamentLayout.IsPink(step, reached), Is.EqualTo(expected));
        }
    }
}
