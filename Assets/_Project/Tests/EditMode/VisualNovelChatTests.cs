using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.NewUI;
using AICompanion.NewUI.VisualNovel;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    // JUH-108: sequenced visual-novel chat playback.

    sealed class FakeVnVoice : IVnVoice, ISpeechProgress
    {
        public bool CanSpeak { get; set; } = true;
        public bool IsSpeaking { get; set; }
        public bool SupportsPrefetch { get; set; } = true;
        public readonly List<string> Spoken = new List<string>();
        public readonly List<string> Prefetched = new List<string>();
        public int PlayedPrefetched;
        public int Stops;
        public float ClipTime;
        public float ClipLength;

        public void Speak(string text) { Spoken.Add(text); IsSpeaking = true; }
        public void Prefetch(string text) { Prefetched.Add(text); IsSpeaking = true; }
        public void PlayPrefetched() { PlayedPrefetched++; IsSpeaking = true; }
        public void Stop() { Stops++; IsSpeaking = false; }

        /// <summary>The line being synthesized or played has finished.</summary>
        public void Finish() { IsSpeaking = false; }

        public bool TryGetSpokenClip(out int serial, out string text, out float time, out float length)
        {
            serial = 1;
            text = Spoken.Count > 0 ? Spoken[Spoken.Count - 1] : string.Empty;
            time = ClipTime;
            length = ClipLength;
            return IsSpeaking && length > 0f;
        }
    }

    sealed class FakeCues : IVnCueSink, IVnFaceSink
    {
        public readonly List<AnimationCue> Fired = new List<AnimationCue>();
        public readonly List<string> Faces = new List<string>();
        public void Fire(AnimationCue cue) { Fired.Add(cue); }
        public void SetFace(string expression) { Faces.Add(expression); }
    }

    public sealed class VnScriptParserTests
    {
        static List<string> Describe(VnScript script)
        {
            var list = new List<string>();
            foreach (var s in script.Segments)
                list.Add((s.Kind == VnSegmentKind.Narration ? "N:" : "S:") + s.Text);
            return list;
        }

        [Test]
        public void MixedNarrationAndSpeech_AreSplitInOrder()
        {
            var script = VnScriptParser.Parse("*smiles* Hi there! *waves* See you soon.", true);
            CollectionAssert.AreEqual(new[] { "N:smiles", "S:Hi there!", "N:waves", "S:See you soon." }, Describe(script));
        }

        [Test]
        public void ReplyWithoutNarration_IsOneSpeechLine()
        {
            var script = VnScriptParser.Parse("Just talking to you today.", true);
            CollectionAssert.AreEqual(new[] { "S:Just talking to you today." }, Describe(script));
        }

        [Test]
        public void FaceBeatsKeepASentenceWholeAndDoNotReplaceTheMainCue()
        {
            const string raw = "<animation emotion=\"thinking\" gesture=\"wave\" />\nOne two, <animation portrait_beat=\"surprised\" /> three four, <animation portrait_beat=\"happy\" /> five six.";
            var script = VnScriptParser.Parse(raw, true);
            Assert.AreEqual(1, script.Segments.Count);
            var line = script.Segments[0];
            Assert.AreEqual("One two, three four, five six.", line.Text);
            Assert.AreEqual(2, line.FaceBeats.Count);
            Assert.AreEqual(9, line.FaceBeats[0].Offset);
            Assert.AreEqual(21, line.FaceBeats[1].Offset);
            Assert.AreEqual("happy", line.FaceBeats[1].Expression);
            var clean = AnimationCueParser.Parse(raw);
            Assert.AreEqual(line.Text, clean.Text);
            Assert.AreEqual(Emotion.Thinking, clean.Cue.Emotion);
            Assert.AreEqual("wave", clean.Cue.Gesture);
        }

        [Test]
        public void InvalidAndGatedBeatsStaySilentAndTheReplyKeepsOnlyTwo()
        {
            const string raw = "One <animation portrait_beat=\"unknown\" landed=\"complimented\" /> two <animation portrait_beat=\"finger_suck\" /> three <animation portrait_beat=\"concerned\" /> four <animation portrait_beat=\"smug\" /> five.\nLater, <animation portrait_beat=\"happy\" /> more.";
            var script = VnScriptParser.Parse(raw, true);
            Assert.AreEqual("One two three four five.", script.Segments[0].Text);
            Assert.AreEqual(2, script.Segments[0].FaceBeats.Count);
            Assert.AreEqual("concerned", script.Segments[0].FaceBeats[0].Expression);
            Assert.AreEqual(0, script.Segments[1].FaceBeats.Count);
            Assert.AreEqual(CueParseStatus.None, AnimationCueParser.Parse(raw).Status);
            Assert.AreEqual(0, VnScriptParser.Parse("One <animation portrait_beat=\"surp", false).Segments.Count);
        }

        [Test]
        public void MissingClosingAsterisk_EndsAtTheLineBreak()
        {
            var script = VnScriptParser.Parse("*sighs softly\nFine, you win.", true);
            CollectionAssert.AreEqual(new[] { "N:sighs softly", "S:Fine, you win." }, Describe(script));
        }

        [Test]
        public void MissingClosingAsteriskAtTheEnd_StaysNarration()
        {
            var script = VnScriptParser.Parse("Okay. *stretches lazily", true);
            CollectionAssert.AreEqual(new[] { "S:Okay.", "N:stretches lazily" }, Describe(script));
        }

        [Test]
        public void NestedAsterisks_ToggleWithoutLosingText()
        {
            var script = VnScriptParser.Parse("*she says *hi* softly*", true);
            CollectionAssert.AreEqual(new[] { "N:she says", "S:hi", "N:softly" }, Describe(script));
        }

        [Test]
        public void DoubleAsterisks_CountAsOneMarker()
        {
            var script = VnScriptParser.Parse("**grins** Okay!", true);
            CollectionAssert.AreEqual(new[] { "N:grins", "S:Okay!" }, Describe(script));
        }

        [Test]
        public void EmptyAndWhitespaceParts_AreDropped()
        {
            var script = VnScriptParser.Parse("**  ** \n\n Hello  ", true);
            CollectionAssert.AreEqual(new[] { "S:Hello" }, Describe(script));
            Assert.AreEqual(0, VnScriptParser.Parse(string.Empty, true).Segments.Count);
            Assert.AreEqual(0, VnScriptParser.Parse(null, true).Segments.Count);
        }

        [Test]
        public void CueTags_AreRemovedAndAttachedToTheirLine()
        {
            var raw = "<animation emotion=\"happy\" intensity=\"0.8\" />\n*smiles* Hi! <animation emotion=\"sad\" />*frowns* Oh no.";
            var script = VnScriptParser.Parse(raw, true);
            CollectionAssert.AreEqual(new[] { "N:smiles", "S:Hi!", "N:frowns", "S:Oh no." }, Describe(script));
            Assert.AreEqual(1, script.Segments[0].Cues.Count);
            Assert.AreEqual(Emotion.Happy, script.Segments[0].Cues[0].Emotion);
            Assert.AreEqual(0, script.Segments[1].Cues.Count);
            Assert.AreEqual(1, script.Segments[2].Cues.Count);
            Assert.AreEqual(Emotion.Sad, script.Segments[2].Cues[0].Emotion);
            StringAssert.DoesNotContain("animation", VnScriptParser.VisibleText(script));
        }

        [Test]
        public void TrailingCue_FiresAtTheEndOfTheReply()
        {
            var script = VnScriptParser.Parse("Bye! <animation emotion=\"happy\" />", true);
            Assert.AreEqual(1, script.Segments.Count);
            Assert.AreEqual(0, script.Segments[0].Cues.Count);
            Assert.AreEqual(1, script.TrailingCues.Count);
        }

        [Test]
        public void Streaming_ReturnsOnlyClosedLinesAndHoldsHalfTags()
        {
            Assert.AreEqual(0, VnScriptParser.Parse("*smi", false).Segments.Count);
            CollectionAssert.AreEqual(new[] { "N:smiles" }, Describe(VnScriptParser.Parse("*smiles* Hi th", false)));
            CollectionAssert.AreEqual(new[] { "N:smiles" }, Describe(VnScriptParser.Parse("*smiles* Hi <anim", false)));
            CollectionAssert.AreEqual(new[] { "N:smiles", "S:Hi there" }, Describe(VnScriptParser.Parse("*smiles* Hi there", true)));
        }

        [Test]
        public void LessThanSign_InSpeechIsKept()
        {
            CollectionAssert.AreEqual(new[] { "S:I <3 you" }, Describe(VnScriptParser.Parse("I <3 you", true)));
        }

        [Test]
        public void VeryLongReplies_AreCappedWithoutLosingText()
        {
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < 100; i++)
                sb.Append("Line ").Append(i).Append('\n');
            var script = VnScriptParser.Parse(sb.ToString(), true);
            Assert.AreEqual(VnScriptParser.MaxSegments, script.Segments.Count);
            StringAssert.EndsWith("Line 99", script.Segments[script.Segments.Count - 1].Text);
        }
    }

    public sealed class VnPlayerTests
    {
        const string FaceReply = "<animation emotion=\"thinking\" />\nOne two, <animation portrait_beat=\"surprised\" /> three four, <animation portrait_beat=\"happy\" /> five six.";

        [Test]
        public void VoicedFaceBeatsFollowTheClipWithoutSplittingSpeech()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var cues = new FakeCues();
            var player = Make(voice, cues, VnMode.Auto, false);
            player.Update(VnScriptParser.Parse(FaceReply, true), true);
            player.Tick(0.5f);
            Assert.IsEmpty(cues.Faces, "synthesis time is not voice playback");
            CollectionAssert.AreEqual(new[] { "One two, three four, five six." }, voice.Spoken);
            voice.ClipLength = 4f;
            voice.ClipTime = 1.7f;
            player.Tick(.5f);
            CollectionAssert.AreEqual(new[] { "surprised" }, cues.Faces);
            voice.ClipTime = 3.5f;
            player.Tick(.5f);
            CollectionAssert.AreEqual(new[] { "surprised", "happy" }, cues.Faces);
            Assert.AreEqual(1, cues.Fired.Count, "only the reply-wide cue reaches animation and mood");
        }

        [Test]
        public void MutedPlaybackTimesTheBeatsAndShowAllUsesOnlyTheLatestPose()
        {
            var voice = new FakeVnVoice { CanSpeak = false };
            var cues = new FakeCues();
            var player = Make(voice, cues, VnMode.Auto, false);
            var script = VnScriptParser.Parse(FaceReply, true);
            player.Update(script, true);
            var duration = Timing().ReadSeconds(script.Segments[0]);
            player.Tick(.45f * duration);
            CollectionAssert.AreEqual(new[] { "surprised" }, cues.Faces);
            player.ShowAll();
            CollectionAssert.AreEqual(new[] { "surprised", "happy" }, cues.Faces);
            player.Tick(.8f);
            Assert.IsNull(cues.Faces[cues.Faces.Count - 1], "finished beats yield back to normal portrait behavior");
            Assert.IsEmpty(voice.Spoken);
        }

        [TearDown]
        public void ClearReactionLead()
        {
            ReactionSpeechLead.Clear();
        }

        static VnTiming Timing()
        {
            return new VnTiming
            {
                NarrationSecondsPerChar = 0.01f,
                SilentSpeechSecondsPerChar = 0.01f,
                MinSeconds = 0.5f,
                MaxSeconds = 1f,
                VoiceStartGrace = 1f
            };
        }

        static VnPlayer Make(FakeVnVoice voice, FakeCues cues, VnMode mode, bool narration)
        {
            var player = new VnPlayer(voice, cues, Timing()) { Mode = mode, VoiceNarration = narration };
            player.Begin();
            return player;
        }

        static void RunAuto(VnPlayer player, FakeVnVoice voice)
        {
            for (var i = 0; i < 200 && !player.Finished; i++)
            {
                player.Tick(0.25f);
                if (voice.IsSpeaking)
                {
                    player.Tick(0.01f);
                    voice.Finish();
                }
            }
        }

        [Test]
        public void VoiceNarrationOff_RequestsTtsOnlyForSpeech()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("*smiles* Hello! *waves* Goodbye.", true), true);
            RunAuto(player, voice);
            Assert.IsTrue(player.Finished);
            CollectionAssert.AreEqual(new[] { "Hello!", "Goodbye." }, voice.Spoken);
        }

        [Test]
        public void VoiceNarrationOn_SpeaksNarrationToo()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, true);
            player.Update(VnScriptParser.Parse("*smiles* Hello!", true), true);
            RunAuto(player, voice);
            CollectionAssert.AreEqual(new[] { "smiles", "Hello!" }, voice.Spoken);
        }

        [Test]
        public void StepMode_NextAdvancesExactlyOneLine()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Step, false);
            player.Update(VnScriptParser.Parse("One. *two* Three. *four*", true), true);
            Assert.AreEqual(0, player.Current);
            Assert.AreEqual(1, player.Revealed);
            voice.Finish();
            for (var i = 0; i < 10; i++)
                player.Tick(0.5f);
            Assert.AreEqual(0, player.Current, "Step mode waits for Next");
            Assert.IsTrue(player.WaitingForNext);

            player.Next();
            Assert.AreEqual(1, player.Current);
            Assert.AreEqual(2, player.Revealed);
            player.Next();
            Assert.AreEqual(2, player.Current);
            player.Next();
            Assert.AreEqual(3, player.Current);
            player.Next();
            Assert.AreEqual(3, player.Current, "no line after the last one");
        }

        [Test]
        public void StepMode_PrefetchesTheNextSpeechLineWhileTheUserReads()
        {
            var voice = new FakeVnVoice();
            var player = Make(voice, new FakeCues(), VnMode.Step, false);
            player.Update(VnScriptParser.Parse("Hello. *leans closer* How are you?", true), true);
            CollectionAssert.AreEqual(new[] { "Hello." }, voice.Spoken);
            voice.Finish();
            player.Tick(0.1f);
            Assert.IsTrue(player.WaitingForNext);
            CollectionAssert.AreEqual(new[] { "How are you?" }, voice.Prefetched, "the next spoken line is synthesized ahead");
            Assert.AreEqual(2, player.PrefetchedIndex);

            voice.Finish();
            player.Next();
            Assert.AreEqual(1, player.Current);
            Assert.AreEqual(0, voice.PlayedPrefetched, "narration is not voiced and keeps the held line");
            player.Next();
            Assert.AreEqual(2, player.Current);
            Assert.AreEqual(1, voice.PlayedPrefetched, "Next plays the prefetched line at once");
            CollectionAssert.AreEqual(new[] { "Hello." }, voice.Spoken, "it is not synthesized a second time");
        }

        [Test]
        public void NextWhileSpeaking_StopsTheCurrentLine()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Step, false);
            player.Update(VnScriptParser.Parse("First line.\nSecond line.", true), true);
            Assert.IsTrue(voice.IsSpeaking);
            player.Next();
            Assert.AreEqual(1, voice.Stops >= 1 ? 1 : 0);
            CollectionAssert.AreEqual(new[] { "First line.", "Second line." }, voice.Spoken);
        }

        [Test]
        public void Cue_FiresAtItsLineNotAtTheStartOfTheReply()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var cues = new FakeCues();
            var player = Make(voice, cues, VnMode.Step, false);
            player.Update(VnScriptParser.Parse("Hello. <animation emotion=\"sad\" />*looks down* I missed you.", true), true);
            Assert.AreEqual(0, cues.Fired.Count, "the first line has no cue, so nothing fires yet");
            player.Next();
            Assert.AreEqual(1, cues.Fired.Count);
            Assert.AreEqual(Emotion.Sad, cues.Fired[0].Emotion);
            player.Next();
            Assert.AreEqual(1, cues.Fired.Count, "a cue fires once");
        }

        [Test]
        public void ReplyWithoutAnyCue_ReturnsToNeutralAtTheEnd()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var cues = new FakeCues();
            var player = Make(voice, cues, VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("Just words.", true), true);
            RunAuto(player, voice);
            Assert.AreEqual(1, cues.Fired.Count);
            Assert.AreEqual(Emotion.Neutral, cues.Fired[0].Emotion);
        }

        [Test]
        public void ShowAll_RevealsEverythingStopsTheVoiceAndJumpsToTheLastCue()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var cues = new FakeCues();
            var player = Make(voice, cues, VnMode.Step, false);
            player.Update(VnScriptParser.Parse("A. <animation emotion=\"happy\" />B. <animation emotion=\"sad\" />C.", true), true);
            player.ShowAll();
            Assert.IsTrue(player.Finished);
            Assert.AreEqual(3, player.Revealed);
            Assert.GreaterOrEqual(voice.Stops, 1);
            Assert.AreEqual(1, cues.Fired.Count);
            Assert.AreEqual(Emotion.Sad, cues.Fired[0].Emotion);
        }

        [Test]
        public void Muted_LinesAreReadNotSpoken()
        {
            var voice = new FakeVnVoice { CanSpeak = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("One.\nTwo.", true), true);
            RunAuto(player, voice);
            Assert.IsTrue(player.Finished);
            Assert.AreEqual(0, voice.Spoken.Count);
            Assert.AreEqual(0, voice.Prefetched.Count);
        }

        [Test]
        public void BlockedVoice_FallsBackToReadingTime()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("One.\nTwo.", true), true);
            voice.IsSpeaking = false; // the route refused: speech never starts
            for (var i = 0; i < 40 && !player.Finished; i++)
            {
                player.Tick(0.25f);
                voice.IsSpeaking = false;
            }

            Assert.IsTrue(player.Finished);
        }

        [Test]
        public void Streaming_LinesPlayAsTheyClose()
        {
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("*smiles* Hel", false), false);
            Assert.AreEqual(0, player.Current);
            Assert.IsFalse(player.Finished);
            player.Tick(1f);
            player.Update(VnScriptParser.Parse("*smiles* Hello!", true), true);
            Assert.AreEqual(1, player.Current);
            CollectionAssert.AreEqual(new[] { "Hello!" }, voice.Spoken);
        }

        [Test]
        public void SpokenLineWithACue_ArmsTheReactionBeforeTheLine()
        {
            ReactionSpeechLead.Clear();
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("<animation emotion=\"happy\" />Hello.", true), true);

            Assert.IsTrue(ReactionSpeechLead.NeedsAnswer);
            Assert.IsTrue(ReactionSpeechLead.Blocks(0d));
            CollectionAssert.AreEqual(new[] { "Hello." }, voice.Spoken);
            ReactionSpeechLead.NotePlayback(0.4d);
            Assert.IsTrue(ReactionSpeechLead.Blocks(0.1d));
            Assert.IsFalse(ReactionSpeechLead.Blocks(0.4d));
        }

        [Test]
        public void NarrationCue_KeepsTheLeadUntilTheSpokenLine()
        {
            ReactionSpeechLead.Clear();
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Step, false);
            player.Update(VnScriptParser.Parse("<animation emotion=\"happy\" />*smiles* Hello.", true), true);

            Assert.AreEqual(0, voice.Spoken.Count);
            Assert.IsTrue(ReactionSpeechLead.NeedsAnswer);
            var serial = ReactionSpeechLead.Serial;
            ReactionSpeechLead.NotePlayback(2d);
            player.Next();

            Assert.AreEqual(serial, ReactionSpeechLead.Serial);
            CollectionAssert.AreEqual(new[] { "Hello." }, voice.Spoken);
            Assert.IsTrue(ReactionSpeechLead.Blocks(0.5d));
        }

        [Test]
        public void SpokenLineWithoutACue_DoesNotArmALead()
        {
            ReactionSpeechLead.Clear();
            var voice = new FakeVnVoice { SupportsPrefetch = false };
            var player = Make(voice, new FakeCues(), VnMode.Auto, false);
            player.Update(VnScriptParser.Parse("Hello.", true), true);

            Assert.AreEqual(0, ReactionSpeechLead.Serial);
            Assert.IsFalse(ReactionSpeechLead.Blocks(0d));
            CollectionAssert.AreEqual(new[] { "Hello." }, voice.Spoken);
        }

        [Test]
        public void Render_HighlightsOnlyTheCurrentLineAndItalicisesNarration()
        {
            var script = VnScriptParser.Parse("*smiles* Hi <b>there</b>.", true);
            var text = VnRender.Dialogue(script.Segments, 2, 1, new HappiTheme());
            Assert.AreEqual(1, text.Split(new[] { "<mark=" }, System.StringSplitOptions.None).Length - 1);
            StringAssert.Contains("<i><color=", text);
            StringAssert.Contains("<noparse>Hi <b>there</b>.</noparse>", text);
            Assert.AreEqual(string.Empty, VnRender.Dialogue(script.Segments, 0, -1, new HappiTheme()));
        }
    }

    public sealed class VisualNovelPromptTests
    {
        [TearDown]
        public void Reset()
        {
            NewUiSwitch.Reset();
        }

        [Test]
        public void PromptIsUnchangedWhileTheNewUiIsOff()
        {
            NewUiSwitch.Reset();
            var off = AICompanion.AnimationDirector.DirectorPrompt.Append("persona", "suffix");
            StringAssert.DoesNotContain("asterisks", off);
            NewUiSwitch.Set(true);
            var on = AICompanion.AnimationDirector.DirectorPrompt.Append("persona", "suffix");
            StringAssert.Contains("*asterisks*", on);
            StringAssert.StartsWith(off, on.Substring(0, off.Length));
        }
    }
}
