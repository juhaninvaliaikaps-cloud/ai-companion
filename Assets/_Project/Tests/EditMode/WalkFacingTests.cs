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
        public void WalkingRightAndLeftTurnOppositeWays()
        {
            var right = WalkFacing.Target(1f);
            var left = WalkFacing.Target(-1f);

            Assert.AreEqual(-right, left, 0.001f);
            Assert.AreEqual(WalkFacing.TurnDegrees, System.Math.Abs(right), 0.001f);
        }

        [Test]
        public void OutOfRangeDirectionsClamp()
        {
            Assert.AreEqual(WalkFacing.Target(1f), WalkFacing.Target(4f), 0.001f);
            Assert.AreEqual(WalkFacing.Target(-1f), WalkFacing.Target(-4f), 0.001f);
        }

        [Test]
        public void NaNFacesForward()
        {
            Assert.AreEqual(0f, WalkFacing.Target(float.NaN), 0.001f);
        }

        [Test]
        public void WalkingOverridesTheSavedYaw()
        {
            Assert.AreEqual(WalkFacing.Target(1f) - 30f, WalkFacing.Offset(1f, 30f), 0.001f);
            Assert.AreEqual(WalkFacing.Target(-1f) - 30f, WalkFacing.Offset(-1f, 30f), 0.001f);
        }

        [Test]
        public void StandingKeepsTheSavedYaw()
        {
            Assert.AreEqual(0f, WalkFacing.Offset(0f, 30f), 0.001f);
        }

        [Test]
        public void NaNSavedYawIsTreatedAsZero()
        {
            Assert.AreEqual(WalkFacing.Target(1f), WalkFacing.Offset(1f, float.NaN), 0.001f);
        }
    }
}
