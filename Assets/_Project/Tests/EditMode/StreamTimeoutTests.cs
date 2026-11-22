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
    }
}
