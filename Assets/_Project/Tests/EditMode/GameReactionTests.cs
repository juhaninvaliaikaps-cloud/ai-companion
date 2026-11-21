using System;
using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.GameCompanion;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-49: her game reactions come from her Pisara personality. The model goes first, and
    /// pre-written lines in the same personality are the fallback.
    /// </summary>
    public sealed class GameReactionTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 21, 14, 0, 0, DateTimeKind.Utc);

        static GameEvent E(GameEventKind kind, double seconds, int count = 0)
        {
            return GameEvent.Of(GameTitle.CounterStrike2, kind, T0.AddSeconds(seconds), count);
        }

        static TemperamentAxes Bratty()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Warmth, 40);
            axes.Set(TemperamentAxes.Playfulness, 85);
            axes.Set(TemperamentAxes.Assertiveness, 75);
            axes.Set(TemperamentAxes.Directness, 80);
            axes.Set(TemperamentAxes.Confidence, 80);
            return axes;
        }

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
        }

        [Test]
        public void TheVoice_ComesFromHerSliders()
        {
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(TemperamentAxes.Neutral(), RelationshipRoles.Friend), "the middle of every slider");
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(TemperamentAxes.ForPreset(PersonaTemperaments.CalmId), null));
            Assert.AreEqual(GameVoice.Hype, GameVoices.For(TemperamentAxes.ForPreset(PersonaTemperaments.CheerfulId), null));
            Assert.AreEqual(GameVoice.Shy, GameVoices.For(TemperamentAxes.ForPreset(PersonaTemperaments.ShyId), null));
            Assert.AreEqual(GameVoice.Tsundere, GameVoices.For(TemperamentAxes.ForPreset(PersonaTemperaments.TsundereId), null));
            Assert.AreEqual(GameVoice.Grumpy, GameVoices.For(TemperamentAxes.ForPreset(PersonaTemperaments.MoodyId), null));
            Assert.AreEqual(GameVoice.Teasing, GameVoices.For(Bratty(), RelationshipRoles.Friend), "playful, pushy, not soft");
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(null, null));
        }

        [Test]
        public void ARival_TeasesSooner()
        {
            var axes = TemperamentAxes.Neutral();
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(axes, RelationshipRoles.Friend));
            Assert.AreEqual(GameVoice.Teasing, GameVoices.For(axes, RelationshipRoles.Rival));
        }

        [Test]
        public void TheBrattyOne_LaughsWhenYouDie()
        {
            var lines = GameReactionCatalog.Lines(GameVoice.Teasing, ReactionMoment.Death);
            CollectionAssert.Contains(lines, "You suck! Ha!");

            var reaction = new GameReactionLines(new Random(3)).Pick(GameVoice.Teasing, E(GameEventKind.Death, 0));
            Assert.IsFalse(reaction.IsEmpty);
            Assert.AreEqual(Emotion.Happy, reaction.Face);
            Assert.AreEqual(AvatarAnimatorStates.Laugh, reaction.Gesture);

            var sweet = new GameReactionLines(new Random(3)).Pick(GameVoice.Sweet, E(GameEventKind.Death, 0));
            Assert.AreEqual(Emotion.Concerned, sweet.Face, "a sweet one comforts you instead");
        }

        [Test]
        public void EveryVoice_HasShortLinesForEveryMoment()
        {
            var gestures = new HashSet<string>
            {
                string.Empty, AvatarAnimatorStates.Wave, AvatarAnimatorStates.Nod, AvatarAnimatorStates.Shrug, AvatarAnimatorStates.Laugh,
            };
            foreach (GameVoice voice in Enum.GetValues(typeof(GameVoice)))
            {
                foreach (ReactionMoment moment in Enum.GetValues(typeof(ReactionMoment)))
                {
                    if (moment == ReactionMoment.None)
                        continue;
                    var lines = GameReactionCatalog.Lines(voice, moment);
                    Assert.IsNotNull(lines, voice + " " + moment);
                    Assert.GreaterOrEqual(lines.Length, 3, voice + " " + moment);
                    foreach (var line in lines)
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(line), voice + " " + moment);
                        Assert.LessOrEqual(line.Length, GameReactionCatalog.MaxLineLength, line);
                        Assert.IsFalse(line.Contains("*"), "TTS would read the asterisks: " + line);
                    }

                    var look = GameReactionCatalog.Look(voice, moment);
                    Assert.IsTrue(gestures.Contains(look.Gesture ?? string.Empty), voice + " " + moment + " gesture " + look.Gesture);
                }
            }
        }

        [Test]
        public void ALine_NeverRepeatsTwiceInARow()
        {
            var lines = new GameReactionLines(new Random(7));
            string last = null;
            for (var i = 0; i < 60; i++)
            {
                var text = lines.Pick(GameVoice.Hype, E(GameEventKind.MatchWon, i)).Text;
                Assert.AreNotEqual(last, text, "pick " + i);
                last = text;
            }
        }

        [Test]
        public void AnUnclearMoment_HasNoLine()
        {
            var lines = new GameReactionLines(new Random(1));
            Assert.IsTrue(lines.Pick(GameVoice.Teasing, E(GameEventKind.BombDefused, 0)).IsEmpty, "the feed does not say whose bomb");
            Assert.IsTrue(lines.Pick(GameVoice.Teasing, null).IsEmpty);
        }

        [Test]
        public void AfterTheHourlyModelCap_SheStillSaysPreWrittenLines_WithTheCooldown()
        {
            var pacer = new CommentaryPacer { Frequency = CommentaryFrequency.Rare };
            var budget = CommentaryPacer.BudgetFor(CommentaryFrequency.Rare);
            var at = 0.0;
            for (var i = 0; i < budget; i++, at += 20)
            {
                var d = pacer.Consider(E(GameEventKind.Ace, at), T0.AddSeconds(at));
                Assert.IsTrue(d.Speak && d.UseModel, "model comment " + i);
                pacer.MarkSpoken(T0.AddSeconds(at), true);
            }

            var capped = pacer.Consider(E(GameEventKind.Ace, at), T0.AddSeconds(at));
            Assert.IsTrue(capped.Speak);
            Assert.IsFalse(capped.UseModel, "the cap stops model calls, not her");
            pacer.MarkSpoken(T0.AddSeconds(at), false);

            Assert.IsFalse(pacer.Consider(E(GameEventKind.Ace, at + 2), T0.AddSeconds(at + 2)).Speak, "a pre-written line starts the cooldown too");
            Assert.IsFalse(pacer.Consider(E(GameEventKind.Death, at + 30), T0.AddSeconds(at + 30)).Speak, "normal cooldown after a pre-written line");
            Assert.IsTrue(pacer.Consider(E(GameEventKind.Death, at + 100), T0.AddSeconds(at + 100)).Speak);
        }

        [Test]
        public void ThePreWrittenLines_DoNotUseUpTheModelCap()
        {
            var pacer = new CommentaryPacer { Frequency = CommentaryFrequency.Chatty };
            for (var i = 0; i < 100; i++)
                pacer.MarkSpoken(T0.AddSeconds(i * 20), false);
            var d = pacer.Consider(E(GameEventKind.MatchWon, 2100), T0.AddSeconds(2100));
            Assert.IsTrue(d.Speak && d.UseModel);
        }

        [Test]
        public void TheRoute_IsModelFirst_WithAPreWrittenFallback()
        {
            var yes = CommentDecision.Yes("cooldown over");
            var capped = CommentDecision.PreWritten("hourly budget used");
            var no = CommentDecision.No("cooldown");

            Assert.AreEqual(CommentarySource.Model, CommentaryRoute.Choose(CommentaryMode.ModelFirst, yes));
            Assert.AreEqual(CommentarySource.PreWritten, CommentaryRoute.Choose(CommentaryMode.ModelFirst, capped));
            Assert.AreEqual(CommentarySource.PreWritten, CommentaryRoute.Choose(CommentaryMode.PreWrittenOnly, yes));
            Assert.AreEqual(CommentarySource.None, CommentaryRoute.Choose(CommentaryMode.Off, yes));
            Assert.AreEqual(CommentarySource.None, CommentaryRoute.Choose(CommentaryMode.ModelFirst, no));

            Assert.IsTrue(CommentaryRoute.UseModelReply(true, "Nice!", 2));
            Assert.IsFalse(CommentaryRoute.UseModelReply(false, "Nice!", 2), "failed");
            Assert.IsFalse(CommentaryRoute.UseModelReply(true, "  ", 2), "empty");
            Assert.IsFalse(CommentaryRoute.UseModelReply(true, "Nice!", CommentaryRoute.ModelTimeoutSeconds + 1), "too late");
        }

        [Test]
        public void TheGate_PicksTheMode()
        {
            string reason;
            Assert.AreEqual(CommentaryMode.Off, CommentaryGate.Decide(false, true, true, false, out reason));
            Assert.AreEqual(CommentaryMode.PreWrittenOnly, CommentaryGate.Decide(true, true, true, true, out reason), "private mode: pre-written lines only");
            Assert.AreEqual(CommentaryGate.PrivatePreWritten, reason);
            Assert.AreEqual(CommentaryMode.PreWrittenOnly, CommentaryGate.Decide(true, false, true, false, out reason));
            Assert.AreEqual(CommentaryGate.NoConsent, reason);
            Assert.AreEqual(CommentaryMode.PreWrittenOnly, CommentaryGate.Decide(true, true, false, false, out reason));
            Assert.AreEqual(CommentaryGate.NoCloud, reason);
            Assert.AreEqual(CommentaryMode.ModelFirst, CommentaryGate.Decide(true, true, true, false, out reason));

            IntimateExpressions.Configure(true, true, true);
            Assert.AreEqual(CommentaryMode.PreWrittenOnly, CommentaryGate.Decide(true, true, true, false, out reason), "an open 18+ gate counts as private");
            Assert.AreEqual(CommentaryMode.Off, CommentaryGate.Decide(false, true, true, true, out reason), "off is off, private or not");
        }

        [Test]
        public void TheModelPrompt_IsHerPersona_ThenHerGameStyle_ThenTheRules()
        {
            var system = CommentaryPrompt.SystemText("You are Happi, a bratty gamer girl.", GameVoice.Teasing);
            var persona = system.IndexOf("You are Happi", StringComparison.Ordinal);
            var style = system.IndexOf("bratty and teasing", StringComparison.Ordinal);
            var rules = system.IndexOf("Never give tactical advice", StringComparison.Ordinal);
            Assert.AreEqual(0, persona);
            Assert.Greater(style, persona);
            Assert.Greater(rules, style);

            StringAssert.StartsWith("Your style while watching", CommentaryPrompt.SystemText(null, GameVoice.Cool));
        }
    }
}
