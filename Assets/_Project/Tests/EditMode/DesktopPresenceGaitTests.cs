using AICompanion.Overlay;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-99. The optional gait on the desktop presence: a walking speed that follows a
    /// video clip, and a stop that lets the clip finish its steps. With no gait set, the
    /// model must behave exactly as before.
    /// </summary>
    public class DesktopPresenceGaitTests
    {
        const float Step = 1f / 60f;

        static DesktopPresenceModel Model()
        {
            var model = new DesktopPresenceModel(480, 720, 7, wanderByIdle: false);
            model.SetWorkArea(new DesktopWorkArea(0, 0, 4000, 1080));
            model.PlaceAt(1500, model.GroundY);
            return model;
        }

        static void Run(DesktopPresenceModel model, float seconds)
        {
            var steps = (int)System.Math.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
                model.Tick(Step);
        }

        [Test]
        public void WithoutAGait_AStopIsImmediate_AsBefore()
        {
            var model = Model();
            model.RequestWalk(1f, 10f);
            Run(model, 0.5f);

            model.RequestStop();

            Assert.IsFalse(model.HasGait);
            Assert.AreEqual(PresenceState.Idle, model.State);
            Assert.AreEqual(PresenceStopPhase.None, model.StopPhase);
        }

        [Test]
        public void WithoutAGait_TheWalkUsesTheConstructorSpeed()
        {
            var model = Model();
            model.RequestWalk(1f, 10f);
            Run(model, 1f);

            Assert.AreEqual(1500f + DesktopPresenceModel.DefaultWalkSpeed, model.X, 2f);
        }

        [Test]
        public void AGaitSetsTheWalkingSpeed()
        {
            var model = Model();
            model.SetGait(200f, 0f, 0f);
            model.RequestWalk(-1f, 10f);
            Run(model, 1f);

            Assert.IsTrue(model.HasGait);
            Assert.AreEqual(1300f, model.X, 4f);
            Assert.AreEqual(200f, model.CurrentSpeed, 0.01f);
        }

        [Test]
        public void AGaitOfZero_KeepsHerWalkingInPlace()
        {
            // The clip can show her standing at the start of the walk. The window waits.
            var model = Model();
            model.SetGait(0f, 0f, 0f);
            model.RequestWalk(1f, 10f);
            Run(model, 1f);

            Assert.IsTrue(model.HasGait);
            Assert.AreEqual(PresenceState.Walking, model.State);
            Assert.AreEqual(1500f, model.X, 0.01f);
        }

        [Test]
        public void TheSpeedCanFollowTheClipFrameByFrame()
        {
            var model = Model();
            model.RequestWalk(1f, 10f);
            model.SetGait(120f, 0f, 0f);
            Run(model, 1f);
            model.SetGait(0f, 0f, 0f);
            Run(model, 1f);
            model.SetGait(60f, 0f, 0f);
            Run(model, 1f);

            Assert.AreEqual(1500f + 120f + 0f + 60f, model.X, 4f);
        }

        [Test]
        public void AStopWithALead_KeepsWalkingForTheLeadThenStands()
        {
            var model = Model();
            model.SetGait(100f, 1f, 0f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.5f);
            var atStop = model.X;

            model.RequestStop();

            Assert.AreEqual(PresenceState.Walking, model.State);
            Assert.AreEqual(PresenceStopPhase.Lead, model.StopPhase);

            Run(model, 0.5f);
            Assert.AreEqual(PresenceState.Walking, model.State);
            Assert.AreEqual(atStop + 50f, model.X, 3f);

            Run(model, 0.6f);
            Assert.AreEqual(PresenceState.Idle, model.State);
            Assert.AreEqual(PresenceStopPhase.None, model.StopPhase);
            Assert.AreEqual(atStop + 100f, model.X, 4f);
        }

        [Test]
        public void AStopWithAGlide_SlowsDownToAHalt()
        {
            var model = Model();
            model.SetGait(100f, 0f, 1f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.5f);
            var atStop = model.X;

            model.RequestStop();
            Assert.AreEqual(PresenceStopPhase.Glide, model.StopPhase);

            Run(model, 0.5f);
            Assert.AreEqual(50f, model.CurrentSpeed, 4f);

            Run(model, 0.6f);
            Assert.AreEqual(PresenceState.Idle, model.State);
            // A straight ramp from 100 to 0 over one second covers about 50 pixels.
            Assert.AreEqual(atStop + 50f, model.X, 5f);
        }

        [Test]
        public void ALeadIsFollowedByTheGlide()
        {
            var model = Model();
            model.SetGait(100f, 0.5f, 0.5f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.2f);

            model.RequestStop();
            Assert.AreEqual(PresenceStopPhase.Lead, model.StopPhase);
            Run(model, 0.6f);
            Assert.AreEqual(PresenceStopPhase.Glide, model.StopPhase);
            Run(model, 0.6f);
            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void TheLeadInForceWhenTheStopBegins_IsTheOneUsed()
        {
            var model = Model();
            model.SetGait(100f, 1f, 0f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.2f);
            model.RequestStop();

            // The player keeps reporting a new lead each frame. It must not extend the stop.
            for (var i = 0; i < 90; i++)
            {
                model.SetGait(100f, 5f, 0f);
                model.Tick(Step);
            }

            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void ASecondStop_DoesNotRestartTheLead()
        {
            var model = Model();
            model.SetGait(100f, 1f, 0f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.2f);
            model.RequestStop();
            Run(model, 0.7f);

            model.RequestStop();
            Run(model, 0.4f);

            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void ANewWalkDuringAStop_CancelsTheStopAndTurnsHer()
        {
            var model = Model();
            model.SetGait(100f, 1f, 1f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.3f);
            model.RequestStop();
            Run(model, 0.3f);
            var before = model.X;

            model.RequestWalk(-1f, 10f);

            Assert.AreEqual(PresenceState.Walking, model.State);
            Assert.AreEqual(PresenceStopPhase.None, model.StopPhase);
            Assert.IsFalse(model.FacingRight);

            Run(model, 2f);
            Assert.AreEqual(PresenceState.Walking, model.State);
            Assert.AreEqual(before - 200f, model.X, 6f);
        }

        [Test]
        public void AWalkThatRunsOutOfTime_AlsoStopsThroughTheLead()
        {
            var model = Model();
            model.SetGait(100f, 1f, 0f);
            model.RequestWalk(1f, 0.5f);

            Run(model, 0.7f);
            Assert.AreEqual(PresenceState.Walking, model.State);
            Assert.AreEqual(PresenceStopPhase.Lead, model.StopPhase);

            Run(model, 1f);
            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void AScreenEdgeEndsTheWalkAtOnce_EvenDuringAStop()
        {
            var model = new DesktopPresenceModel(480, 720, 7, wanderByIdle: false);
            model.SetWorkArea(new DesktopWorkArea(0, 0, 1920, 1080));
            model.PlaceAt(30, model.GroundY);
            model.SetGait(100f, 5f, 0f);
            model.RequestWalk(-1f, 10f);
            Run(model, 0.1f);
            model.RequestStop();
            Assert.AreEqual(PresenceStopPhase.Lead, model.StopPhase);

            Run(model, 1f);

            Assert.AreEqual(PresenceState.Idle, model.State);
            Assert.AreEqual(PresenceStopPhase.None, model.StopPhase);
            Assert.AreEqual(0f, model.X, 0.01f);
        }

        [Test]
        public void ClearingTheGait_RestoresTheOldSpeedAndTheImmediateStop()
        {
            var model = Model();
            model.SetGait(300f, 2f, 2f);
            model.ClearGait();
            model.RequestWalk(1f, 10f);
            Run(model, 1f);

            Assert.IsFalse(model.HasGait);
            Assert.AreEqual(1500f + DesktopPresenceModel.DefaultWalkSpeed, model.X, 2f);

            model.RequestStop();
            Assert.AreEqual(PresenceState.Idle, model.State);
        }

        [Test]
        public void BadGaitNumbers_AreTreatedAsZero()
        {
            var model = Model();
            model.SetGait(float.NaN, float.PositiveInfinity, -3f);
            model.RequestWalk(1f, 10f);
            Run(model, 0.5f);

            Assert.AreEqual(1500f, model.X, 0.01f);
            model.RequestStop();
            Assert.AreEqual(PresenceState.Idle, model.State);
        }
    }
}
