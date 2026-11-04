using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class IdleYawPolicyTests
    {
        [Test]
        public void TheIdleTurnIsAFullTurntable()
        {
            Assert.AreEqual(360f, IdleYawPolicy.MaxDegrees);
            Assert.IsTrue(IdleYawPolicy.AllowsFullTurn);
        }

        [Test]
        public void EveryDirectionStaysReachable()
        {
            Assert.AreEqual(0f, IdleYawPolicy.Clamp(0f), 0.0001f);
            Assert.AreEqual(90f, IdleYawPolicy.Clamp(90f), 0.0001f);
            Assert.AreEqual(-90f, IdleYawPolicy.Clamp(-90f), 0.0001f);
            // Facing away from the camera must not be clamped toward the front.
            Assert.AreEqual(180f, IdleYawPolicy.Clamp(180f), 0.0001f);
            Assert.AreEqual(-179f, IdleYawPolicy.Clamp(181f), 0.0001f);
            Assert.AreEqual(1f, IdleYawPolicy.Clamp(361f), 0.0001f);
        }

        [Test]
        public void ADragCanTurnHerAllTheWayAround()
        {
            // 720 degrees of drag (1440 pixels at 0.5 deg per pixel) returns to the start.
            var yaw = 0f;
            for (var i = 0; i < 18; i++)
                yaw = AvatarFacing.ApplyDrag(yaw, 160f);

            Assert.AreEqual(0f, yaw, 0.0001f);

            // A half turn stays a half turn instead of being clamped.
            var half = AvatarFacing.ApplyDrag(0f, 360f);
            Assert.AreEqual(180f, half, 0.0001f);
        }
    }
}
