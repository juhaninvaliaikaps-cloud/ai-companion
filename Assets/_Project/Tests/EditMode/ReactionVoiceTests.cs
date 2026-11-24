using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class ReactionVoiceTests
    {
        const string Sample = @"[
            {""reaction"":""happy_giggle"",""label"":""Happy giggle"",""portrait_file"":""teary_happy.png"",""wav"":""happy_giggle.wav"",""duration_s"":0.34},
            {""reaction"":""flustered_eh"",""label"":""Flustered eh"",""portrait_file"":""embarrassed_worried.png"",""wav"":""flustered_eh.wav"",""duration_s"":0.4},
            {""reaction"":""pouty_hmph"",""label"":""Pouty hmph"",""portrait_file"":""tsundere.png"",""wav"":""pouty_hmph.wav"",""duration_s"":0.5},
            {""reaction"":""surprised_oh"",""label"":""Surprised oh"",""portrait_file"":""shocked.png"",""wav"":""surprised_oh.wav"",""duration_s"":0.3},
            {""reaction"":""teasing_mmm"",""label"":""Teasing mmm"",""portrait_file"":""smug_wink.png"",""wav"":""teasing_mmm.wav"",""duration_s"":0.6},
            {""reaction"":""pleased_sigh_laugh"",""label"":""Pleased sigh"",""portrait_file"":""smug.png"",""wav"":""pleased_sigh_laugh.wav"",""duration_s"":0.7}
        ]";

        [SetUp]
        public void ResetGate()
        {
            IntimateExpressions.Reset();
            ReactionVoiceRequests.Reset();
            ReactionSpeechLead.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
            ReactionVoiceRequests.Reset();
            ReactionSpeechLead.Clear();
        }

        [Test]
        public void SamplePackMapsEachReactionToItsPortrait()
        {
            var clips = ReactionVoiceManifest.Parse(Sample);

            Assert.AreEqual(6, clips.Count);
            Assert.AreEqual("teary_happy", Clip(clips, "happy_giggle").Expression);
            Assert.AreEqual("embarrassed_worried", Clip(clips, "flustered_eh").Expression);
            Assert.AreEqual("tsundere", Clip(clips, "pouty_hmph").Expression);
            Assert.AreEqual("shocked", Clip(clips, "surprised_oh").Expression);
            Assert.AreEqual("smug_wink", Clip(clips, "teasing_mmm").Expression);
            Assert.AreEqual("smug", Clip(clips, "pleased_sigh_laugh").Expression);
            Assert.AreEqual(0.34d, Clip(clips, "happy_giggle").DurationSeconds, 0.0001d);
            Assert.AreEqual("happy_giggle.wav", Clip(clips, "happy_giggle").FileName);
        }

        [Test]
        public void ExpressionFieldWinsOverPortraitFile()
        {
            var clips = ReactionVoiceManifest.Parse(
                "{\"expression\":\"SMUG\",\"portrait_file\":\"teary_happy.png\",\"wav\":\"pleased_sigh_laugh.wav\",\"reaction\":\"sigh\"}");

            Assert.AreEqual(1, clips.Count);
            Assert.AreEqual("smug", clips[0].Expression);
            Assert.AreEqual("sigh", clips[0].Id);
        }

        [Test]
        public void WrapperArrayAndSingleObjectBothLoad()
        {
            var wrapped = ReactionVoiceManifest.Parse("{\"reactions\":" + Sample + "}");
            var one = ReactionVoiceManifest.Parse(
                "{\"id\":\"oh\",\"portrait_file\":\"shocked.png\",\"file\":\"surprised_oh\"}");

            Assert.AreEqual(6, wrapped.Count);
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual("shocked", one[0].Expression);
            Assert.AreEqual("surprised_oh.wav", one[0].FileName);
        }

        [Test]
        public void BrokenJsonAndUnsafePathsAddNothing()
        {
            Assert.AreEqual(0, ReactionVoiceManifest.Parse("").Count);
            Assert.AreEqual(0, ReactionVoiceManifest.Parse("{").Count);
            Assert.AreEqual(0, ReactionVoiceManifest.Parse("not json").Count);
            Assert.AreEqual(0, ReactionVoiceManifest.Parse(null).Count);

            var skipped = ReactionVoiceManifest.Parse(@"[
                {""portrait_file"":""../teary_happy.png"",""wav"":""happy_giggle.wav""},
                {""expression"":""smug"",""wav"":""../pleased.wav""},
                {""expression"":""smug"",""wav"":""folder/pleased.wav""},
                {""expression"":""smug"",""wav"":""notes.txt""},
                {""expression"":""smug"",""wav"":""C:pleased.wav""},
                {""expression"":""smug"",""wav"":""pleased sigh.wav""},
                {""wav"":""only_file.wav""}
            ]");

            Assert.AreEqual(0, skipped.Count);
        }

        [Test]
        public void MissingWavIsLeftOutAndTheOtherVariationRemains()
        {
            var parsed = ReactionVoiceManifest.Parse(@"[
                {""reaction"":""giggle_a"",""expression"":""teary_happy"",""wav"":""a.wav""},
                {""reaction"":""giggle_b"",""expression"":""teary_happy"",""wav"":""b.wav""}
            ]");
            var missing = new List<string>();
            var catalog = ReactionVoiceCatalog.FromClips(parsed, name => name == "b.wav", missing);

            Assert.AreEqual(1, catalog.ClipCount);
            Assert.AreEqual("a.wav", missing[0]);
            ReactionVoiceClip clip;
            Assert.IsTrue(catalog.TryPick("Teary_Happy", null, count => 0, out clip));
            Assert.AreEqual("giggle_b", clip.Id);
        }

        [Test]
        public void PickSkipsTheFileJustPlayedWhenAnotherExists()
        {
            var catalog = TwoGiggles();
            var calls = new List<int>();
            Func<int, int> next = count =>
            {
                calls.Add(count);
                return 0;
            };

            ReactionVoiceClip first;
            ReactionVoiceClip second;
            Assert.IsTrue(catalog.TryPick("teary_happy", null, next, out first));
            Assert.IsTrue(catalog.TryPick("teary_happy", first.FileName, next, out second));

            Assert.AreEqual("a.wav", first.FileName);
            Assert.AreEqual("b.wav", second.FileName);
            Assert.AreNotEqual(first.FileName, second.FileName);
        }

        [Test]
        public void OneVariationPlaysAgain()
        {
            var catalog = ReactionVoiceCatalog.FromClips(
                ReactionVoiceManifest.Parse("{\"expression\":\"sleepy\",\"wav\":\"yawn.wav\",\"reaction\":\"yawn\"}"),
                null,
                null);
            ReactionVoiceClip again;
            Assert.IsTrue(catalog.TryPick("sleepy", "yawn.wav", count => 0, out again));
            Assert.AreEqual("yawn.wav", again.FileName);
        }

        [Test]
        public void CuePlaysAgainAfterTheClipAndDoesNotStackWhileItPlays()
        {
            var session = new ReactionVoiceSession();
            var catalog = TwoGiggles();
            ReactionVoiceClip clip;
            Assert.IsTrue(session.TryChoose(catalog, "teary_happy", ReactionVoiceCause.Cue, 1d, count => 0, out clip));
            Assert.AreEqual("a.wav", clip.FileName);
            Assert.IsFalse(session.TryChoose(catalog, "teary_happy", ReactionVoiceCause.Cue, 1.1d, count => 0, out clip));
            Assert.IsTrue(session.TryChoose(catalog, "teary_happy", ReactionVoiceCause.Cue, 2d, count => 0, out clip));
            Assert.AreEqual("b.wav", clip.FileName);
        }

        [Test]
        public void DifferentExpressionReplacesTheOneStillPlaying()
        {
            var session = new ReactionVoiceSession();
            var catalog = ReactionVoiceCatalog.FromClips(ReactionVoiceManifest.Parse(Sample), null, null);
            ReactionVoiceClip clip;
            Assert.IsTrue(session.TryChoose(catalog, "teary_happy", ReactionVoiceCause.Cue, 0d, count => 0, out clip));
            Assert.IsTrue(session.TryChoose(catalog, "shocked", ReactionVoiceCause.Event, 0.1d, count => 0, out clip));
            Assert.AreEqual("surprised_oh", clip.Id);
            Assert.AreEqual("shocked", session.LastExpression);
        }

        [Test]
        public void MoodDoesNotRepeatTheSameRestFace()
        {
            var session = new ReactionVoiceSession();
            var catalog = ReactionVoiceCatalog.FromClips(
                ReactionVoiceManifest.Parse("{\"expression\":\"embarrassed_worried\",\"wav\":\"eh.wav\",\"reaction\":\"eh\",\"duration_s\":0.2}"),
                null,
                null);
            ReactionVoiceClip clip;
            Assert.IsTrue(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Mood, 0d, count => 0, out clip));
            Assert.IsFalse(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Mood, 5d, count => 0, out clip));
            Assert.IsFalse(session.TryChoose(catalog, "sleepy", ReactionVoiceCause.Mood, 5d, count => 0, out clip));
            Assert.IsTrue(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Cue, 5d, count => 0, out clip));
        }

        [Test]
        public void NeutralAndClosedGateStayQuiet()
        {
            var session = new ReactionVoiceSession();
            var catalog = ReactionVoiceCatalog.FromClips(
                ReactionVoiceManifest.Parse(@"[
                    {""expression"":""neutral"",""wav"":""hmm.wav"",""reaction"":""hmm""},
                    {""expression"":""finger_suck"",""wav"":""adult.wav"",""reaction"":""adult""},
                    {""expression"":""smug"",""wav"":""sigh.wav"",""reaction"":""sigh""}
                ]"),
                null,
                null);
            ReactionVoiceClip clip;
            Assert.IsFalse(session.TryChoose(catalog, "neutral", ReactionVoiceCause.Cue, 0d, count => 0, out clip));
            Assert.IsFalse(session.TryChoose(catalog, "", ReactionVoiceCause.Cue, 0d, count => 0, out clip));
            Assert.IsFalse(session.TryChoose(catalog, "finger_suck", ReactionVoiceCause.Event, 0d, count => 0, out clip));

            IntimateExpressions.Configure(true, true, true);
            Assert.IsTrue(session.TryChoose(catalog, "finger_suck", ReactionVoiceCause.Event, 0d, count => 0, out clip));
            Assert.AreEqual("adult", clip.Id);
        }

        [Test]
        public void FaceNameUsesThePortraitAndFallsBackToTheEmotion()
        {
            Assert.AreEqual("teary_happy", ReactionVoiceNames.FromFace("teary_happy.png", Emotion.Happy));
            Assert.AreEqual("happy", ReactionVoiceNames.FromFace("", Emotion.Happy));
            Assert.AreEqual(string.Empty, ReactionVoiceNames.FromFace("neutral", Emotion.Neutral));
            Assert.AreEqual(string.Empty, ReactionVoiceNames.FromFace("", Emotion.Neutral));
        }

        [Test]
        public void SameFrameKeepsTheStrongerCause()
        {
            ReactionVoiceRequests.Offer("smile", ReactionVoiceCause.Mood);
            ReactionVoiceRequests.Offer("teary_happy", ReactionVoiceCause.Cue);
            ReactionVoiceRequests.Offer("pouty", ReactionVoiceCause.Mood);

            string expression;
            ReactionVoiceCause cause;
            Assert.IsTrue(ReactionVoiceRequests.TryTake(out expression, out cause));
            Assert.AreEqual("teary_happy", expression);
            Assert.AreEqual(ReactionVoiceCause.Cue, cause);

            ReactionVoiceRequests.Offer("teary_happy", ReactionVoiceCause.Cue);
            ReactionVoiceRequests.Offer("tsundere", ReactionVoiceCause.Event);
            Assert.IsTrue(ReactionVoiceRequests.TryTake(out expression, out cause));
            Assert.AreEqual("tsundere", expression);
            Assert.AreEqual(ReactionVoiceCause.Event, cause);
            Assert.IsFalse(ReactionVoiceRequests.TryTake(out expression, out cause));
        }

        [Test]
        public void PortraitChangeLosesToACueAndBeatsAMoodInTheSameFrame()
        {
            ReactionVoiceRequests.Offer("smile", ReactionVoiceCause.Mood);
            ReactionVoiceRequests.Offer("embarrassed_worried", ReactionVoiceCause.Portrait);

            string expression;
            ReactionVoiceCause cause;
            Assert.IsTrue(ReactionVoiceRequests.TryTake(out expression, out cause));
            Assert.AreEqual("embarrassed_worried", expression);
            Assert.AreEqual(ReactionVoiceCause.Portrait, cause);

            ReactionVoiceRequests.Offer("embarrassed_worried", ReactionVoiceCause.Portrait);
            ReactionVoiceRequests.Offer("teary_happy", ReactionVoiceCause.Cue);
            ReactionVoiceRequests.Offer("smile", ReactionVoiceCause.Portrait);
            Assert.IsTrue(ReactionVoiceRequests.TryTake(out expression, out cause));
            Assert.AreEqual("teary_happy", expression);
            Assert.AreEqual(ReactionVoiceCause.Cue, cause);
        }

        [Test]
        public void PortraitCausePlaysAFaceAgainAfterTheClip()
        {
            var session = new ReactionVoiceSession();
            var catalog = ReactionVoiceCatalog.FromClips(
                ReactionVoiceManifest.Parse("{\"expression\":\"embarrassed_worried\",\"wav\":\"eh.wav\",\"reaction\":\"eh\",\"duration_s\":0.2}"),
                null,
                null);
            ReactionVoiceClip clip;
            Assert.IsTrue(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Mood, 0d, count => 0, out clip));
            Assert.IsFalse(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Portrait, 0.1d, count => 0, out clip));
            Assert.IsFalse(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Mood, 5d, count => 0, out clip));
            Assert.IsTrue(session.TryChoose(catalog, "embarrassed_worried", ReactionVoiceCause.Portrait, 5d, count => 0, out clip));
            Assert.AreEqual("eh", clip.Id);
        }

        [Test]
        public void PortraitWatchReportsOnlyANewExpression()
        {
            var watch = new PortraitExpressionWatch();
            string name;
            Assert.IsFalse(watch.TryChange("neutral", out name));
            Assert.IsFalse(watch.TryChange("", out name));
            Assert.IsTrue(watch.TryChange("Teary_Happy.png", out name));
            Assert.AreEqual("teary_happy", name);
            Assert.IsFalse(watch.TryChange("teary_happy", out name));
            Assert.IsFalse(watch.TryChange("neutral", out name));
            Assert.IsTrue(watch.TryChange("shy", out name));
            Assert.AreEqual("shy", name);
        }

        [Test]
        public void SpeechWaitsForTheCueClipAndNotLongerThanIt()
        {
            ReactionSpeechLead.Arm();
            Assert.IsTrue(ReactionSpeechLead.NeedsAnswer);
            Assert.IsTrue(ReactionSpeechLead.Blocks(1d));
            Assert.IsFalse(ReactionSpeechLead.Blocks(1d + ReactionSpeechLead.AckGraceSeconds));

            ReactionSpeechLead.Arm();
            ReactionSpeechLead.NotePlayback(2.4d);
            Assert.IsFalse(ReactionSpeechLead.NeedsAnswer);
            Assert.IsTrue(ReactionSpeechLead.Blocks(2d));
            Assert.IsFalse(ReactionSpeechLead.Blocks(2.4d));
            ReactionSpeechLead.NotePlayback(2.1d);
            Assert.IsTrue(ReactionSpeechLead.Blocks(2.2d));
            ReactionSpeechLead.NotePlayback(3d);
            Assert.IsTrue(ReactionSpeechLead.Blocks(2.5d));
            Assert.IsFalse(ReactionSpeechLead.Blocks(3d));

            ReactionSpeechLead.Clear();
            Assert.IsFalse(ReactionSpeechLead.Blocks(0d));
            Assert.AreEqual(0, ReactionSpeechLead.Serial);
        }

        [Test]
        public void ClipWindowNeverExceedsEightSeconds()
        {
            Assert.AreEqual(ReactionVoiceSession.DefaultSeconds, ReactionVoiceSession.ClampSeconds(0d));
            Assert.AreEqual(ReactionVoiceSession.MaxSeconds, ReactionVoiceSession.ClampSeconds(30d));
            Assert.AreEqual(0.34d, ReactionVoiceSession.ClampSeconds(0.34d), 0.0001d);
        }

        [Test]
        public void PackFolderPrefersTheRoamingFolder()
        {
            var seen = new List<string>();
            Func<string, bool> exists = path =>
            {
                seen.Add(path);
                return path.IndexOf("Roaming", StringComparison.OrdinalIgnoreCase) >= 0;
            };

            var folder = ReactionVoicePaths.Resolve(
                @"C:\Users\Test\AppData\Roaming",
                @"C:\Users\Test\AppData\LocalLow\AI Companion\Happi",
                exists);

            Assert.AreEqual(
                Path.Combine(@"C:\Users\Test\AppData\Roaming", "ProjectLeia", "reaction-voices"),
                folder);
            Assert.AreEqual(1, seen.Count);
        }

        [Test]
        public void MissingRoamingFolderUsesTheSaveFolder()
        {
            var save = Path.Combine(@"C:\Users\Test\AppData\LocalLow\AI Companion\Happi", "ProjectLeia", "reaction-voices");
            var folder = ReactionVoicePaths.Resolve(
                @"C:\Users\Test\AppData\Roaming",
                @"C:\Users\Test\AppData\LocalLow\AI Companion\Happi",
                path => string.Equals(path, save, StringComparison.OrdinalIgnoreCase));

            Assert.AreEqual(save, folder);
            Assert.IsNull(ReactionVoicePaths.Resolve("", "", path => true));
        }

        [Test]
        public void LoadSkipsAMissingFileAndKeepsTheRest()
        {
            var folder = Path.Combine(Path.GetTempPath(), "happi-reactions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "manifest.json"), @"[
                    {""reaction"":""happy_giggle"",""portrait_file"":""teary_happy.png"",""wav"":""happy_giggle.wav"",""duration_s"":0.34},
                    {""reaction"":""gone"",""expression"":""shocked"",""wav"":""missing.wav""}
                ]");
                File.WriteAllBytes(Path.Combine(folder, "happy_giggle.wav"), new byte[] { 1 });

                List<string> warnings;
                var catalog = ReactionVoiceCatalog.Load(folder, out warnings);
                ReactionVoiceClip clip;
                Assert.AreEqual(1, catalog.ClipCount);
                Assert.IsTrue(catalog.TryPick("teary_happy", null, count => 0, out clip));
                Assert.AreEqual("happy_giggle", clip.Id);
                Assert.IsFalse(catalog.Has("shocked"));
                StringAssert.Contains("missing.wav", string.Join("\n", warnings.ToArray()));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void UnreadableManifestStaysEmpty()
        {
            var folder = Path.Combine(Path.GetTempPath(), "happi-reactions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "manifest.json"), "{");
                List<string> warnings;
                var catalog = ReactionVoiceCatalog.Load(folder, out warnings);
                Assert.AreEqual(0, catalog.ClipCount);
                StringAssert.Contains("no playable", warnings[0]);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static ReactionVoiceCatalog TwoGiggles()
        {
            return ReactionVoiceCatalog.FromClips(
                ReactionVoiceManifest.Parse(@"[
                    {""reaction"":""giggle_a"",""expression"":""teary_happy"",""wav"":""a.wav"",""duration_s"":0.4},
                    {""reaction"":""giggle_b"",""expression"":""teary_happy"",""wav"":""b.wav"",""duration_s"":0.4}
                ]"),
                null,
                null);
        }

        static ReactionVoiceClip Clip(List<ReactionVoiceClip> clips, string id)
        {
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].Id == id)
                    return clips[i];
            }

            Assert.Fail("missing " + id);
            return default(ReactionVoiceClip);
        }
    }
}
