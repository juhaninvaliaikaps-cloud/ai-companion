using System;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-185: she doesn't cool down in the middle of a scene.</summary>
    public sealed class DesireHoldTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 22, 18, 0, 0, DateTimeKind.Utc);

        [Test]
        public void WhileHolding_DesireAboveHerLevel_DoesNotFade()
        {
            // Libido 20, undressed: her level is 32. Two minutes between replies.
            Assert.AreEqual(65f, DesireMath.AfterTime(65f, 20f, 2d, 1f, true, false, true), 0.0001f);
            Assert.Less(DesireMath.AfterTime(65f, 20f, 2d, 1f, true, false, false), 63f, "without the hold she fades about 2.3");
        }

        [Test]
        public void WhileHolding_BuildingIsUnchanged()
        {
            Assert.AreEqual(DesireMath.AfterTime(10f, 50f, 30d, 1f, false, false, false), DesireMath.AfterTime(10f, 50f, 30d, 1f, false, false, true), 0.0001f);
        }

        [Test]
        public void AScene_KeepsBuilding_WithTheHold()
        {
            var state = new DesireState { Desire = 60f };
            var t = Now;
            for (var i = 0; i < 10; i++)
            {
                t = t.AddMinutes(2);
                state.Desire = DesireMath.AfterTime(state.Desire, 20f, 2d, 1f, true, false, state.Holding(t));
                state.Desire = DesireMath.AfterMoment(state.Desire, DesireHeat.Warm, 0.5f, 20f, 20f, false, null);
                state.NoteHeat(DesireHeat.Warm, t);
            }

            Assert.Greater(state.Desire, 75f, "ten warm replies two minutes apart build from 60");
        }

        [Test]
        public void TheHold_LastsTenMinutes_AndAReleaseEndsIt()
        {
            var state = new DesireState();
            Assert.IsFalse(state.Holding(Now));
            state.NoteHeat(DesireHeat.Hot, Now);
            Assert.IsTrue(state.Holding(Now.AddMinutes(9.9)));
            Assert.IsFalse(state.Holding(Now.AddMinutes(10.1)));
            state.NoteHeat(DesireHeat.Warm, Now);
            state.NoteHeat(DesireHeat.Release, Now.AddMinutes(1));
            Assert.IsFalse(state.Holding(Now.AddMinutes(2)));
            state.NoteHeat(DesireHeat.Hot, Now);
            state.NoteHeat(DesireHeat.None, Now.AddMinutes(1));
            Assert.IsTrue(state.Holding(Now.AddMinutes(9)), "a reply with no heat does not end the hold");
        }

        [Test]
        public void TheHold_IsSaved()
        {
            var state = new DesireState { Desire = 50f, LastTicks = Now.Ticks };
            state.NoteHeat(DesireHeat.Hot, Now);
            var back = DesireState.FromJson(state.ToJson());
            Assert.AreEqual(state.HeatUntilTicks, back.HeatUntilTicks);
            Assert.IsTrue(back.Holding(Now.AddMinutes(5)));
        }
    }
}
