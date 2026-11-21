using System;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-47: one game event through the session, and the Games settings.</summary>
    public sealed class GameCompanionSessionTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc);

        static GameEvent E(GameEventKind kind, double seconds, int count = 0, GameTitle game = GameTitle.CounterStrike2)
        {
            return GameEvent.Of(game, kind, T0.AddSeconds(seconds), count);
        }

        static GameCommentarySession Session()
        {
            return new GameCommentarySession(new Random(5));
        }

        [Test]
        public void WithoutTheCloud_SheSaysAPreWrittenLine()
        {
            var session = Session();
            var step = session.Next(E(GameEventKind.MatchWon, 0), CommentaryMode.PreWrittenOnly, GameVoice.Teasing, T0);
            Assert.AreEqual(CommentarySource.PreWritten, step.Source);
            Assert.IsFalse(step.Reaction.IsEmpty);
            CollectionAssert.Contains(GameReactionCatalog.Lines(GameVoice.Teasing, ReactionMoment.Win), step.Reaction.Text);
            Assert.IsTrue(string.IsNullOrEmpty(step.Turn), "nothing is written for a model");

            var soon = session.Next(E(GameEventKind.Death, 5), CommentaryMode.PreWrittenOnly, GameVoice.Teasing, T0.AddSeconds(5));
            Assert.IsTrue(soon.Nothing, "the pre-written line started the cooldown");
        }

        [Test]
        public void WithTheCloud_SheAsksTheModel_AndWaits()
        {
            var session = Session();
            var step = session.Next(E(GameEventKind.MatchWon, 0), CommentaryMode.ModelFirst, GameVoice.Hype, T0);
            Assert.AreEqual(CommentarySource.Model, step.Source);
            StringAssert.Contains("they won the match", step.Turn);
            Assert.IsTrue(session.Waiting);

            var meanwhile = session.Next(E(GameEventKind.Ace, 20), CommentaryMode.ModelFirst, GameVoice.Hype, T0.AddSeconds(20));
            Assert.IsTrue(meanwhile.Nothing, "one request at a time");

            bool fromModel;
            var line = session.Resolve(step, true, "*jumps* \"YES! You did it!\" <cue emotion=happy>", 2, T0.AddSeconds(2), out fromModel);
            Assert.IsTrue(fromModel);
            Assert.AreEqual("YES! You did it!", line.Text);
            Assert.AreEqual(step.Reaction.Face, line.Face, "the face comes from her voice");
            Assert.IsFalse(session.Waiting);
        }

        [Test]
        public void AFailedOrLateModelLine_FallsBackToAPreWrittenOne()
        {
            var session = Session();
            var step = session.Next(E(GameEventKind.MatchLost, 0), CommentaryMode.ModelFirst, GameVoice.Sweet, T0);
            bool fromModel;
            var failed = session.Resolve(step, false, string.Empty, 1, T0.AddSeconds(1), out fromModel);
            Assert.IsFalse(fromModel);
            Assert.AreEqual(step.Reaction.Text, failed.Text);

            var late = Session();
            var lateStep = late.Next(E(GameEventKind.MatchLost, 0), CommentaryMode.ModelFirst, GameVoice.Sweet, T0);
            var tooLate = late.Resolve(lateStep, true, "Aw, next time!", CommentaryRoute.ModelTimeoutSeconds + 2, T0.AddSeconds(9), out fromModel);
            Assert.IsFalse(fromModel, "the moment has passed");
            Assert.AreEqual(lateStep.Reaction.Text, tooLate.Text);
        }

        [Test]
        public void Off_MeansNothing()
        {
            var session = Session();
            Assert.IsTrue(session.Next(E(GameEventKind.MatchWon, 0), CommentaryMode.Off, GameVoice.Hype, T0).Nothing);
            Assert.IsTrue(session.Next(null, CommentaryMode.ModelFirst, GameVoice.Hype, T0).Nothing);
        }

        [Test]
        public void AQuickRunOfKills_BecomesABigMoment_ButNotInLeague()
        {
            var session = Session();
            session.Next(E(GameEventKind.Kill, 0), CommentaryMode.PreWrittenOnly, GameVoice.Cool, T0);
            var second = session.Next(E(GameEventKind.Kill, 2), CommentaryMode.PreWrittenOnly, GameVoice.Cool, T0.AddSeconds(2));
            Assert.AreEqual(GameEventKind.MultiKill, second.Event.Kind);
            Assert.AreEqual(CommentarySource.PreWritten, second.Source, "a double kill is worth a line");
            var third = session.Next(E(GameEventKind.Kill, 4), CommentaryMode.PreWrittenOnly, GameVoice.Cool, T0.AddSeconds(4));
            Assert.AreEqual(GameEventKind.MultiKill, third.Event.Kind);
            Assert.AreEqual(3, third.Event.Count);
            Assert.AreEqual(GameMoment.Big, third.Event.Moment);

            var league = Session();
            var kill = league.Next(E(GameEventKind.Kill, 0, 0, GameTitle.LeagueOfLegends), CommentaryMode.PreWrittenOnly, GameVoice.Cool, T0);
            Assert.AreEqual(GameEventKind.Kill, kill.Event.Kind, "League reports multi-kills itself");
        }

        [Test]
        public void AModelReply_IsCleanedToOneSpokenLine()
        {
            Assert.AreEqual("Nice shot!", GameCommentarySession.CleanReply("  *grins*  \"Nice   shot!\"  "));
            Assert.AreEqual("Ha, you died.", GameCommentarySession.CleanReply("<reaction emotion=\"happy\"/>Ha, you died."));
            Assert.AreEqual(string.Empty, GameCommentarySession.CleanReply(null));
            var longLine = new string('a', 100) + ". " + new string('b', 300);
            Assert.LessOrEqual(GameCommentarySession.CleanReply(longLine).Length, GameCommentarySession.MaxModelLineLength + 3);
        }

        [Test]
        public void GsiPosts_AreRoutedByTheGame()
        {
            Assert.AreEqual(GameTitle.CounterStrike2, GsiRouting.GameOf("{\"provider\":{\"name\":\"Counter-Strike: Global Offensive\",\"appid\":730}}"));
            Assert.AreEqual(GameTitle.Dota2, GsiRouting.GameOf("{\"provider\":{\"name\":\"Dota 2\",\"appid\":570}}"));
            Assert.AreEqual(GameTitle.Unknown, GsiRouting.GameOf("{\"provider\":{\"appid\":440}}"));
            Assert.AreEqual(GameTitle.Unknown, GsiRouting.GameOf("not json"));
            Assert.AreEqual(GameTitle.Unknown, GsiRouting.GameOf(null));
        }

        [Test]
        public void TheGamesSettings_DefaultOff_AndRoundTrip()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsFalse(fresh.GameCompanion);
            Assert.IsFalse(fresh.GameCloudConsent);
            Assert.AreEqual(GameFrequencyNames.Normal, fresh.GameFrequency);
            Assert.AreEqual(GsiConfig.DefaultPort, fresh.GsiPort);
            Assert.AreEqual(string.Empty, fresh.GsiToken);

            fresh.GameCompanion = true;
            fresh.GameCloudConsent = true;
            fresh.GameFrequency = GameFrequencyNames.Chatty;
            fresh.GameDota2 = false;
            fresh.GsiPort = 40000;
            fresh.GsiToken = "abc123";
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None);
            Assert.IsTrue(back.GameCompanion);
            Assert.IsTrue(back.GameCloudConsent);
            Assert.AreEqual(GameFrequencyNames.Chatty, back.GameFrequency);
            Assert.IsTrue(back.GameCs2);
            Assert.IsFalse(back.GameDota2);
            Assert.AreEqual(40000, back.GsiPort);
            Assert.AreEqual("abc123", back.GsiToken);
        }

        [Test]
        public void AnOldOrBrokenSettingsFile_GetsSafeGameValues()
        {
            var old = CompanionSettingsJson.Parse("{\n  \"version\": 1\n}\n", SettingsLegacyPrefs.None);
            Assert.IsFalse(old.GameCompanion);
            Assert.AreEqual(GsiConfig.DefaultPort, old.GsiPort);

            var broken = CompanionSettingsJson.Parse("{\n  \"gameFrequency\": \"LOUD\",\n  \"gsiPort\": 80,\n  \"gsiToken\": \"a b/c\\\"1\"\n}\n", SettingsLegacyPrefs.None);
            Assert.AreEqual(GameFrequencyNames.Normal, broken.GameFrequency);
            Assert.AreEqual(GsiConfig.DefaultPort, broken.GsiPort, "a well-known port is refused");
            Assert.AreEqual("abc1", broken.GsiToken, "letters and digits only");
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(CompanionSettingsJson.Write(broken)), "the GSI token is not an API key field");
        }

        [Test]
        public void TheGameText_IsHerStyle_ThenTheRules()
        {
            var text = CommentaryPrompt.GameText(GameVoice.Grumpy);
            StringAssert.StartsWith(GameVoices.StyleLine(GameVoice.Grumpy), text);
            StringAssert.Contains(CommentaryPrompt.Rules, text);
        }
    }
}
