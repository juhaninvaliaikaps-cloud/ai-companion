using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class WalkFacingTests
    {
        [Test]
        public void StandingStillFacesForward()
        {
            Assert.AreEqual(0f, WalkFacing.Target(0f), 0.001f);
        }

        [Test]
        public void WalkingRightTurnsOneWayAndLeftTheOther()
        {
            Assert.AreEqual(WalkFacing.TurnDegrees, WalkFacing.Target(1f), 0.001f);
            Assert.AreEqual(-WalkFacing.TurnDegrees, WalkFacing.Target(-1f), 0.001f);
        }

        [Test]
        public void OutOfRangeDirectionsClamp()
        {
            Assert.AreEqual(WalkFacing.TurnDegrees, WalkFacing.Target(4f), 0.001f);
            Assert.AreEqual(-WalkFacing.TurnDegrees, WalkFacing.Target(-4f), 0.001f);
        }

        [Test]
        public void NaNFacesForward()
        {
            Assert.AreEqual(0f, WalkFacing.Target(float.NaN), 0.001f);
        }
    }
}
