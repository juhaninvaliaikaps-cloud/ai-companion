using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class PortraitTests
    {
        [Test]
        public void Defaults_MapTheEmotionSetAndFallBackToNeutral()
        {
            var map = PortraitMap.CreateDefaults();

            Assert.AreEqual(PortraitStems.NeutralOpen, map.Resolve("neutral").Open);
            Assert.AreEqual(PortraitStems.NeutralBlinkHalf, map.Resolve("neutral").BlinkHalf);
            Assert.AreEqual(PortraitStems.NeutralTalk, map.Resolve("neutral").Talk);
            Assert.IsTrue(map.Resolve("neutral").HasBlink);
            Assert.IsTrue(map.Resolve("neutral").HasTalk);

            Assert.AreEqual(PortraitStems.SmileOpen, map.Resolve("happy").Open);
            Assert.AreEqual(PortraitStems.SmileBlinkClosed, map.Resolve("happy").BlinkClosed);
            Assert.AreEqual(PortraitStems.SmileTalk, map.Resolve("smile").Talk);
            Assert.AreEqual(PortraitStems.SmileBlinkHalf, map.Resolve("happy").BlinkHalf);

            AssertAnimated(map, "laughing", PortraitStems.Happy, PortraitStems.HappyBlinkHalf, PortraitStems.HappyBlinkClosed, PortraitStems.HappyTalk);
            AssertAnimated(map, "sad", PortraitStems.Sad, PortraitStems.SadBlinkHalf, PortraitStems.SadBlinkClosed, PortraitStems.SadTalk);
            AssertAnimated(map, "surprised", PortraitStems.Surprised, PortraitStems.SurprisedBlinkHalf, PortraitStems.SurprisedBlinkClosed, PortraitStems.SurprisedTalk);
            AssertAnimated(map, "thinking", PortraitStems.Thinking, PortraitStems.ThinkingBlinkHalf, PortraitStems.ThinkingBlinkClosed, PortraitStems.ThinkingTalk);
            AssertAnimated(map, "angry", PortraitStems.Pouty, PortraitStems.PoutyBlinkHalf, PortraitStems.PoutyBlinkClosed, PortraitStems.PoutyTalk);
            AssertAnimated(map, "pouty", PortraitStems.Pouty, PortraitStems.PoutyBlinkHalf, PortraitStems.PoutyBlinkClosed, PortraitStems.PoutyTalk);
            AssertAnimated(map, "excited", PortraitStems.Excited, PortraitStems.ExcitedBlinkHalf, PortraitStems.ExcitedBlinkClosed, PortraitStems.ExcitedTalk);
            AssertAnimated(map, "concerned", PortraitStems.Concerned, PortraitStems.ConcernedBlinkHalf, PortraitStems.ConcernedBlinkClosed, PortraitStems.ConcernedTalk);
            AssertAnimated(map, "embarrassed", PortraitStems.Embarrassed, PortraitStems.EmbarrassedBlinkHalf, PortraitStems.EmbarrassedBlinkClosed, PortraitStems.EmbarrassedTalk);
            AssertAnimated(map, "shy", PortraitStems.Shy, PortraitStems.ShyBlinkHalf, PortraitStems.ShyBlinkClosed, PortraitStems.ShyTalk);
            AssertAnimated(map, "flustered", PortraitStems.Flustered, PortraitStems.FlusteredBlinkHalf, PortraitStems.FlusteredBlinkClosed, PortraitStems.FlusteredTalk);

            AssertStill(map, "sleepy", PortraitStems.Sleepy);
            AssertStill(map, "tired", PortraitStems.Sleepy);
            AssertStill(map, "teasing", PortraitStems.SmugWink);
            AssertStill(map, "loving", PortraitStems.WinkSmile);
            AssertStill(map, "embarrassed_worried", PortraitStems.EmbarrassedWorried);

            Assert.AreEqual(PortraitStems.NeutralOpen, map.Resolve("not-an-emotion").Open);
            Assert.AreEqual(PortraitStems.NeutralOpen, map.Resolve(null).Open);
            Assert.AreEqual(PortraitStems.NeutralOpen, map.Resolve(string.Empty).Open);

            string between;
            Assert.IsTrue(map.TryTransition("neutral", "happy", out between));
            Assert.AreEqual(PortraitStems.Smile, between);
        }

        [Test]
        public void Names_UseTheAnimatorEmotionAndRejectUnknownValues()
        {
            Assert.AreEqual("neutral", PortraitNames.For(Emotion.Neutral));
            Assert.AreEqual("happy", PortraitNames.For(Emotion.Happy));
            Assert.AreEqual("sad", PortraitNames.For(Emotion.Sad));
            Assert.AreEqual("surprised", PortraitNames.For(Emotion.Surprised));
            Assert.AreEqual("thinking", PortraitNames.For(Emotion.Thinking));
            Assert.AreEqual("neutral", PortraitNames.For((Emotion)99));
        }

        [Test]
        public void Parse_ReadsFrameSetsStringsAndTransitions()
        {
            const string json = "{"
                + "\"happy\": {\"open\": \"01_happy.png\", \"blinkHalf\": \"smile_blink_half\", \"blinkClosed\": \"smile_blink_closed\", \"talk\": \"smile_talk\"},"
                + "\"sad\": {\"open\": \"10_sad\", \"blink_half\": \"neutral_blink_half\", \"blink_closed\": \"neutral_blink_closed\"},"
                + "\"angry\": \"09_pouty\","
                + "\"neutral\": \"../secret\","
                + "\"transitions\": {\"neutral->happy\": {\"inBetween\": \"05_smile\"}, \"sad->neutral\": \"04_neutral\"}"
                + "}";

            var map = PortraitMapJson.Parse(json);
            Assert.AreEqual(PortraitStems.Happy, map.Resolve("happy").Open);
            Assert.IsTrue(map.Resolve("happy").HasBlink);
            Assert.AreEqual(PortraitStems.SmileTalk, map.Resolve("happy").Talk);
            Assert.IsTrue(map.Resolve("sad").HasBlink);
            Assert.AreEqual(PortraitStems.NeutralBlinkHalf, map.Resolve("sad").BlinkHalf);
            Assert.AreEqual(PortraitStems.Pouty, map.Resolve("angry").Open);
            Assert.IsFalse(map.Resolve("angry").HasTalk);
            Assert.AreEqual(PortraitStems.NeutralOpen, map.Resolve("neutral").Open);
            Assert.AreEqual(PortraitStems.Surprised, map.Resolve("surprised").Open);
            Assert.AreEqual(PortraitStems.SurprisedBlinkHalf, map.Resolve("surprised").BlinkHalf);
            Assert.AreEqual(PortraitStems.HappyBlinkHalf, map.Resolve("laughing").BlinkHalf);

            string file;
            Assert.IsTrue(map.TryTransition("neutral", "happy", out file));
            Assert.AreEqual(PortraitStems.Smile, file);
            Assert.IsTrue(map.TryTransition("sad", "neutral", out file));
            Assert.AreEqual(PortraitStems.NeutralStill, file);
            Assert.IsFalse(map.TryTransition("happy", "sad", out file));
        }

        [Test]
        public void Parse_BlankOrBrokenJsonKeepsDefaults()
        {
            Assert.AreEqual(PortraitStems.SmileOpen, PortraitMapJson.Parse(null).Resolve("happy").Open);
            Assert.AreEqual(PortraitStems.SmileOpen, PortraitMapJson.Parse("  ").Resolve("happy").Open);
            Assert.AreEqual(PortraitStems.SmileOpen, PortraitMapJson.Parse("{").Resolve("happy").Open);

            var cleared = PortraitMapJson.Parse("{\"transitions\": {}}");
            string file;
            Assert.IsFalse(cleared.TryTransition("neutral", "happy", out file));
            Assert.AreEqual(PortraitStems.SmileOpen, cleared.Resolve("happy").Open);

            var entering = PortraitMapJson.Parse("{\"excited\": {\"open\": \"15_excited\", \"inBetween\": \"05_smile\"}}");
            Assert.IsTrue(entering.TryTransition("neutral", "excited", out file));
            Assert.AreEqual(PortraitStems.Smile, file);
        }

        [Test]
        public void Blink_PlaysHalfClosedHalfAndSometimesTwice()
        {
            Assert.AreEqual(2f, PortraitBlinkScheduler.Gap(0f), 0.0001f);
            Assert.AreEqual(6f, PortraitBlinkScheduler.Gap(1f), 0.0001f);
            Assert.AreEqual(4f, PortraitBlinkScheduler.Gap(0.5f), 0.0001f);
            Assert.AreEqual(2f, PortraitBlinkScheduler.Gap(float.NaN), 0.0001f);
            Assert.AreEqual(0.04f, PortraitBlinkScheduler.FrameHold(0f), 0.0001f);
            Assert.AreEqual(0.06f, PortraitBlinkScheduler.FrameHold(1f), 0.0001f);
            Assert.IsTrue(PortraitBlinkScheduler.RollsDouble(0f));
            Assert.IsTrue(PortraitBlinkScheduler.RollsDouble(0.19f));
            Assert.IsFalse(PortraitBlinkScheduler.RollsDouble(0.2f));

            var blink = PortraitBlinkScheduler.Start(0f);
            blink = blink.Tick(1.9f, 0f, 0f, 1f);
            Assert.IsTrue(blink.IsWaiting);
            Assert.AreEqual(PortraitLid.Open, blink.Lid);

            blink = blink.Tick(5f, 0f, 0f, 1f);
            Assert.AreEqual(PortraitLid.Half, blink.Lid);
            blink = blink.Tick(0.04f, 0f, 0f, 1f);
            Assert.AreEqual(PortraitLid.Closed, blink.Lid);
            blink = blink.Tick(0.04f, 0f, 0f, 1f);
            Assert.AreEqual(PortraitLid.Half, blink.Lid);
            blink = blink.Tick(0.04f, 0f, 0f, 1f);
            Assert.IsTrue(blink.IsWaiting);
            Assert.AreEqual(PortraitLid.Open, blink.Lid);

            var twice = PortraitBlinkScheduler.Start(0f);
            twice = twice.Tick(2f, 0f, 0f, 0f);
            var closed = 0;
            Assert.AreEqual(PortraitLid.Half, twice.Lid);
            for (var i = 0; i < 8; i++)
            {
                twice = twice.Tick(0.04f, 0f, 0f, 0f);
                if (twice.Lid == PortraitLid.Closed)
                    closed++;
            }

            Assert.AreEqual(2, closed);
            Assert.IsTrue(twice.IsWaiting);
        }

        [Test]
        public void Talk_FollowsAmplitudeAndFallsBackToATimer()
        {
            var quiet = new PortraitTalkMotion();
            quiet = quiet.Tick(0.1f, false, true, true, 1f, 0f);
            Assert.IsFalse(quiet.ShowTalk);

            var loud = new PortraitTalkMotion();
            loud = loud.Tick(0f, true, true, true, 0.8f, 0f);
            Assert.IsTrue(loud.ShowTalk);
            Assert.IsTrue(loud.FollowsAmplitude);
            loud = loud.Tick(0.05f, true, true, true, 0f, 0f);
            Assert.IsTrue(loud.ShowTalk);
            loud = loud.Tick(0.05f, true, true, true, 0f, 0f);
            Assert.IsFalse(loud.ShowTalk);

            var waiting = new PortraitTalkMotion();
            waiting = waiting.Tick(0.1f, true, true, true, 0f, 0f);
            Assert.IsFalse(waiting.ShowTalk);
            waiting = waiting.Tick(0.1f, true, true, true, 0f, 0f);
            Assert.IsFalse(waiting.ShowTalk);
            waiting = waiting.Tick(0.1f, true, true, true, 0f, 0f);
            Assert.IsTrue(waiting.ShowTalk);
            Assert.IsFalse(waiting.FollowsAmplitude);

            var flapping = new PortraitTalkMotion();
            flapping = flapping.Tick(0f, true, true, false, 0f, 0f);
            Assert.IsTrue(flapping.ShowTalk);
            flapping = flapping.Tick(0.05f, true, true, false, 0f, 0f);
            Assert.IsTrue(flapping.ShowTalk);
            flapping = flapping.Tick(0.08f, true, true, false, 0f, 0f);
            Assert.IsFalse(flapping.ShowTalk);

            var noFrame = new PortraitTalkMotion();
            noFrame = noFrame.Tick(0f, true, false, true, 1f, 0f);
            Assert.IsFalse(noFrame.ShowTalk);

            var smile = PortraitFrames.SmileSet();
            Assert.AreEqual(PortraitStems.SmileTalk, PortraitFrames.Choose(smile, PortraitLid.Half, true));
            Assert.AreEqual(PortraitStems.SmileBlinkHalf, PortraitFrames.Choose(smile, PortraitLid.Half, false));
            Assert.AreEqual(PortraitStems.SmileBlinkClosed, PortraitFrames.Choose(smile, PortraitLid.Closed, false));
            Assert.AreEqual(PortraitStems.Sad, PortraitFrames.Choose(PortraitFrameSet.Single(PortraitStems.Sad), PortraitLid.Closed, true));
        }

        [Test]
        public void Fade_CrossfadesAndHoldsTheInBetweenFrame()
        {
            Assert.AreEqual(0.15f, PortraitCrossfade.DurationFor(0f), 0.0001f);
            Assert.AreEqual(0.25f, PortraitCrossfade.DurationFor(1f), 0.0001f);
            Assert.AreEqual(0.2f, PortraitCrossfade.DurationFor(0.5f), 0.0001f);

            var fade = new PortraitCrossfade();
            fade.Begin("neutral_open", "smile_open", null, 0.2f);
            var mid = fade.Advance(0.1f);
            Assert.AreEqual("neutral_open", mid.From);
            Assert.AreEqual(0.5f, mid.FromAlpha, 0.001f);
            Assert.AreEqual(0.5f, mid.ToAlpha, 0.001f);
            var done = fade.Advance(0.1f);
            Assert.IsFalse(fade.Active);
            Assert.AreEqual("smile_open", done.To);
            Assert.AreEqual(1f, done.ToAlpha, 0.001f);

            var held = PortraitCrossfade.Sample(0.5f, "neutral_open", "smile_open", "05_smile");
            Assert.AreEqual("05_smile", held.Between);
            Assert.AreEqual(1f, held.BetweenAlpha, 0.001f);
            Assert.AreEqual(0f, held.FromAlpha, 0.001f);
            Assert.AreEqual(0f, held.ToAlpha, 0.001f);

            var edge = PortraitCrossfade.Sample(0.1f, "neutral_open", "smile_open", "05_smile");
            Assert.Greater(edge.FromAlpha, 0.5f);
            Assert.Greater(edge.BetweenAlpha, 0f);
            Assert.AreEqual(0f, edge.ToAlpha, 0.001f);

            fade.Begin("same", "same", null, 0.2f);
            Assert.IsFalse(fade.Active);
            fade.Begin("same", "same", "05_smile", 9f);
            Assert.IsTrue(fade.Active);
            Assert.AreEqual(PortraitCrossfade.MaxSeconds, fade.Duration, 0.0001f);
            fade.Begin("a", "b", null, 0.01f);
            Assert.AreEqual(PortraitCrossfade.MinSeconds, fade.Duration, 0.0001f);
        }

        [Test]
        public void Fit_KeepsAspectForBothSheetSizes()
        {
            var wide = PortraitFit.Inside(160f, 180f, 448f, 504f);
            Assert.AreEqual(160f, wide.width, 0.01f);
            Assert.AreEqual(180f, wide.height, 0.01f);
            Assert.AreEqual(0f, wide.x, 0.01f);
            Assert.AreEqual(0f, wide.y, 0.01f);

            var other = PortraitFit.Inside(160f, 180f, 320f, 360f);
            Assert.AreEqual(160f, other.width, 0.01f);
            Assert.AreEqual(180f, other.height, 0.01f);

            var tall = PortraitFit.Inside(160f, 180f, 100f, 200f);
            Assert.AreEqual(90f, tall.width, 0.01f);
            Assert.AreEqual(180f, tall.height, 0.01f);
            Assert.AreEqual(35f, tall.x, 0.01f);
            Assert.AreEqual(0f, tall.y, 0.01f);
        }

        [Test]
        public void Director_BlinksTalksAndCrossfadesFromTheLibrary()
        {
            var library = PortraitLibrary.FromFileNames(new[]
            {
                "neutral_open.png",
                "neutral_blink_half.png",
                "neutral_blink_closed.png",
                "neutral_talk.png",
                "smile_open.png",
                "smile_blink_half.png",
                "smile_blink_closed.png",
                "smile_talk.png",
                "05_smile.png",
                "10_sad.png"
            });
            var map = PortraitMap.CreateDefaults();
            var director = new PortraitDirector();

            var open = director.Tick(0f, "neutral", false, false, 0f, map, library, 0f, 0f, 1f, 0f, 0.5f);
            Assert.AreEqual("neutral_open.png", open.Front);
            Assert.AreEqual(1f, open.FrontAlpha, 0.001f);

            var half = director.Tick(2f, "neutral", false, false, 0f, map, library, 0f, 0f, 1f, 0f, 0.5f);
            Assert.AreEqual("neutral_blink_half.png", half.Front);

            var talking = new PortraitDirector();
            var mouth = talking.Tick(0f, "neutral", true, true, 0.9f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("neutral_talk.png", mouth.Front);

            var sad = new PortraitDirector(_ => { });
            var still = sad.Tick(0f, "sad", true, true, 0.9f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("10_sad.png", still.Front);
            Assert.IsTrue(still.Visible);

            var missing = new PortraitDirector(_ => { });
            var fallback = missing.Tick(0f, "surprised", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("neutral_open.png", fallback.Front);
            Assert.IsTrue(fallback.Visible);

            var fading = new PortraitDirector();
            fading.Tick(0f, "neutral", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            var start = fading.Tick(0f, "happy", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.IsTrue(fading.Fading);
            Assert.AreEqual("neutral_open.png", start.Back);
            Assert.AreEqual(1f, start.BackAlpha, 0.001f);
            var between = fading.Tick(0.1f, "happy", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("05_smile.png", between.Mid);
            Assert.AreEqual(1f, between.MidAlpha, 0.001f);
        }

        [Test]
        public void MissingFolder_HidesLogsOnceAndFallsBackToPersistentData()
        {
            var off = PortraitShowPlan.Describe(false, false, false);
            Assert.IsFalse(off.Show);
            Assert.IsFalse(off.ShouldLog);

            var missing = PortraitShowPlan.Describe(true, false, false);
            Assert.IsFalse(missing.Show);
            Assert.IsTrue(missing.ShouldLog);
            Assert.AreEqual(PortraitHideReason.FolderMissing, missing.Reason);

            var noNeutral = PortraitShowPlan.Describe(true, true, false);
            Assert.IsFalse(noNeutral.Show);
            Assert.AreEqual(PortraitHideReason.NeutralMissing, noNeutral.Reason);

            var ready = PortraitShowPlan.Describe(true, true, true);
            Assert.IsTrue(ready.Show);
            Assert.IsFalse(ready.ShouldLog);

            var root = Path.Combine(Path.GetTempPath(), "leia-portraits-" + Guid.NewGuid().ToString("N"));
            var app = Path.Combine(root, "app");
            var persistent = Path.Combine(root, "persistent");
            try
            {
                Assert.IsNull(PortraitFolders.Resolve(app, persistent, Directory.Exists));
                var fallback = Path.Combine(persistent, "ProjectLeia", "portraits");
                Directory.CreateDirectory(fallback);
                File.WriteAllText(Path.Combine(fallback, "neutral_open.png"), "png");
                Assert.AreEqual(fallback, PortraitFolders.Resolve(app, persistent, Directory.Exists));

                var primary = Path.Combine(app, "ProjectLeia", "portraits");
                Directory.CreateDirectory(primary);
                Assert.AreEqual(primary, PortraitFolders.Resolve(app, persistent, Directory.Exists));
                Assert.IsTrue(primary.StartsWith(root, StringComparison.Ordinal));

                var loaded = PortraitLibrary.Load(fallback);
                Assert.AreEqual(Path.Combine(fallback, "neutral_open.png"), loaded.Existing("neutral_open"));
                Assert.IsTrue(loaded.ContainsNeutral(PortraitMap.CreateDefaults()));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void MissingFrame_UsesTheStillThenNeutralAndWarnsOnce()
        {
            var library = PortraitLibrary.FromFileNames(new[]
            {
                "neutral_open.png",
                "neutral_blink_half.png",
                "neutral_blink_closed.png",
                "neutral_talk.png",
                "10_sad.png",
                "sad_talk.png"
            });

            Assert.AreEqual("10_sad.png", PortraitFiles.Resolve(library, PortraitStems.SadBlinkHalf, PortraitStems.Sad, PortraitStems.NeutralOpen));
            Assert.AreEqual("sad_talk.png", PortraitFiles.Resolve(library, PortraitStems.SadTalk, PortraitStems.Sad, PortraitStems.NeutralOpen));
            Assert.AreEqual("neutral_open.png", PortraitFiles.Resolve(library, PortraitStems.Surprised, PortraitStems.Surprised, PortraitStems.NeutralOpen));
            Assert.AreEqual("neutral_open.png", PortraitFiles.Resolve(library, PortraitStems.SurprisedBlinkClosed, PortraitStems.Surprised, PortraitStems.NeutralOpen));
            Assert.AreEqual("neutral_open.png", PortraitFiles.Resolve(library, PortraitStems.NeutralOpen, PortraitStems.NeutralOpen, PortraitStems.NeutralOpen));
            Assert.IsNull(PortraitFiles.Resolve(null, PortraitStems.Sad, PortraitStems.Sad, PortraitStems.NeutralOpen));

            var warnings = new List<string>();
            var map = PortraitMap.CreateDefaults();
            var director = new PortraitDirector(warnings.Add);
            var quiet = director.Tick(0f, "sad", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("10_sad.png", quiet.Front);
            Assert.IsTrue(quiet.Visible);
            Assert.AreEqual(2, warnings.Count);
            CollectionAssert.Contains(warnings, PortraitMissing.Line(PortraitStems.SadBlinkHalf, false));
            CollectionAssert.Contains(warnings, PortraitMissing.Line(PortraitStems.SadBlinkClosed, false));

            director.Tick(0.05f, "sad", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual(2, warnings.Count);

            var talking = director.Tick(0f, "sad", true, true, 0.9f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("sad_talk.png", talking.Front);
            Assert.IsTrue(talking.Visible);
            Assert.AreEqual(2, warnings.Count);

            var before = warnings.Count;
            var surprisedDirector = new PortraitDirector(warnings.Add);
            var surprised = surprisedDirector.Tick(0f, "surprised", false, false, 0f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual("neutral_open.png", surprised.Front);
            Assert.IsTrue(surprised.Visible);
            CollectionAssert.Contains(warnings, PortraitMissing.Line(PortraitStems.Surprised, true));
            CollectionAssert.Contains(warnings, PortraitMissing.Line(PortraitStems.SurprisedTalk, true));
            var afterSurprised = warnings.Count;
            surprisedDirector.Tick(0.05f, "surprised", true, true, 1f, map, library, 1f, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual(afterSurprised, warnings.Count);
            Assert.Greater(afterSurprised, before);
        }

        [Test]
        public void Override_ReplacesDefaultFramesAndLeavesTheRest()
        {
            const string json = "{"
                + "\"concerned\": \"02_concerned\","
                + "\"laughing\": {\"open\": \"01_happy\", \"blinkHalf\": \"smile_blink_half\", \"blinkClosed\": \"smile_blink_closed\", \"talk\": \"smile_talk\"}"
                + "}";

            var map = PortraitMapJson.Parse(json);
            Assert.AreEqual(PortraitStems.Concerned, map.Resolve("concerned").Open);
            Assert.IsFalse(map.Resolve("concerned").HasBlink);
            Assert.IsFalse(map.Resolve("concerned").HasTalk);
            Assert.AreEqual(PortraitStems.Happy, map.Resolve("laughing").Open);
            Assert.AreEqual(PortraitStems.SmileBlinkHalf, map.Resolve("laughing").BlinkHalf);
            Assert.AreEqual(PortraitStems.SmileTalk, map.Resolve("laughing").Talk);
            Assert.AreEqual(PortraitStems.SadBlinkHalf, map.Resolve("sad").BlinkHalf);
            Assert.AreEqual(PortraitStems.PoutyTalk, map.Resolve("pouty").Talk);
            Assert.AreEqual(PortraitStems.SmileOpen, map.Resolve("happy").Open);
        }

        [Test]
        public void Library_PrefersPngAndRejectsUnsafeNames()
        {
            var library = PortraitLibrary.FromFileNames(new[]
            {
                "neutral_open.jpg",
                "neutral_open.png",
                "notes.txt",
                "../evil.png",
                "a/b.png"
            });
            Assert.AreEqual(1, library.Count);
            Assert.AreEqual("neutral_open.png", library.Existing("neutral_open"));
            Assert.AreEqual("neutral_open.png", library.Existing("neutral_open.jpg"));
            Assert.IsNull(library.Existing("../evil"));
            Assert.IsNull(library.Existing("a/b"));
            Assert.IsFalse(PortraitStems.IsSafe("../secret"));
            Assert.IsFalse(PortraitStems.IsSafe("..\\windows\\system32"));
            Assert.AreEqual("01_happy", PortraitStems.Stem("01_happy.PNG"));
        }

        [Test]
        public void Settings_RoundTripDefaultsThePortraitOff()
        {
            var settings = CompanionSettings.CreateDefault();
            Assert.IsFalse(settings.ShowExpressionPortrait);

            settings.ShowExpressionPortrait = true;
            var json = CompanionSettingsJson.Write(settings);
            StringAssert.Contains("\"showExpressionPortrait\": true", json);
            var loaded = CompanionSettingsJson.Parse(json, SettingsLegacyPrefs.None);
            Assert.IsTrue(loaded.ShowExpressionPortrait);

            var omitted = CompanionSettingsJson.Parse("{\"version\":1,\"providerId\":\"local\"}", SettingsLegacyPrefs.None);
            Assert.IsFalse(omitted.ShowExpressionPortrait);
            Assert.IsFalse(CompanionSettingsJson.Write(omitted).Contains("\"showExpressionPortrait\": true"));
        }

        [Test]
        public void Placement_SitsBesideTheHeadAndInsideTheWindow()
        {
            var chat = new Rect(8f, 124f, 220f, 340f);
            var placed = PortraitPlacement.Beside(new Vector2(300f, 400f), 480f, 720f, chat, PortraitPlacement.DebugLabel);
            Assert.GreaterOrEqual(placed.x, 300f);
            Assert.AreEqual(PortraitPlacement.Width, placed.width, 0.001f);
            Assert.AreEqual(PortraitPlacement.Height, placed.height, 0.001f);
            Assert.LessOrEqual(placed.xMax, 480f);
            Assert.LessOrEqual(placed.yMax, 720f);
            Assert.GreaterOrEqual(placed.xMin, 0f);
            Assert.IsFalse(PortraitPlacement.Overlaps(placed, chat));
            Assert.IsFalse(PortraitPlacement.Overlaps(placed, PortraitPlacement.DebugLabel));

            var first = PortraitPlacement.Smooth(new Rect(0f, 0f, 1f, 1f), placed, 0f, false);
            Assert.AreEqual(placed.x, first.x, 0.001f);
            var next = PortraitPlacement.Smooth(first, new Rect(placed.x + 40f, placed.y, placed.width, placed.height), 0.05f, true);
            Assert.Greater(next.x, placed.x);
            Assert.Less(next.x, placed.x + 40f);
        }

        static void AssertAnimated(PortraitMap map, string emotion, string open, string half, string closed, string talk)
        {
            var set = map.Resolve(emotion);
            Assert.AreEqual(open, set.Open, emotion);
            Assert.AreEqual(half, set.BlinkHalf, emotion);
            Assert.AreEqual(closed, set.BlinkClosed, emotion);
            Assert.AreEqual(talk, set.Talk, emotion);
            Assert.IsTrue(set.HasBlink, emotion);
            Assert.IsTrue(set.HasTalk, emotion);
        }

        static void AssertStill(PortraitMap map, string emotion, string open)
        {
            var set = map.Resolve(emotion);
            Assert.AreEqual(open, set.Open, emotion);
            Assert.IsFalse(set.HasBlink, emotion);
            Assert.IsFalse(set.HasTalk, emotion);
        }
    }
}
