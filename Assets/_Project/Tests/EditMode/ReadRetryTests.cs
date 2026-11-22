using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-176: a reading that failed while the model was not answering yet is tried again later.</summary>
    public sealed class ReadRetryTests
    {
        [Test]
        public void AFailureThatCanPass_IsTriedAgainAfterAWait()
        {
            var retry = new ReadRetry();
            Assert.IsTrue(retry.Ready("k", 0f));
            retry.Started("k");
            Assert.IsFalse(retry.Ready("k", 0f), "not while it runs");
            retry.Failed("k", 10f, ReadRetry.IsPassing("the model did not answer (Unreachable)"));
            Assert.IsTrue(retry.AnyWaiting);
            Assert.IsFalse(retry.Ready("k", 39f));
            Assert.IsTrue(retry.Ready("k", 40f), "30 s after the first failure");

            retry.Started("k");
            retry.Failed("k", 100f, true);
            Assert.IsFalse(retry.Ready("k", 159f));
            Assert.IsTrue(retry.Ready("k", 160f), "60 s after the second");
        }

        [Test]
        public void ARefusal_OrASuccess_EndsItForThisLaunch()
        {
            var retry = new ReadRetry();
            retry.Started("gate");
            retry.Failed("gate", 0f, ReadRetry.IsPassing("the 18+ gate is closed"));
            Assert.IsFalse(retry.Ready("gate", 9999f));

            retry.Started("ok");
            retry.Succeeded("ok");
            Assert.IsFalse(retry.Ready("ok", 9999f));
            Assert.IsFalse(retry.AnyWaiting);

            retry.Clear();
            Assert.IsTrue(retry.Ready("gate", 0f), "Read again or a wipe starts over");
        }

        [Test]
        public void ItGivesUpAfterTheLastTry()
        {
            var retry = new ReadRetry();
            var now = 0f;
            for (var i = 0; i < ReadRetry.MaxAttempts; i++)
            {
                Assert.IsTrue(retry.Ready("k", now), "try " + (i + 1));
                retry.Started("k");
                retry.Failed("k", now, true);
                now += 1000f;
            }

            Assert.IsFalse(retry.Ready("k", now + 10000f));
            Assert.AreEqual(ReadRetry.MaxAttempts, retry.AttemptsFor("k"));
        }

        [Test]
        public void OnlyFailuresThatCanPassByThemselves_AreRetried()
        {
            Assert.IsTrue(ReadRetry.IsPassing("the model did not answer (Unreachable)"));
            Assert.IsTrue(ReadRetry.IsPassing("the model took too long"));
            Assert.IsTrue(ReadRetry.IsPassing("the call failed (HttpRequestException)"));
            Assert.IsFalse(ReadRetry.IsPassing("no model is set up"));
            Assert.IsFalse(ReadRetry.IsPassing("only the local model may read this"));
            Assert.IsFalse(ReadRetry.IsPassing("the reply had no voice"));
            Assert.IsFalse(ReadRetry.IsPassing(null));
        }
    }
}
