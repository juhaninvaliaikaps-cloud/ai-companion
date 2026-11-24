using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-122: critical hits in the relationship math, and the rules for the floating pieces.</summary>
    public sealed class RelationshipFeedbackTests
    {
        static RelationshipChange Change(LandingEvent landing, float delta, bool critical = false, float familiarity = 50f)
        {
            return new RelationshipChange
            {
                Landing = landing,
                Affection = 20f + delta,
                AffectionDelta = delta,
                Familiarity = familiarity,
                Critical = critical,
            };
        }

        static RelationshipNotice Notice(RelationshipChange change, int announcedBand = 2)
        {
            return RelationshipNotice.From(change, announcedBand);
        }

        // ---- Critical hits --------------------------------------------------------------------

        [Test]
        public void Crit_FromIntensity085_DoublesTheAffectionChange()
        {
            Assert.AreEqual(2f, RelationshipMath.CritMultiplier);
            Assert.AreEqual(0.85f, RelationshipMath.CritIntensity);

            var normal = RelationshipMath.Land(LandingEvent.Complimented, 0.84f, 20f, 60f);
            var crit = RelationshipMath.Land(LandingEvent.Complimented, 0.85f, 20f, 60f);
            Assert.IsFalse(normal.Critical);
            Assert.IsTrue(crit.Critical);

            // Same table value and standing, just the multiplier (intensity 0.85 vs 0.84 scales by 1.35 / 1.34).
            var plain = normal.AffectionDelta / 1.34f * 1.35f;
            Assert.AreEqual(plain * RelationshipMath.CritMultiplier, crit.AffectionDelta, 0.001f);
            Assert.AreEqual(normal.FamiliarityDelta, crit.FamiliarityDelta, 0.0001f, "a crit changes affection only");
            StringAssert.Contains("complimented (critical)", crit.LogLine());
            StringAssert.DoesNotContain("critical", normal.LogLine());
        }

        [Test]
        public void Crit_DropsToo_AndKeepsTheClamps()
        {
            var drop = RelationshipMath.Land(LandingEvent.Insulted, 1f, 0f, 0f);
            Assert.IsTrue(drop.Critical);
            Assert.AreEqual(-30f, drop.AffectionDelta, 0.001f);

            var floor = RelationshipMath.Land(LandingEvent.Insulted, 1f, -95f, 0f);
            Assert.AreEqual(-100f, floor.Affection);
            var top = RelationshipMath.Land(LandingEvent.Comforted, 1f, 98f, 100f);
            Assert.AreEqual(99.147f, top.Affection, 0.01f);
        }

        [Test]
        public void Crit_NeverForNeutral_NoRandom_AndTheSecretHook()
        {
            var neutral = RelationshipMath.Land(LandingEvent.Neutral, 1f, 10f, 10f);
            Assert.IsFalse(neutral.Critical, "an ordinary reply is never a crit");
            Assert.AreEqual(0f, neutral.AffectionDelta);

            for (var i = 0; i < 20; i++)
                Assert.IsFalse(RelationshipMath.Land(LandingEvent.Teased, 0.5f, 0f, 50f + i).Critical, "no random crits");

            // JUH-123's secret= hit makes a crit at any intensity.
            Assert.IsTrue(RelationshipMath.IsCritical(LandingEvent.Complimented, 0.2f, true));
            Assert.IsTrue(RelationshipMath.Land(LandingEvent.Complimented, 0.2f, 0f, 50f, true).Critical);
            Assert.IsFalse(RelationshipMath.IsCritical(LandingEvent.Neutral, 1f, true));
            Assert.IsFalse(RelationshipMath.IsCritical(LandingEvent.Complimented, float.NaN, false));
        }

        [Test]
        public void History_MarksACrit()
        {
            var crit = RelationshipMath.Land(LandingEvent.Complimented, 1f, 20f, 60f);
            StringAssert.StartsWith("Critical: A compliment landed well: affection +", RelationshipHistory.Describe(crit));
            var normal = RelationshipMath.Land(LandingEvent.Complimented, 0.5f, 20f, 60f);
            StringAssert.StartsWith("A compliment landed well", RelationshipHistory.Describe(normal));
        }

        // ---- The notice -----------------------------------------------------------------------

        [Test]
        public void Notice_CarriesTheNameAndANewBandOnlyUpwards()
        {
            var notice = Notice(Change(LandingEvent.Dismissed, -2.4f, false, 70f), 2);
            Assert.AreEqual("Brushed off", notice.LandingName);
            Assert.IsTrue(notice.NewBand, "70 is the 'very well' band");
            Assert.AreEqual(3, notice.Band);

            Assert.IsFalse(Notice(Change(LandingEvent.Teased, 1f, false, 69f), 2).NewBand);
            Assert.IsFalse(Notice(Change(LandingEvent.Teased, 1f, false, 40f), 3).NewBand, "never a band downwards");
            Assert.AreEqual(0, RelationshipCard.KnowingBand(9.9f));
            Assert.AreEqual(1, RelationshipCard.KnowingBand(10f));
            Assert.AreEqual(2, RelationshipCard.KnowingBand(35f));
            Assert.AreEqual("you know each other well", RelationshipCard.Knowing(50f), "the card text is unchanged");
        }

        [Test]
        public void ShortNames_ForEveryLanding()
        {
            Assert.AreEqual("Compliment", RelationshipHistory.ShortName(LandingEvent.Complimented));
            Assert.AreEqual("Brushed off", RelationshipHistory.ShortName(LandingEvent.Dismissed));
            Assert.AreEqual(string.Empty, RelationshipHistory.ShortName(LandingEvent.Neutral));
            foreach (LandingEvent landing in System.Enum.GetValues(typeof(LandingEvent)))
            {
                if (landing != LandingEvent.Neutral)
                    Assert.IsNotEmpty(RelationshipHistory.ShortName(landing), landing.ToString());
            }
        }

        // ---- Which changes show ------------------------------------------------------------------

        [Test]
        public void Shows_FromHalfAPoint_CritsAlways_NeverNeutral()
        {
            Assert.IsTrue(RelationshipFeedback.ShowsAffection(Change(LandingEvent.Complimented, 0.5f)));
            Assert.IsTrue(RelationshipFeedback.ShowsAffection(Change(LandingEvent.Insulted, -0.5f)));
            Assert.IsFalse(RelationshipFeedback.ShowsAffection(Change(LandingEvent.Complimented, 0.49f)));
            Assert.IsTrue(RelationshipFeedback.ShowsAffection(Change(LandingEvent.Complimented, 0.2f, true)), "a crit always shows");
            Assert.IsFalse(RelationshipFeedback.ShowsAffection(Change(LandingEvent.Neutral, 0f)));
        }

        [Test]
        public void Pieces_TextAndKinds()
        {
            var feedback = new RelationshipFeedback();
            Assert.AreEqual(1, feedback.Add(Notice(Change(LandingEvent.Complimented, 1.5f)), 0f, true));
            var piece = feedback.Piece(0);
            Assert.AreEqual(FeedbackKind.Up, piece.Kind);
            Assert.AreEqual("+1.5", piece.Amount);
            Assert.AreEqual("Compliment", piece.Label);

            feedback.Add(Notice(Change(LandingEvent.Dismissed, -2.4f)), 0.1f, true);
            Assert.AreEqual(FeedbackKind.Down, feedback.Piece(1).Kind);
            Assert.AreEqual("-2.4", feedback.Piece(1).Amount);

            feedback.Add(Notice(Change(LandingEvent.Complimented, 4.8f, true)), 0.2f, true);
            Assert.AreEqual(FeedbackKind.CritUp, feedback.Piece(2).Kind);
            Assert.AreEqual("CRITICAL", RelationshipFeedback.CriticalLabel);
            feedback.Add(Notice(Change(LandingEvent.Insulted, -12f, true)), 0.3f, true);
            Assert.AreEqual(FeedbackKind.CritDown, feedback.Piece(3).Kind);

            Assert.AreEqual("You know each other well now", RelationshipFeedback.BandText(2));
            Assert.AreEqual("Secret found: praise for her design work", RelationshipFeedback.SecretText(" praise for her design work "));
        }

        [Test]
        public void FamiliarityShowsOnlyAtANewBand()
        {
            var feedback = new RelationshipFeedback();
            Assert.AreEqual(0, feedback.Add(Notice(Change(LandingEvent.Neutral, 0f, false, 50f), 2), 0f, true), "a small step shows nothing");
            Assert.AreEqual(2, feedback.Add(Notice(Change(LandingEvent.Teased, 1f, false, 70f), 2), 0f, true), "the landing and the new band");
            Assert.AreEqual(FeedbackKind.Familiarity, feedback.Piece(1).Kind);
            Assert.AreEqual("You know each other very well now", feedback.Piece(1).Label);
            Assert.AreEqual(string.Empty, feedback.Piece(1).Amount);
        }

        [Test]
        public void AtMostTwoNormalPieces_OldestFadesFirst_CritsNotCounted()
        {
            var feedback = new RelationshipFeedback();
            feedback.Add(Notice(Change(LandingEvent.Thanked, 0.8f)), 0f, true);
            feedback.Add(Notice(Change(LandingEvent.Complimented, 1.5f)), 0.1f, true);
            feedback.Add(Notice(Change(LandingEvent.Complimented, 4.8f, true)), 0.2f, true);
            Assert.AreEqual(2, feedback.NormalShown, "the crit is not counted");

            feedback.Add(Notice(Change(LandingEvent.Teased, 1.1f)), 0.3f, true);
            Assert.AreEqual(2, feedback.NormalShown);
            Assert.AreEqual(0.3f, feedback.Piece(0).RetiredAt, 0.0001f, "the oldest is pushed out");
            Assert.IsTrue(float.IsNaN(feedback.Piece(2).RetiredAt), "the crit is never pushed out");

            feedback.Tick(0.3f + RelationshipFeedback.RetireSeconds);
            Assert.AreEqual(3, feedback.Count, "the pushed-out piece is gone after its short fade");
            Assert.AreEqual("Compliment", feedback.Piece(0).Label);

            feedback.Tick(10f);
            Assert.AreEqual(0, feedback.Count);
        }

        [Test]
        public void ABurstOfCrits_StaysInThePool()
        {
            var feedback = new RelationshipFeedback();
            for (var i = 0; i < 10; i++)
                feedback.Add(Notice(Change(LandingEvent.Complimented, 4f, true)), i * 0.01f, true);
            Assert.AreEqual(RelationshipFeedback.Capacity, feedback.Count);
            Assert.AreEqual(0.04f, feedback.Piece(0).StartTime, 0.0001f, "the oldest went first");
        }

        [Test]
        public void NothingShows_WhileOff()
        {
            var feedback = new RelationshipFeedback();
            Assert.AreEqual(0, feedback.Add(Notice(Change(LandingEvent.Complimented, 4.8f, true, 80f), 0), 0f, false));
            Assert.IsFalse(feedback.AddSecret("praise", 0f, false));
            Assert.AreEqual(0, feedback.Count);
        }

        /// <summary>The view's slot guard, frame by frame: slot i shows piece i, as FloatingFeedbackView does.</summary>
        static int RunFrames(RelationshipFeedback feedback, int[] shown, float from, float to, float step)
        {
            var setups = 0;
            for (var now = from; now <= to; now += step)
            {
                feedback.Tick(now);
                for (var i = 0; i < feedback.Count; i++)
                {
                    var piece = feedback.Piece(i);
                    if (!RelationshipFeedback.NeedsSetup(shown[i], piece))
                        continue;
                    shown[i] = piece.Id;
                    setups++;
                }

                for (var i = feedback.Count; i < shown.Length; i++)
                    shown[i] = 0;
            }

            return setups;
        }

        [Test]
        public void APiece_IsSetUpOnce_NotEveryFrame()
        {
            var feedback = new RelationshipFeedback();
            var shown = new int[RelationshipFeedback.Capacity];
            feedback.Add(Notice(Change(LandingEvent.Complimented, 1.5f)), 0f, true);
            Assert.AreEqual(1, RunFrames(feedback, shown, 0f, 1f, 1f / 60f), "a normal piece: once over 60 frames");

            feedback.Add(Notice(Change(LandingEvent.Complimented, 4.8f, true)), 1f, true);
            Assert.AreEqual(1, RunFrames(feedback, shown, 1f, 1.4f, 1f / 60f), "the crit: once; the normal piece is not set up again");
        }

        [Test]
        public void TwoPiecesInOneFrame_HaveTheirOwnIds_AndAShiftSetsUpTheRightText()
        {
            var feedback = new RelationshipFeedback();
            var shown = new int[RelationshipFeedback.Capacity];
            feedback.Add(Notice(Change(LandingEvent.Thanked, 0.8f)), 0f, true);
            feedback.Add(Notice(Change(LandingEvent.Teased, 1f, false, 70f), 2), 0.5f, true);
            Assert.AreEqual(3, feedback.Count, "the thanks, the tease and the new band");
            Assert.AreEqual(feedback.Piece(1).StartTime, feedback.Piece(2).StartTime, "the landing and the band start in the same frame");
            Assert.AreNotEqual(feedback.Piece(1).Id, feedback.Piece(2).Id);
            Assert.AreEqual(3, RunFrames(feedback, shown, 0.5f, 0.6f, 1f / 60f));

            // The thanks was pushed out (two normal pieces at most) and goes; the others move down a slot.
            var setups = RunFrames(feedback, shown, 0.6f, 0.9f, 1f / 60f);
            Assert.AreEqual(2, feedback.Count);
            Assert.AreEqual(2, setups, "each slot that now holds another piece sets it up once");
            Assert.AreEqual(feedback.Piece(0).Id, shown[0]);
            Assert.AreEqual(feedback.Piece(1).Id, shown[1]);
            Assert.AreEqual(0, RunFrames(feedback, shown, 0.9f, 1.5f, 1f / 60f), "nothing again until something changes");
        }

        // ---- The motion -----------------------------------------------------------------------------

        [Test]
        public void Curves_RiseFadeAndPunch()
        {
            var normal = new FeedbackPiece { Kind = FeedbackKind.Up, StartTime = 1f, RetiredAt = float.NaN };
            Assert.AreEqual(0f, RelationshipFeedback.Rise(normal, 1f), 0.001f);
            Assert.AreEqual(RelationshipFeedback.RisePixels, RelationshipFeedback.Rise(normal, 1f + RelationshipFeedback.LifeSeconds), 0.001f, "about 30 px");
            Assert.Greater(RelationshipFeedback.Rise(normal, 1.5f), RelationshipFeedback.RisePixels * 0.5f, "fast at first");

            Assert.AreEqual(1f, RelationshipFeedback.Alpha(normal, 1.5f), 0.001f);
            Assert.AreEqual(0f, RelationshipFeedback.Alpha(normal, 1f + RelationshipFeedback.LifeSeconds), 0.001f, "gone in about 1.5 s");
            Assert.Greater(RelationshipFeedback.Alpha(normal, 1f + RelationshipFeedback.LifeSeconds * 0.8f), 0f);
            Assert.AreEqual(1f, RelationshipFeedback.Punch(normal, 1f), "a normal piece does not punch");

            var crit = new FeedbackPiece { Kind = FeedbackKind.CritUp, StartTime = 1f, RetiredAt = float.NaN };
            Assert.AreEqual(1.6f, RelationshipFeedback.Punch(crit, 1f), 0.001f);
            Assert.AreEqual(1f, RelationshipFeedback.Punch(crit, 1f + RelationshipFeedback.PunchSeconds), 0.001f);
            var mid = RelationshipFeedback.Punch(crit, 1f + RelationshipFeedback.PunchSeconds * 0.5f);
            Assert.Less(mid, 1.6f);
            Assert.Greater(mid, 1f);
            Assert.Greater(RelationshipFeedback.Life(crit), RelationshipFeedback.Life(normal), "a crit stays a little longer");

            var retired = new FeedbackPiece { Kind = FeedbackKind.Up, StartTime = 1f, RetiredAt = 1.2f };
            Assert.AreEqual(0f, RelationshipFeedback.Alpha(retired, 1.2f + RelationshipFeedback.RetireSeconds), 0.001f);
        }

        [Test]
        public void ChatTool_ReadsItsFlag()
        {
            Assert.IsTrue(PisaraChatTool.Requested(new[] { "Happi.exe", "-pisara-chat", "lines.txt" }));
            Assert.IsFalse(PisaraChatTool.Requested(new[] { "Happi.exe", "-pisara-chat" }));
            Assert.IsFalse(PisaraChatTool.Requested(new[] { "Happi.exe" }));
        }

        [Test]
        public void TurnReminder_AsksForLanded_WithEveryWordTheParserReads()
        {
            StringAssert.Contains("<animation", LandingEvents.TurnReminder);
            StringAssert.Contains("landed=", LandingEvents.TurnReminder);
            foreach (LandingEvent landing in System.Enum.GetValues(typeof(LandingEvent)))
            {
                var word = LandingEvents.Name(landing);
                StringAssert.Contains(word, LandingEvents.TurnReminder);
                Assert.AreEqual(landing, LandingEvents.Parse(word));
            }

            var example = AnimationCueParser.Parse("<animation emotion=\"neutral\" landed=\"teased\" />Hi").Cue;
            Assert.AreEqual(LandingEvent.Teased, example.Landed);
        }

        // ---- The setting ------------------------------------------------------------------------------

        [Test]
        public void Setting_IsOnByDefault_AndOldFilesStillLoad()
        {
            Assert.IsTrue(new CompanionSettings().ShowRelationshipFeedback);
            var old = CompanionSettingsJson.Parse("{\"version\":1,\"companionName\":\"Leia\"}", SettingsLegacyPrefs.None);
            Assert.IsNotNull(old);
            Assert.IsTrue(old.ShowRelationshipFeedback, "a file without the key keeps it on");

            var settings = CompanionSettings.CreateDefault();
            settings.ShowRelationshipFeedback = false;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.IsFalse(back.ShowRelationshipFeedback);
        }
    }
}
