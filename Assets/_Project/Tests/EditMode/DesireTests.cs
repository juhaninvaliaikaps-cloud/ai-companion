using System;
using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-170: her desire, which moves on its own, and her libido.</summary>
    public sealed class DesireTests
    {
        // ---- The cue's heat --------------------------------------------------------------------

        [Test]
        public void TheCueReadsHeat_AndAnUnknownWordIsNone()
        {
            var hot = AnimationCueParser.Parse("<animation emotion=\"flustered\" intensity=\"0.7\" heat=\"hot\" />\nWait...");
            Assert.AreEqual(CueParseStatus.Parsed, hot.Status);
            Assert.AreEqual(DesireHeat.Hot, hot.Cue.Heat);
            Assert.AreEqual("Wait...", hot.Text.Trim(), "the attribute never shows in the chat");

            Assert.AreEqual(DesireHeat.Release, AnimationCueParser.Parse("<animation emotion=\"happy\" heat=\"release\" />\nAh.").Cue.Heat);
            Assert.AreEqual(DesireHeat.Warm, AnimationCueParser.Parse("<animation emotion=\"happy\" heat='warm' />\nHm.").Cue.Heat);

            var odd = AnimationCueParser.Parse("<animation emotion=\"happy\" heat=\"spicy\" />\nHi.");
            Assert.AreEqual(CueParseStatus.Parsed, odd.Status, "an unknown heat never makes the tag unreadable");
            Assert.AreEqual(DesireHeat.None, odd.Cue.Heat);
            Assert.AreEqual(DesireHeat.None, AnimationCueParser.Parse("<animation emotion=\"happy\" />\nHi.").Cue.Heat);
        }

        [Test]
        public void AHotMomentOrARelease_IsAnIntimateMomentForInnocence()
        {
            var neutralSex = new AnimationCue(Emotion.Neutral, 0.5f, string.Empty, 0f, "neutral", null, LandingEvent.Neutral, null, DesireHeat.Hot);
            Assert.IsTrue(InnocenceMath.IsMoment(neutralSex, true), "the local model tags sex scenes neutral; heat still counts");
            Assert.IsFalse(InnocenceMath.IsMoment(neutralSex, false), "never while the gate is closed");

            var release = new AnimationCue(Emotion.Happy, 0.9f, string.Empty, 0f, "happy", null, LandingEvent.Neutral, null, DesireHeat.Release);
            Assert.IsTrue(InnocenceMath.IsMoment(release, true));

            var warm = new AnimationCue(Emotion.Happy, 0.5f, string.Empty, 0f, "happy", null, LandingEvent.Flirted, null, DesireHeat.Warm);
            Assert.IsFalse(InnocenceMath.IsMoment(warm, true), "a suggestive moment is not an intimate one");
        }

        // ---- Time -------------------------------------------------------------------------------

        [Test]
        public void Libido_SetsHerLevel_HowFastSheBuilds_AndHowSoonSheIsBack()
        {
            Assert.Less(DesireMath.RestLevel(10f), DesireMath.RestLevel(90f));
            Assert.Greater(DesireMath.BuildMinutes(10f), DesireMath.BuildMinutes(90f));
            Assert.Greater(DesireMath.RefractoryMinutes(10f), DesireMath.RefractoryMinutes(90f));
            Assert.AreEqual(40f, DesireMath.RestLevel(50f), 0.001f);
            Assert.AreEqual(DesireMath.DefaultLibido, DesireMath.ClampLibido(-1f), "no reading runs on the middle");
        }

        [Test]
        public void RelationshipLibido_AddsUpToFiftyForAPartnerOrHighAffection()
        {
            Assert.AreEqual(75f, DesireMath.RelationshipLibido(25f, 0f, true), 0.001f);
            Assert.AreEqual(100f, DesireMath.RelationshipLibido(80f, 0f, true), 0.001f);
            Assert.AreEqual(25f, DesireMath.RelationshipLibido(25f, 60f, false), 0.001f);
            Assert.AreEqual(50f, DesireMath.RelationshipLibido(25f, 80f, false), 0.001f);
            Assert.AreEqual(75f, DesireMath.RelationshipLibido(25f, 90f, false), 0.001f);
        }

        [Test]
        public void Time_BuildsTowardHerLevel_WithoutOvershooting()
        {
            var libido = 70f;
            var level = DesireMath.RestLevel(libido);
            var hour = DesireMath.AfterTime(10f, libido, 60d, 1f, false, false);
            Assert.Greater(hour, 10f);
            Assert.Less(hour, level);

            var week = DesireMath.AfterTime(10f, libido, 60d * 24d * 7d, 1f, false, false);
            Assert.LessOrEqual(week, level + 0.001f, "a long time away settles at her level");
            Assert.Greater(week, level - 1f);

            Assert.AreEqual(10f, DesireMath.AfterTime(10f, libido, 60d, 1f, false, true), 0.001f, "spent: no building");
            Assert.Greater(DesireMath.AfterTime(10f, libido, 60d, 1f, true, false), hour, "undressed lifts the level");
        }

        [Test]
        public void Time_FadesDesireAboveHerLevel_Back()
        {
            var faded = DesireMath.AfterTime(95f, 50f, 30d, 1f, false, false);
            Assert.Less(faded, 95f);
            Assert.Greater(faded, DesireMath.RestLevel(50f));
        }

        [Test]
        public void HerMood_ChangesThePace()
        {
            Assert.Less(DesireMath.MoodPace(CompanionMood.Sleepy, 1f), 1f);
            Assert.Less(DesireMath.MoodPace(CompanionMood.Sad, 1f), 1f);
            Assert.Greater(DesireMath.MoodPace(CompanionMood.Affectionate, 1f), 1f);
            Assert.AreEqual(1f, DesireMath.MoodPace(CompanionMood.Sleepy, 0f), 0.001f);

            var sleepy = DesireMath.AfterTime(10f, 50f, 60d, DesireMath.MoodPace(CompanionMood.Sleepy, 1f), false, false);
            var warm = DesireMath.AfterTime(10f, 50f, 60d, DesireMath.MoodPace(CompanionMood.Affectionate, 1f), false, false);
            Assert.Less(sleepy, warm);
        }

        // ---- Moments ----------------------------------------------------------------------------

        [Test]
        public void Moments_RaiseIt_MoreWhenHotter_AndWithAHigherLibido()
        {
            var warm = DesireMath.AfterMoment(30f, DesireHeat.Warm, 0.5f, 50f, 20f, false, null);
            var hot = DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 50f, 20f, false, null);
            Assert.Greater(warm, 30f);
            Assert.Greater(hot, warm);
            Assert.Greater(DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 90f, 20f, false, null), hot);
            Assert.AreEqual(30f, DesireMath.AfterMoment(30f, DesireHeat.None, 0.5f, 50f, 20f, false, null), 0.001f);
        }

        [Test]
        public void HerSecrets_RaiseIt_EvenWithoutHeat_ShyOnesLess()
        {
            var kink = DesireMath.AfterMoment(30f, DesireHeat.None, 0.5f, 50f, 20f, false, IntimateSecretGroup.Kink);
            var spot = DesireMath.AfterMoment(30f, DesireHeat.None, 0.5f, 50f, 20f, false, IntimateSecretGroup.Sensitive);
            var shy = DesireMath.AfterMoment(30f, DesireHeat.None, 0.5f, 50f, 20f, false, IntimateSecretGroup.Shy);
            Assert.Greater(kink, 30f);
            Assert.AreEqual(kink, spot, 0.001f);
            Assert.Greater(shy, 30f);
            Assert.Less(shy, kink);
            Assert.Greater(DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 50f, 20f, false, IntimateSecretGroup.Kink),
                DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 50f, 20f, false, null), "doing her kink is always more");
        }

        [Test]
        public void LowAffection_AndBeingSpent_DampIt()
        {
            var liked = DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 50f, 20f, false, null);
            var disliked = DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 50f, -60f, false, null);
            var spent = DesireMath.AfterMoment(30f, DesireHeat.Hot, 0.5f, 50f, 20f, true, null);
            Assert.Less(disliked, liked);
            Assert.Greater(disliked, 30f, "she still feels it");
            Assert.Less(spent, liked);
            Assert.AreEqual(0.4f, DesireMath.AffectionShare(-100f), 0.001f);
        }

        [Test]
        public void ARelease_SpendsIt_AndNothingLeavesTheRange()
        {
            Assert.AreEqual(90f * DesireMath.ReleaseKeeps, DesireMath.AfterRelease(90f), 0.001f);
            Assert.LessOrEqual(DesireMath.AfterMoment(99f, DesireHeat.Hot, 1f, 100f, 100f, false, IntimateSecretGroup.Kink), DesireMath.Max);
            Assert.AreEqual(DesireMath.Min, DesireMath.Clamp(float.NaN));
        }

        // ---- How she shows it -------------------------------------------------------------------

        [Test]
        public void TheLine_IsEmptyWhileTheGateIsClosed_AndAsksForHeatWhileOpen()
        {
            Assert.AreEqual(string.Empty, DesirePrompt.Line(80f, 50f, 50f, 20f, false, false));
            StringAssert.Contains("heat=\"hot\"", DesirePrompt.Line(5f, 50f, 50f, 20f, false, true));
            Assert.AreEqual(-1, DesirePrompt.Key(80f, 50f, 50f, 20f, false, false));
        }

        [Test]
        public void VeryAroused_APervertedOneTakesTheLead_AnInnocentOneTriesToHideIt()
        {
            var perverted = DesirePrompt.Line(80f, 50f, 95f, 20f, false, true);
            var innocent = DesirePrompt.Line(80f, 50f, 5f, 20f, false, true);
            StringAssert.Contains("takes the lead", perverted);
            StringAssert.Contains("provocative", perverted);
            StringAssert.Contains("barely hide", innocent);
            Assert.AreNotEqual(DesirePrompt.Key(80f, 50f, 95f, 20f, false, true), DesirePrompt.Key(80f, 50f, 5f, 20f, false, true));
        }

        [Test]
        public void TheLine_SaysWhenSheIsConflictedSpentOrRelentless()
        {
            StringAssert.Contains("frustrates her", DesirePrompt.Line(60f, 50f, 50f, -30f, false, true));
            StringAssert.DoesNotContain("frustrates her", DesirePrompt.Line(60f, 50f, 50f, 30f, false, true));
            StringAssert.Contains("spent", DesirePrompt.Line(8f, 50f, 50f, 30f, true, true));
            StringAssert.Contains("relentless", DesirePrompt.Line(30f, 95f, 50f, 30f, false, true));
            Assert.AreEqual(DesirePrompt.Key(50f, 50f, 50f, 30f, false, true), DesirePrompt.Key(55f, 50f, 50f, 30f, false, true), "same band, same line");
            Assert.AreNotEqual(DesirePrompt.Key(50f, 50f, 50f, 30f, false, true), DesirePrompt.Key(75f, 50f, 50f, 30f, false, true));
        }

        [Test]
        public void BandPushes_FlusterTheShy_AndWarmTheBold()
        {
            var shy = DesireMath.BandPushes(3, 5f);
            var bold = DesireMath.BandPushes(3, 95f);
            Assert.AreEqual(CompanionMood.Flustered, shy[0].Mood);
            Assert.AreEqual(CompanionMood.Intimate, bold[0].Mood);
            Assert.AreEqual(0, DesireMath.BandPushes(1, 50f).Length);
        }

        // ---- Saved -------------------------------------------------------------------------------

        [Test]
        public void TheState_SurvivesItsFile_AndLibidoComesFromTheUserThenTheReading()
        {
            var state = new DesireState { Desire = 63f, LastTicks = 1234L, RefractoryUntilTicks = 5678L, LibidoUserSet = true, UserLibido = 81f };
            var back = DesireState.FromJson(state.ToJson());
            Assert.AreEqual(63f, back.Desire, 0.001f);
            Assert.AreEqual(1234L, back.LastTicks);
            Assert.AreEqual(5678L, back.RefractoryUntilTicks);
            Assert.IsTrue(back.LibidoUserSet);
            Assert.AreEqual(81f, back.Libido(30f), 0.001f, "the user's setting first");

            var fresh = DesireState.FromJson("not json");
            Assert.IsTrue(fresh.IsNew);
            Assert.AreEqual(30f, fresh.Libido(30f), 0.001f, "then the reading");
            Assert.AreEqual(DesireMath.DefaultLibido, fresh.Libido(-1f), 0.001f, "then the middle");

            Assert.IsTrue(new DesireState { RefractoryUntilTicks = DateTime.UtcNow.AddMinutes(5).Ticks }.InRefractory(DateTime.UtcNow));
            Assert.IsFalse(new DesireState().InRefractory(DateTime.UtcNow));
        }

        [Test]
        public void TheFile_IsPartOfWipeAndExport()
        {
            Assert.IsTrue(LocalDataInventory.IsDesireName("desire.json"));
            Assert.IsTrue(LocalDataInventory.IsDesireName("desire-mira.json"));
            Assert.IsFalse(LocalDataInventory.IsDesireName("desired.json"));
            StringAssert.EndsWith("desire-mira.json", DesireStore.FileFor("folder", "mira"));
        }

        // ---- Libido is read with innocence, on the local model ----------------------------------

        [Test]
        public void TheInnocenceReading_AlsoGivesLibido()
        {
            float value;
            float libido;
            string reason;
            Assert.IsTrue(InnocenceReader.TryParse("{\"innocence\": 70, \"libido\": 85, \"reason\": \"Bold.\"}", out value, out libido, out reason));
            Assert.AreEqual(70f, value, 0.001f);
            Assert.AreEqual(85f, libido, 0.001f);
            Assert.IsTrue(InnocenceReader.TryParse("{\"innocence\": 70, \"libido\": 0.4}", out value, out libido, out reason));
            Assert.AreEqual(40f, libido, 0.001f, "a fraction means a share of 100");
            Assert.IsTrue(InnocenceReader.TryParse("{\"innocence\": 70}", out value, out libido, out reason));
            Assert.AreEqual(-1f, libido, "an old-style reply has no libido");
            StringAssert.Contains("libido", InnocenceReader.SystemPrompt);
            Assert.IsTrue(InnocenceReader.BuildRequest("Mira", "A swordswoman.").BlockCloud, "never the cloud");

            var state = new InnocenceState { ReadLibido = 77f };
            Assert.AreEqual(77f, InnocenceState.FromJson(state.ToJson()).ReadLibido, 0.001f);
            Assert.AreEqual(-1f, InnocenceState.FromJson("{\"version\":1,\"current\":40}").ReadLibido, "a file from before JUH-170");
        }

        // ---- Floating pieces ---------------------------------------------------------------------

        [Test]
        public void DesireChanges_FloatAsMagentaPieces_OnlyWhenEnabled()
        {
            var feedback = new RelationshipFeedback();
            Assert.IsFalse(feedback.AddDesire(new DesireNotice { Delta = 8f, Desire = 48f }, 0f, false), "gate closed: nothing");
            Assert.IsFalse(feedback.AddDesire(new DesireNotice { Delta = 0.2f, Desire = 48f }, 0f, true), "too small");

            Assert.IsTrue(feedback.AddDesire(new DesireNotice { Delta = 8f, Desire = 48f }, 0f, true));
            var up = feedback.Piece(feedback.Count - 1);
            Assert.AreEqual(FeedbackKind.DesireUp, up.Kind);
            Assert.AreEqual("+8", up.Amount);
            Assert.AreEqual("Desire 48", up.Label);
            Assert.IsTrue(up.IsDesire);

            Assert.IsTrue(feedback.AddDesire(new DesireNotice { Delta = -40f, Desire = 7f, Release = true }, 0.1f, true));
            var spent = feedback.Piece(feedback.Count - 1);
            Assert.AreEqual(FeedbackKind.DesireDown, spent.Kind);
            Assert.AreEqual("Release · desire 7", spent.Label);
        }

        [Test]
        public void ClosingTheGate_DropsOnlyTheDesirePieces()
        {
            var feedback = new RelationshipFeedback();
            feedback.AddSecret("honest compliments", 0f, true);
            feedback.AddDesire(new DesireNotice { Delta = 8f, Desire = 48f }, 0f, true);
            Assert.IsTrue(feedback.Has(true));
            feedback.Drop(true);
            Assert.IsFalse(feedback.Has(true));
            Assert.IsTrue(feedback.Has(false), "the secret piece stays");
            Assert.AreEqual(1, feedback.Count);
        }
    }
}
