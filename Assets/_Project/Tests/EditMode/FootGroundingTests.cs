using AICompanion.Overlay;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class FootOffsetMathTests
    {
        [Test]
        public void Smooth_CoversHalfTheDistanceInOneHalfLife()
        {
            var value = FootOffsetMath.Smooth(0f, 100f, 1f, 1f);

            Assert.AreEqual(50f, value, 1f);
        }

        [Test]
        public void Smooth_DoesNotDependOnTheStepCount()
        {
            var coarse = FootOffsetMath.Smooth(0f, 100f, 1f, 0.5f);

            var fine = 0f;
            for (var i = 0; i < 10; i++)
                fine = FootOffsetMath.Smooth(fine, 100f, 0.1f, 0.5f);

            Assert.AreEqual(coarse, fine, 2f);
        }

        [Test]
        public void Smooth_KeepsTheCurrentValueWhenDeltaIsZero()
        {
            Assert.AreEqual(12f, FootOffsetMath.Smooth(12f, 100f, 0f, 0.35f));
        }

        [Test]
        public void ClampOffset_KeepsTheOffsetInsideTheWindow()
        {
            Assert.AreEqual(0f, FootOffsetMath.ClampOffset(-8f, 720));
            Assert.AreEqual(719f, FootOffsetMath.ClampOffset(5000f, 720));
            Assert.AreEqual(40f, FootOffsetMath.ClampOffset(40f, 720));
            Assert.AreEqual(0f, FootOffsetMath.ClampOffset(40f, 1));
        }
    }

    public class FootGroundingModelTests
    {
        static readonly DesktopWorkArea Area = new DesktopWorkArea(0, 0, 1920, 1080);

        static DesktopPresenceModel Model()
        {
            return new DesktopPresenceModel(480, 720, seed: 7);
        }

        [Test]
        public void SetFootOffset_MovesTheGroundLineDownByTheOffset()
        {
            var model = Model();
            model.SetWorkArea(Area);

            Assert.AreEqual(1080 - 720, model.GroundY);
            model.SetFootOffset(40f);
            Assert.AreEqual(1080 - 720 + 40, model.GroundY);
        }

        [Test]
        public void SetFootOffset_LandsHerWithTheSolesOnTheLine()
        {
            var model = Model();
            model.SetWorkArea(Area);
            model.SetFootOffset(40f);

            model.PlaceAt(300, 100);
            for (var i = 0; i < 200 && model.State == PresenceState.Falling; i++)
                model.Tick(1f / 60f);

            Assert.AreEqual(model.GroundY, model.Y, 0.001f);
            Assert.AreEqual(400, model.Y, 0.001f);
        }

        [Test]
        public void SetFootOffset_WhileStanding_MovesHerWithTheLine()
        {
            var model = Model();
            model.SetWorkArea(Area);
            model.PlaceAt(300, model.GroundY);
            Assert.AreEqual(PresenceState.Idle, model.State);

            model.SetFootOffset(25f);

            Assert.AreEqual(model.GroundY, model.Y, 0.001f);
            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void SetFootOffset_IsClampedToTheWindowHeight()
        {
            var model = Model();
            model.SetWorkArea(Area);

            model.SetFootOffset(5000f);
            Assert.AreEqual(model.WindowHeight - 1, (int)model.FootOffsetPx);

            model.SetFootOffset(-20f);
            Assert.AreEqual(0f, model.FootOffsetPx, 0.001f);
        }
    }
}
