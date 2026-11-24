using System.Threading;
using AICompanion.AI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-183: the timeout counts from the last data, so a slow reply that keeps arriving finishes.</summary>
    public sealed class StreamTimeoutTests
    {
        [Test]
        public void AReplyThatKeepsArriving_IsNotIdle_HoweverLongItRuns()
        {
            // 300 s into a reply, with a token 2 s ago: not idle with a 120 s timeout.
            Assert.IsFalse(StreamTimeout.Idle(1300.0, 1298.0, 120f));
        }

        [Test]
        public void NothingForTheTimeout_IsIdle()
        {
            Assert.IsTrue(StreamTimeout.Idle(1120.0, 1000.0, 120f));
            Assert.IsFalse(StreamTimeout.Idle(1119.9, 1000.0, 120f));
        }

        [Test]
        public void ABadTimeout_UsesTheDefault()
        {
            Assert.IsFalse(StreamTimeout.Idle(1000.0 + ChatDefaults.TimeoutSeconds - 1.0, 1000.0, 0f));
            Assert.IsTrue(StreamTimeout.Idle(1000.0 + ChatDefaults.TimeoutSeconds, 1000.0, float.NaN));
        }

        [Test]
        public void TheHardCap_IsFifteenMinutes_OrTheTimeoutWhenLonger()
        {
            Assert.AreEqual(900, StreamTimeout.HardCap(120f));
            Assert.AreEqual(1200, StreamTimeout.HardCap(1200f));
            Assert.AreEqual(900, StreamTimeout.HardCap(-5f));
        }

        [Test]
        public void ElapsedIdle_CountsFromTheLastData()
        {
            const long hz = 1000L;
            // 2 ms since the last byte, however long the reply has already been running.
            Assert.IsFalse(StreamTimeout.Clock.ElapsedIdle(1300, 1298, hz, 120f));
            Assert.IsFalse(StreamTimeout.Clock.ElapsedIdle(1000 + 119999, 1000, hz, 120f));
            Assert.IsTrue(StreamTimeout.Clock.ElapsedIdle(1000 + 120000, 1000, hz, 120f));
            Assert.IsFalse(StreamTimeout.Clock.ElapsedIdle(500, 1000, hz, 1f), "a stamp ahead of now is not idle");
        }

        [Test]
        public void Clock_MarkOnAnotherThread_ResetsTheIdleWait()
        {
            var clock = new StreamTimeout.Clock();
            clock.MarkTicks(0);
            Assert.IsTrue(clock.Idle(1f), "tick 0 is long ago");

            using (var done = new ManualResetEventSlim(false))
            {
                var thread = new Thread(() =>
                {
                    clock.Mark();
                    done.Set();
                });
                thread.Start();
                Assert.IsTrue(done.Wait(5000));
                thread.Join();
            }

            Assert.IsFalse(clock.Idle(30f), "the timeout counts from the data that thread recorded");
        }

        [Test]
        public void Clock_MarkAndIdle_StayConsistentAcrossThreads()
        {
            var clock = new StreamTimeout.Clock();
            var stop = 0;
            var reader = new Thread(() =>
            {
                while (Volatile.Read(ref stop) == 0)
                    clock.Idle(120f);
            });
            reader.Start();
            for (var i = 0; i < 20000; i++)
                clock.Mark();
            Volatile.Write(ref stop, 1);
            reader.Join();
            Assert.IsFalse(clock.Idle(120f));
        }
    }
}
