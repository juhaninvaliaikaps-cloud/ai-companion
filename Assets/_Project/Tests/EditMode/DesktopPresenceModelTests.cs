using AICompanion.Overlay;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class DesktopPresenceModelTests
    {
        static DesktopWorkArea Area()
        {
            return new DesktopWorkArea(0, 0, 1920, 1080);
        }

        static DesktopPresenceModel Model(int width = 480, int height = 720, int seed = 7)
        {
            return new DesktopPresenceModel(width, height, seed);
        }

        [Test]
        public void Tick_FallsAndLandsOnTheGround()
        {
            var model = Model();
            model.SetWorkArea(Area());
            model.PlaceAt(400, 100);

            Assert.AreEqual(PresenceState.Falling, model.State);

            for (var i = 0; i < 120 && model.State == PresenceState.Falling; i++)
                model.Tick(1f / 60f);

            Assert.AreEqual(PresenceState.Idle, model.State);
            Assert.AreEqual(model.GroundY, model.Y);
            Assert.AreEqual(1080 - 720, model.GroundY);
        }

        [Test]
        public void Tick_KeepsHerOnTheGroundOnceLanded()
        {
            var model = Model();
            model.SetWorkArea(Area());
            model.PlaceAt(400, 100);
            for (var i = 0; i < 120; i++)
                model.Tick(1f / 60f);

            var landedY = model.Y;
            for (var i = 0; i < 600; i++)
                model.Tick(1f / 60f);

            Assert.AreEqual(landedY, model.Y, 0.001f);
        }

        [Test]
        public void PlaceAt_AboveTheGround_StartsFalling()
        {
            var model = Model();
            model.SetWorkArea(Area());

            model.PlaceAt(100, 10);

            Assert.AreEqual(PresenceState.Falling, model.State);
        }

        [Test]
        public void SetWorkArea_LiftsHerWhenTheTaskbarGrows()
        {
            var model = Model();
            model.SetWorkArea(Area());
            model.PlaceAt(100, 0);
            for (var i = 0; i < 200; i++)
                model.Tick(1f / 60f);
            Assert.AreEqual(model.GroundY, model.Y);

            // The work area shrinks, so the ground moves up and the window must follow
            // without falling through it.
            model.SetWorkArea(new DesktopWorkArea(0, 0, 1920, 1040));

            Assert.AreEqual(1040 - 720, model.GroundY);
            Assert.AreEqual(model.GroundY, model.Y, 0.001f);
            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void SetWorkArea_DropsHerWhenTheTaskbarShrinks()
        {
            var model = Model();
            model.SetWorkArea(Area());
            model.PlaceAt(100, 0);
            for (var i = 0; i < 200; i++)
                model.Tick(1f / 60f);
            Assert.AreEqual(model.GroundY, model.Y);

            // A bigger work area lowers the ground, so she has to fall the difference.
            model.SetWorkArea(new DesktopWorkArea(0, 0, 1920, 1120));

            Assert.AreEqual(1120 - 720, model.GroundY);
            Assert.AreEqual(PresenceState.Falling, model.State);
        }

        [Test]
        public void Walking_StaysInsideTheWorkAreaEdges()
        {
            var model = Model(seed: 3);
            model.SetWorkArea(new DesktopWorkArea(0, 0, 1920, 1080));
            model.PlaceAt(700, 300);
            model.Tick(1f / 60f);

            var maxX = 1920 - 480;
            for (var i = 0; i < 6000; i++)
            {
                var frame = model.Tick(1f / 60f);
                Assert.GreaterOrEqual(frame.X, 0);
                Assert.LessOrEqual(frame.X, maxX);
            }
        }

        [Test]
        public void Tick_WalksAndTurnsInsideTheArea()
        {
            var model = Model(seed: 5);
            model.SetWorkArea(Area());
            model.PlaceAt(20, 360);

            var sawWalk = false;
            var sawLeftwards = false;
            var sawRightwards = false;
            for (var i = 0; i < 3000; i++)
            {
                var frame = model.Tick(1f / 60f);
                if (!frame.Walking)
                    continue;

                sawWalk = true;
                sawLeftwards |= !frame.FacingRight;
                sawRightwards |= frame.FacingRight;
            }

            Assert.IsTrue(sawWalk, "She should wander on her own.");
            Assert.IsTrue(sawLeftwards && sawRightwards, "Wandering should use both directions.");
        }

        [Test]
        public void Tick_IsDeterministicForTheSameSeed()
        {
            var a = Model(seed: 11);
            var b = Model(seed: 11);
            a.SetWorkArea(Area());
            b.SetWorkArea(Area());
            a.PlaceAt(300, 200);
            b.PlaceAt(300, 200);

            for (var i = 0; i < 500; i++)
            {
                var fa = a.Tick(1f / 60f);
                var fb = b.Tick(1f / 60f);
                Assert.AreEqual(fa.X, fb.X);
                Assert.AreEqual(fa.Y, fb.Y);
                Assert.AreEqual(fa.State, fb.State);
            }
        }

        [Test]
        public void Drop_FromTheGroundDoesNothing()
        {
            var model = Model();
            model.SetWorkArea(Area());
            model.PlaceAt(100, model.GroundY);

            model.Drop();

            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        static DesktopPresenceModel QuietModel()
        {
            var model = new DesktopPresenceModel(480, 720, 7, wanderByIdle: false);
            model.SetWorkArea(Area());
            return model;
        }

        [Test]
        public void WithoutWanderingSheStandsStillOnHerOwn()
        {
            var model = QuietModel();
            model.PlaceAt(400, model.GroundY);
            var start = model.X;

            for (var i = 0; i < 600; i++)
                model.Tick(1f / 60f);

            Assert.AreEqual(PresenceState.Idle, model.State);
            Assert.AreEqual(start, model.X);
        }

        [Test]
        public void ARequestedWalkMovesInTheRequestedDirection()
        {
            var model = QuietModel();
            model.PlaceAt(400, model.GroundY);

            model.RequestWalk(1f, 2f);
            model.Tick(0.5f);

            Assert.Greater(model.X, 400f);

            model.RequestStop();
            model.RequestWalk(-1f, 2f);
            var before = model.X;
            model.Tick(0.5f);

            Assert.Less(model.X, before);
        }

        [Test]
        public void ARequestedWalkEndsAfterItsDuration()
        {
            var model = QuietModel();
            model.PlaceAt(400, model.GroundY);

            model.RequestWalk(1f, 0.3f);
            for (var i = 0; i < 40; i++)
                model.Tick(1f / 60f);

            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void RequestStopEndsAWalkImmediately()
        {
            var model = QuietModel();
            model.PlaceAt(400, model.GroundY);

            model.RequestWalk(1f, 10f);
            model.Tick(0.2f);
            model.RequestStop();

            Assert.AreEqual(PresenceState.Idle, model.State);

            var x = model.X;
            model.Tick(1f);
            Assert.AreEqual(x, model.X);
        }

        [Test]
        public void ARequestedWalkIsIgnoredWhileFalling()
        {
            var model = QuietModel();
            model.PlaceAt(400, 100);

            model.RequestWalk(1f, 2f);

            Assert.AreEqual(PresenceState.Falling, model.State);
        }
    }
}
