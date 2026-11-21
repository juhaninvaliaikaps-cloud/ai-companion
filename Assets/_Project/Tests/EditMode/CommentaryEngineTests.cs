using System;
using AICompanion.Core;
using AICompanion.GameCompanion;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-46: when she comments on a game, so she is fun, not annoying, and cloud calls stay sparse.</summary>
    public sealed class CommentaryEngineTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc);

        static GameEvent E(GameEventKind kind, double seconds, int count = 0)
        {
            return GameEvent.Of(GameTitle.CounterStrike2, kind, T0.AddSeconds(seconds), count);
        }

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
        }

        [Test]
        public void TheCooldown_FollowsTheFrequency()
        {
            var pacer = new CommentaryPacer { Frequency = CommentaryFrequency.Normal };
            Assert.IsTrue(pacer.Consider(E(GameEventKind.RoundWon, 0), T0).Speak);
            pacer.MarkSpoken(T0);

            Assert.IsFalse(pacer.Consider(E(GameEventKind.RoundLost, 30), T0.AddSeconds(30)).Speak, "40 s cooldown");
            Assert.IsTrue(pacer.Consider(E(GameEventKind.RoundWon, 41), T0.AddSeconds(41)).Speak);

            Assert.Less(CommentaryPacer.CooldownFor(CommentaryFrequency.Chatty), CommentaryPacer.CooldownFor(CommentaryFrequency.Normal));
            Assert.Greater(CommentaryPacer.CooldownFor(CommentaryFrequency.Rare), CommentaryPacer.CooldownFor(CommentaryFrequency.Normal));
        }

        [Test]
        public void ABigMoment_BreaksTheCooldown_ButNotRightAway()
        {
            var pacer = new CommentaryPacer();
            pacer.Consider(E(GameEventKind.RoundWon, 0), T0);
            pacer.MarkSpoken(T0);

            Assert.IsFalse(pacer.Consider(E(GameEventKind.Ace, 5), T0.AddSeconds(5)).Speak, "never sooner than 8 s");
            var ace = pacer.Consider(E(GameEventKind.Ace, 9), T0.AddSeconds(9));
            Assert.IsTrue(ace.Speak);
            Assert.AreEqual("big moment", ace.Reason);
        }

        [Test]
        public void AMinorMoment_IsOnlyContext()
        {
            var pacer = new CommentaryPacer();
            Assert.IsFalse(pacer.Consider(E(GameEventKind.Kill, 0), T0).Speak);
            Assert.IsFalse(pacer.Consider(E(GameEventKind.LevelUp, 1, 6), T0.AddSeconds(1)).Speak);
            Assert.IsTrue(pacer.Consider(E(GameEventKind.RoundWon, 2), T0.AddSeconds(2)).Speak);
            Assert.AreEqual(3, pacer.Context.Count, "the kill and the level-up go with the round win");

            pacer.MarkSpoken(T0.AddSeconds(2));
            Assert.AreEqual(0, pacer.Context.Count);
        }

        [Test]
        public void TheHourlyBudget_CapsEvenBigMoments()
        {
            var pacer = new CommentaryPacer { Frequency = CommentaryFrequency.Rare };
            var spoken = 0;
            for (var i = 0; i < 60; i++)
            {
                var at = T0.AddSeconds(i * 20);
                var decision = pacer.Consider(E(GameEventKind.Ace, i * 20), at);
                if (decision.Speak && decision.UseModel)
                {
                    pacer.MarkSpoken(at);
                    spoken++;
                }
            }

            Assert.AreEqual(CommentaryPacer.BudgetFor(CommentaryFrequency.Rare), spoken, "20 minutes of aces, one budget");
            Assert.AreEqual("hourly budget used", pacer.Consider(E(GameEventKind.Ace, 1300), T0.AddSeconds(1300)).Reason);
            Assert.IsTrue(pacer.Consider(E(GameEventKind.Ace, 3800), T0.AddSeconds(3800)).Speak, "an hour after the first comments the budget is back");
        }

        [Test]
        public void QuietMode_AndPushToMute_StopEverything()
        {
            var pacer = new CommentaryPacer { Quiet = true };
            Assert.AreEqual("muted", pacer.Consider(E(GameEventKind.MatchWon, 0), T0).Reason);

            pacer.Quiet = false;
            pacer.MuteFor(TimeSpan.FromMinutes(2), T0);
            Assert.IsFalse(pacer.Consider(E(GameEventKind.MatchWon, 60), T0.AddSeconds(60)).Speak);
            Assert.IsTrue(pacer.Consider(E(GameEventKind.MatchWon, 121), T0.AddSeconds(121)).Speak, "push-to-mute runs out");
        }

        [Test]
        public void QuickKills_BecomeAMultiKill_AndADeathStartsOver()
        {
            var detector = new BigMomentDetector();
            Assert.AreEqual(GameEventKind.Kill, detector.Process(E(GameEventKind.Kill, 0)).Kind);
            var two = detector.Process(E(GameEventKind.Kill, 3));
            Assert.AreEqual(GameEventKind.MultiKill, two.Kind);
            Assert.AreEqual(GameMoment.Normal, two.Moment);
            var three = detector.Process(E(GameEventKind.Kill, 6));
            Assert.AreEqual(3, three.Count);
            Assert.AreEqual(GameMoment.Big, three.Moment);

            detector.Process(E(GameEventKind.Death, 7));
            Assert.AreEqual(GameEventKind.Kill, detector.Process(E(GameEventKind.Kill, 8)).Kind);
            Assert.AreEqual(GameEventKind.Kill, detector.Process(E(GameEventKind.Kill, 30)).Kind, "too slow for a multi-kill");
        }

        [Test]
        public void TheGate_KeepsCommentaryOffInPrivateMode_And18Plus_AndWithoutConsent()
        {
            string reason;
            Assert.IsFalse(CommentaryGate.Allowed(false, true, false, out reason));
            Assert.AreEqual(CommentaryGate.Off, reason);
            Assert.IsFalse(CommentaryGate.Allowed(true, true, true, out reason));
            Assert.AreEqual(CommentaryGate.Private, reason);
            Assert.IsFalse(CommentaryGate.Allowed(true, false, false, out reason));
            Assert.AreEqual(CommentaryGate.NoConsent, reason);
            Assert.IsTrue(CommentaryGate.Allowed(true, true, false, out reason));

            IntimateExpressions.Configure(true, true, true);
            Assert.IsFalse(CommentaryGate.Allowed(true, true, false, out reason), "an open 18+ gate counts as private");
        }

        [Test]
        public void ThePrompt_ReactsToWhatHappened_AndKeepsTheFairPlayRules()
        {
            var pacer = new CommentaryPacer();
            pacer.Consider(E(GameEventKind.Kill, 0), T0);
            var won = E(GameEventKind.RoundWon, 2);
            pacer.Consider(won, T0.AddSeconds(2));

            var turn = CommentaryPrompt.Turn(GameTitle.CounterStrike2, pacer.Context, won);

            StringAssert.Contains("Counter-Strike 2", turn);
            StringAssert.Contains("Just now: their team won the round.", turn);
            StringAssert.Contains("Before that: they got a kill.", turn);
            StringAssert.Contains("Never give tactical advice, timers", CommentaryPrompt.Rules);
            StringAssert.Contains("enemy positions", CommentaryPrompt.Rules);
        }

        [Test]
        public void ADetail_IsShortAndSafe()
        {
            Assert.AreEqual("dragon", GameEventRules.SafeDetail("dragon"));
            Assert.AreEqual("Baron Nashor", GameEventRules.SafeDetail("Baron Nashor"));
            Assert.AreEqual("scriptalert1script", GameEventRules.SafeDetail("<script>alert(1)</script>"));
            Assert.LessOrEqual(GameEventRules.SafeDetail(new string('a', 100)).Length, GameEventRules.MaxDetailLength);
        }
    }
}
