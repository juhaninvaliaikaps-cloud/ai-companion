using System;
using System.IO;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-103 phase 2: the mood-to-look table, reply-cue precedence and softening, the mood
    /// prompt line, the 2D idle clip, and the Intimate gate.
    /// </summary>
    public class CompanionMoodLookTests
    {
        [SetUp]
        public void SetUp()
        {
            IntimateExpressions.Reset();
            CompanionMoodNow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
            CompanionMoodNow.Reset();
        }

        static CompanionMood[] AllMoods()
        {
            return (CompanionMood[])Enum.GetValues(typeof(CompanionMood));
        }

        // ---- the mapping --------------------------------------------------------------------

        [TestCase(CompanionMood.Content, "", Emotion.Neutral, "")]
        [TestCase(CompanionMood.Happy, "smile", Emotion.Happy, "happy")]
        [TestCase(CompanionMood.Affectionate, "smile", Emotion.Happy, "")]
        [TestCase(CompanionMood.Flustered, "shy", Emotion.Shy, "")]
        [TestCase(CompanionMood.Annoyed, "pouty", Emotion.Sad, "")]
        [TestCase(CompanionMood.Sad, "concerned", Emotion.Concerned, "concerned")]
        [TestCase(CompanionMood.Anxious, "embarrassed_worried", Emotion.Concerned, "concerned")]
        [TestCase(CompanionMood.Sleepy, "sleepy", Emotion.Neutral, "")]
        public void Rest_MapsEachMood(CompanionMood mood, string expression, Emotion face, string clip)
        {
            var look = CompanionMoodLooks.Rest(mood, 0.6f, false);
            Assert.AreEqual(mood, look.Mood);
            Assert.AreEqual(expression, look.Expression);
            Assert.AreEqual(face, look.Face);
            Assert.AreEqual(clip, look.IdleClip);
            Assert.AreEqual(mood == CompanionMood.Content, look.IsNeutral);
        }

        [Test]
        public void Rest_EveryMoodHasARow_AndNoneIsAnIntimatePortrait()
        {
            foreach (var mood in AllMoods())
            {
                foreach (var active in new[] { false, true })
                {
                    var look = CompanionMoodLooks.Rest(mood, 1f, active);
                    Assert.IsNotNull(look.Expression, mood.ToString());
                    Assert.IsNotNull(look.IdleClip, mood.ToString());
                    Assert.IsFalse(IntimateExpressions.IsIntimateId(look.Expression), mood.ToString());
                    Assert.GreaterOrEqual(look.Intensity, 0f);
                    Assert.LessOrEqual(look.Intensity, 1f);
                }
            }
        }

        [Test]
        public void Rest_FaceGrowsWithStrength()
        {
            var weak = CompanionMoodLooks.Rest(CompanionMood.Sad, 0.3f, false);
            var strong = CompanionMoodLooks.Rest(CompanionMood.Sad, 1f, false);
            Assert.Greater(strong.Intensity, weak.Intensity);
            Assert.AreEqual(0.35f, strong.Intensity, 0.0001f);
            Assert.AreEqual(0.35f * 0.6f, CompanionMoodLooks.Rest(CompanionMood.Sad, float.NaN, false).Intensity, 0.0001f);
        }

        [Test]
        public void Now_ReadsTheKeepersMood()
        {
            Assert.IsTrue(CompanionMoodNow.Rest.IsNeutral);
            CompanionMoodNow.Set(CompanionMood.Sleepy, 2f);
            Assert.AreEqual(1f, CompanionMoodNow.Strength);
            Assert.AreEqual("sleepy", CompanionMoodNow.Rest.Expression);
            CompanionMoodNow.Reset();
            Assert.AreEqual(CompanionMood.Content, CompanionMoodNow.Shown);
            Assert.AreEqual(0f, CompanionMoodNow.Strength);
        }

        // ---- reply cues win, the mood shades them -----------------------------------------

        [TestCase(CompanionMood.Sad, "happy", "smile")]
        [TestCase(CompanionMood.Sad, "EXCITED", "smile")]
        [TestCase(CompanionMood.Sleepy, "laughing", "smile")]
        [TestCase(CompanionMood.Anxious, "happy", "smile")]
        [TestCase(CompanionMood.Annoyed, "happy", "smug")]
        [TestCase(CompanionMood.Happy, "sad", "concerned")]
        [TestCase(CompanionMood.Affectionate, "angry", "pouty")]
        [TestCase(CompanionMood.Sad, "sad", "")]
        [TestCase(CompanionMood.Sad, "surprised", "")]
        [TestCase(CompanionMood.Content, "happy", "")]
        [TestCase(CompanionMood.Sad, "", "")]
        public void Soften_PicksTheSofterVariant_OnlyWhenOneExists(CompanionMood mood, string cueFace, string expected)
        {
            Assert.AreEqual(expected, CompanionMoodLooks.Soften(mood, cueFace, false));
        }

        [Test]
        public void Shade_HappyCueWhileSad_PlaysTheSmile()
        {
            var happy = new DirectorDecision(DirectorAction.Emotion, string.Empty, Emotion.Happy, 0.8f, string.Empty);
            var shaded = AnimationDirectorModel.ShadeByMood(happy, CompanionMood.Sad, false, out var cueFace);
            Assert.AreEqual("happy", cueFace);
            Assert.AreEqual(DirectorAction.Emotion, shaded.Action);
            Assert.AreEqual("smile", shaded.Portrait);
            Assert.AreEqual(Emotion.Happy, shaded.Emotion);
            Assert.AreEqual(0.8f * CompanionMoodLooks.SoftenedIntensity, shaded.Intensity, 0.0001f);

            // A portrait word is shaded by its own name.
            var excited = new DirectorDecision(DirectorAction.Emotion, string.Empty, Emotion.Happy, 0.6f, string.Empty, portrait: "excited");
            Assert.AreEqual("smile", AnimationDirectorModel.ShadeByMood(excited, CompanionMood.Sleepy, false, out _).Portrait);
        }

        [Test]
        public void Shade_ExplicitCuesWithoutASofterVariant_GestureAndNeutral_AreUnchanged()
        {
            var surprised = new DirectorDecision(DirectorAction.Emotion, string.Empty, Emotion.Surprised, 0.7f, string.Empty);
            var same = AnimationDirectorModel.ShadeByMood(surprised, CompanionMood.Sad, false, out var face);
            Assert.AreEqual(string.Empty, face);
            Assert.AreEqual(Emotion.Surprised, same.Emotion);
            Assert.AreEqual(0.7f, same.Intensity, 0.0001f);

            var gesture = new DirectorDecision(DirectorAction.GestureState, "Wave", Emotion.Happy, 0.7f, "wave_hello");
            var waved = AnimationDirectorModel.ShadeByMood(gesture, CompanionMood.Sad, false, out face);
            Assert.AreEqual(string.Empty, face);
            Assert.AreEqual("Wave", waved.StateName);
            Assert.AreEqual(Emotion.Happy, waved.Emotion);

            var neutral = AnimationDirectorModel.Neutral();
            Assert.AreEqual(DirectorAction.Neutral, AnimationDirectorModel.ShadeByMood(neutral, CompanionMood.Sad, false, out face).Action);
            Assert.AreEqual(string.Empty, face);

            var happy = new DirectorDecision(DirectorAction.Emotion, string.Empty, Emotion.Happy, 0.8f, string.Empty);
            Assert.AreEqual(string.Empty, AnimationDirectorModel.ShadeByMood(happy, CompanionMood.Content, false, out face).Portrait);
            Assert.AreEqual(string.Empty, face);
        }

        // ---- the prompt line ----------------------------------------------------------------

        [Test]
        public void PromptLine_IsOneLine_AfterThePersona()
        {
            var line = CompanionMoodPrompt.Line(CompanionMood.Annoyed, 0.35f, false);
            StringAssert.StartsWith("Your current mood: annoyed, mild.", line);
            Assert.IsFalse(line.Contains("\n"));
            StringAssert.StartsWith("Your current mood: sad, moderate.", CompanionMoodPrompt.Line(CompanionMood.Sad, 0.6f, false));
            StringAssert.StartsWith("Your current mood: happy, strong.", CompanionMoodPrompt.Line(CompanionMood.Happy, 0.9f, false));
            StringAssert.StartsWith("Your current mood: content.", CompanionMoodPrompt.Line(CompanionMood.Content, 0f, false));

            var prompt = CompanionMoodPrompt.Append("You are Leia.  ", CompanionMood.Sleepy, 0.4f, false);
            Assert.AreEqual("You are Leia.\n" + CompanionMoodPrompt.Line(CompanionMood.Sleepy, 0.4f, false), prompt);
            Assert.AreEqual(CompanionMoodPrompt.Line(CompanionMood.Sleepy, 0.4f, false), CompanionMoodPrompt.Append(null, CompanionMood.Sleepy, 0.4f, false));

            CompanionMoodNow.Set(CompanionMood.Annoyed, 0.35f);
            StringAssert.EndsWith(CompanionMoodPrompt.Line(CompanionMood.Annoyed, 0.35f, false), CompanionMoodPrompt.AppendCurrent("Persona"));
        }

        // ---- the Intimate gate --------------------------------------------------------------

        [Test]
        public void Intimate_IsIgnoredEverywhere_WhileTheGateIsClosed()
        {
            Assert.AreEqual(CompanionMood.Content, CompanionMoodLooks.Effective(CompanionMood.Intimate, false));
            Assert.IsTrue(CompanionMoodLooks.Rest(CompanionMood.Intimate, 1f, false).IsNeutral);
            Assert.AreEqual(string.Empty, CompanionMoodLooks.Soften(CompanionMood.Intimate, "happy", false));
            var line = CompanionMoodPrompt.Line(CompanionMood.Intimate, 0.9f, false);
            StringAssert.StartsWith("Your current mood: content.", line);
            StringAssert.DoesNotContain("intimate", line.ToLowerInvariant());

            var happy = new DirectorDecision(DirectorAction.Emotion, string.Empty, Emotion.Happy, 0.8f, string.Empty);
            AnimationDirectorModel.ShadeByMood(happy, CompanionMood.Intimate, false, out var face);
            Assert.AreEqual(string.Empty, face);

            // The static path reads the gate too.
            CompanionMoodNow.Set(CompanionMood.Intimate, 0.9f);
            Assert.AreEqual(CompanionMood.Content, CompanionMoodNow.Effective);
            Assert.IsTrue(CompanionMoodNow.Rest.IsNeutral);
            StringAssert.DoesNotContain("intimate", CompanionMoodPrompt.AppendCurrent("Persona").ToLowerInvariant());
        }

        [Test]
        public void Intimate_CountsOnlyWhileTheGateIsOpen_AndNeverPicksAFingerPortrait()
        {
            IntimateExpressions.Configure(true, true, true);
            CompanionMoodNow.Set(CompanionMood.Intimate, 0.9f);

            Assert.AreEqual(CompanionMood.Intimate, CompanionMoodNow.Effective);
            var look = CompanionMoodNow.Rest;
            Assert.AreEqual("shy", look.Expression);
            Assert.IsFalse(IntimateExpressions.IsIntimateId(look.Expression));
            StringAssert.StartsWith("Your current mood: intimate, strong.", CompanionMoodPrompt.AppendCurrent(string.Empty));
            Assert.AreEqual(string.Empty, CompanionMoodNow.Soften("happy"));
        }

        // ---- the log line -------------------------------------------------------------------

        [Test]
        public void LogLine_NamesTheMoodAndWhatItPicked()
        {
            Assert.AreEqual("Mood expression: sad → concerned (rest face)", CompanionMoodLooks.LogLine(CompanionMood.Sad, "concerned", "rest face"));
            Assert.AreEqual("Mood expression: content → neutral", CompanionMoodLooks.LogLine(CompanionMood.Content, string.Empty, null));
        }

        // ---- the 2D idle clip ---------------------------------------------------------------

        static VideoAvatarClipSet Clips(bool withVariation)
        {
            var folder = Path.Combine("D:", "clips");
            var names = withVariation
                ? new[] { "idle.sbs.mp4", "idle_breeze.sbs.mp4", "happy.sbs.mp4", "concerned.sbs.mp4" }
                : new[] { "idle.sbs.mp4", "happy.sbs.mp4", "concerned.sbs.mp4" };
            var paths = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
                paths[i] = Path.Combine(folder, names[i]);
            var set = VideoAvatarManifest.Resolve(null, paths);
            set.VariationMinSeconds = 1f;
            set.VariationMaxSeconds = 1f;
            return set;
        }

        [Test]
        public void VideoIdle_MoodClipStandsInForAVariation_AndReturnsToTheIdle()
        {
            var set = Clips(false);
            var director = new VideoAvatarDirector(set, 1);

            // No mood clip and no variation: nothing is queued.
            director.Tick(2f);
            Assert.AreEqual(-1, director.Queued);

            director.MoodClip = "happy";
            director.Tick(1.1f);
            Assert.AreEqual(set.FindReaction("happy"), director.Queued);
            Assert.IsTrue(director.QueuedIsMood);
            Assert.IsFalse(director.QueuedIsReaction);

            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Variation, director.Act);
            Assert.AreEqual(set.FindReaction("happy"), director.Clip);
            director.OnEnded();
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void VideoIdle_ClipWithSound_UnknownClip_OrNoMood_IsNeverUsed()
        {
            var set = Clips(false);
            set.Clips[set.FindReaction("concerned")].Sound = true;
            var director = new VideoAvatarDirector(set, 1);

            director.MoodClip = "concerned";
            director.Tick(2f);
            Assert.AreEqual(-1, director.Queued);

            director.MoodClip = "not_a_clip";
            director.Tick(2f);
            Assert.AreEqual(-1, director.Queued);

            director.MoodClip = null;
            Assert.AreEqual(string.Empty, director.MoodClip);
            director.Tick(2f);
            Assert.AreEqual(-1, director.Queued);
        }

        [Test]
        public void VideoIdle_WithAPlainVariation_TheMoodClipIsMixedIn_AndNeverTwiceInARow()
        {
            var set = Clips(true);
            var happy = set.FindReaction("happy");
            var director = new VideoAvatarDirector(set, 7);
            director.MoodClip = "happy";

            var moodPicks = 0;
            var plainPicks = 0;
            var last = -1;
            for (var i = 0; i < 60; i++)
            {
                director.Tick(1.1f);
                var queued = director.Queued;
                Assert.GreaterOrEqual(queued, 0);
                if (queued == happy)
                {
                    Assert.IsTrue(director.QueuedIsMood);
                    Assert.AreNotEqual(happy, last, "the mood clip repeated");
                    moodPicks++;
                }
                else
                {
                    Assert.IsFalse(director.QueuedIsMood);
                    plainPicks++;
                }

                last = queued;
                director.OnBoundary(true);
                director.OnEnded();
            }

            Assert.Greater(moodPicks, 0);
            Assert.Greater(plainPicks, 0);
        }
    }
}
