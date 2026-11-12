using System;
using System.IO;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-103 phase 1: her lasting mood.</summary>
    public class CompanionMoodTests
    {
        const float Day = 14f;

        [TearDown]
        public void TearDown()
        {
            InstallKeyFile.KeyForPath = null;
            IntimateExpressions.Reset();
        }

        static AnimationCue Cue(string tag)
        {
            return AnimationCueParser.Parse(tag + "\nx").Cue;
        }

        // ---- showing one mood, without flicker ---------------------------------------------

        [Test]
        public void StartsContent_AndAMoodShowsOnlyWhenStrongEnough()
        {
            var mood = new CompanionMoodState();
            Assert.AreEqual(CompanionMood.Content, mood.Shown);

            mood.Push(CompanionMood.Happy, 0.2f);
            Assert.AreEqual(CompanionMood.Content, mood.Shown, "0.2 is below the enter strength");

            Assert.IsTrue(mood.Push(CompanionMood.Happy, 0.2f));
            Assert.AreEqual(CompanionMood.Happy, mood.Shown);
            Assert.AreEqual(0.36f, mood.Strength(CompanionMood.Happy), 0.001f, "each push covers its share of the distance left");
        }

        [Test]
        public void ANewMood_MustBeatTheShownOneByTheMargin()
        {
            var mood = new CompanionMoodState();
            mood.Push(CompanionMood.Happy, 0.5f);
            Assert.AreEqual(CompanionMood.Happy, mood.Shown);

            // Just stronger is not enough.
            mood.Push(CompanionMood.Sad, 0.55f);
            Assert.Greater(mood.Strength(CompanionMood.Sad), mood.Strength(CompanionMood.Happy));
            Assert.AreEqual(CompanionMood.Happy, mood.Shown);

            // Stronger by the margin switches.
            mood.Push(CompanionMood.Sad, 0.3f);
            Assert.AreEqual(CompanionMood.Sad, mood.Shown);
        }

        [Test]
        public void TwoMoodsNeckAndNeck_DoNotFlickerBackAndForth()
        {
            var mood = new CompanionMoodState();
            mood.Push(CompanionMood.Happy, 0.5f);
            var switches = 0;
            for (var i = 0; i < 20; i++)
            {
                if (mood.Push(i % 2 == 0 ? CompanionMood.Annoyed : CompanionMood.Happy, 0.05f))
                    switches++;
            }

            Assert.LessOrEqual(switches, 1);
        }

        [Test]
        public void AFadedMood_LeavesBelowTheLeaveStrength_NotAtTheEnterStrength()
        {
            var mood = new CompanionMoodState();
            mood.Push(CompanionMood.Happy, 0.4f);
            Assert.AreEqual(CompanionMood.Happy, mood.Shown);

            // Happy halves every 3 minutes: 0.4 -> 0.2 after 3 min, still shown (above 0.15).
            mood.Tick(180f, Day);
            Assert.AreEqual(0.2f, mood.Strength(CompanionMood.Happy), 0.005f);
            Assert.AreEqual(CompanionMood.Happy, mood.Shown);

            // 0.1 after 6 min: back to Content.
            Assert.IsTrue(mood.Tick(180f, Day));
            Assert.AreEqual(CompanionMood.Content, mood.Shown);
        }

        // ---- decay ------------------------------------------------------------------------

        [Test]
        public void Moods_FadeTowardContent_AnnoyedAndSadSlower_HappyFaster()
        {
            var mood = new CompanionMoodState();
            mood.Push(CompanionMood.Happy, 0.8f);
            mood.Push(CompanionMood.Annoyed, 0.8f);
            mood.Push(CompanionMood.Sad, 0.8f);

            for (var i = 0; i < 600; i++)
                mood.Tick(1f, Day);

            var happy = mood.Strength(CompanionMood.Happy);
            var annoyed = mood.Strength(CompanionMood.Annoyed);
            var sad = mood.Strength(CompanionMood.Sad);
            Assert.Less(happy, annoyed);
            Assert.Less(annoyed, sad);
            Assert.AreEqual(0.8f * Math.Pow(0.5, 600.0 / 180.0), happy, 0.005f);
            Assert.AreEqual(0.8f * Math.Pow(0.5, 600.0 / 1200.0), sad, 0.005f);
        }

        [Test]
        public void ManySmallTicks_FadeLikeOneLongOne()
        {
            var small = new CompanionMoodState();
            var once = new CompanionMoodState();
            small.Push(CompanionMood.Annoyed, 0.7f);
            once.Push(CompanionMood.Annoyed, 0.7f);

            for (var i = 0; i < 300; i++)
                small.Tick(1f, Day);
            once.Tick(300f, Day);

            Assert.AreEqual(once.Strength(CompanionMood.Annoyed), small.Strength(CompanionMood.Annoyed), 0.001f);
        }

        [Test]
        public void Sleepy_GrowsSlowlyLateAtNight_AndNotInTheDay()
        {
            var day = new CompanionMoodState();
            for (var i = 0; i < 1800; i++)
                day.Tick(1f, Day);
            Assert.AreEqual(0f, day.Strength(CompanionMood.Sleepy));

            var night = new CompanionMoodState();
            night.Tick(60f, 2f);
            var afterAMinute = night.Strength(CompanionMood.Sleepy);
            Assert.Greater(afterAMinute, 0f);
            Assert.Less(afterAMinute, 0.05f, "it grows slowly");

            for (var i = 0; i < 60; i++)
                night.Tick(60f, 2f);
            Assert.AreEqual(CompanionMood.Sleepy, night.Shown, "an hour at 2 am makes her sleepy");
            Assert.LessOrEqual(night.Strength(CompanionMood.Sleepy), CompanionMoodTuning.NightSleepyMax + 0.0001f);
        }

        [TestCase(12f, 0f)]
        [TestCase(21.9f, 0f)]
        [TestCase(23.5f, 0.3f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(3f, 0.6f)]
        [TestCase(5.5f, 0f)]
        public void NightLevel_RisesFrom22To01_HoldsTo05(float hour, float expected)
        {
            Assert.AreEqual(expected, CompanionMoodTuning.NightSleepyLevel(hour), 0.001f);
        }

        // ---- the Intimate gate ------------------------------------------------------------

        [Test]
        public void Intimate_NeverGains_WithoutTheGate()
        {
            var mood = new CompanionMoodState();
            mood.SetIntimateAllowed(false);

            Assert.IsFalse(mood.Push(CompanionMood.Intimate, 1f));
            CompanionMoodTable.ApplyCue(mood, Cue("<animation emotion=\"seductive\" intensity=\"1\" />"));

            Assert.AreEqual(0f, mood.Strength(CompanionMood.Intimate));
            Assert.AreNotEqual(CompanionMood.Intimate, mood.Shown);
            StringAssert.DoesNotContain("intimate", mood.DescribeTop3());
        }

        [Test]
        public void Intimate_GrowsWithTheGate_AndIsClearedTheMomentItCloses()
        {
            var mood = new CompanionMoodState();
            mood.SetIntimateAllowed(true);
            mood.Push(CompanionMood.Intimate, 0.6f);
            Assert.AreEqual(CompanionMood.Intimate, mood.Shown);
            StringAssert.Contains("intimate", mood.DescribeTop3());

            Assert.IsTrue(mood.SetIntimateAllowed(false));
            Assert.AreEqual(0f, mood.Strength(CompanionMood.Intimate));
            Assert.AreEqual(CompanionMood.Content, mood.Shown);

            // Ticking with the gate closed keeps it at 0.
            mood.Tick(10f, Day);
            Assert.AreEqual(0f, mood.Strength(CompanionMood.Intimate));
        }

        [Test]
        public void Intimate_FollowsIntimateExpressionsActive()
        {
            IntimateExpressions.Reset();
            Assert.IsFalse(IntimateExpressions.Active);
            var mood = new CompanionMoodState();
            mood.SetIntimateAllowed(IntimateExpressions.Active);
            Assert.IsFalse(mood.Push(CompanionMood.Intimate, 1f));

            IntimateExpressions.Configure(true, true, true);
            mood.SetIntimateAllowed(IntimateExpressions.Active);
            Assert.IsTrue(mood.Push(CompanionMood.Intimate, 1f));
        }

        // ---- persistence ------------------------------------------------------------------

        [Test]
        public void Json_RoundTrip_KeepsStrengthsAndShown_ButNeverIntimate()
        {
            var mood = new CompanionMoodState();
            mood.SetIntimateAllowed(true);
            mood.Push(CompanionMood.Annoyed, 0.6f);
            mood.Push(CompanionMood.Sad, 0.2f);
            mood.Push(CompanionMood.Intimate, 0.3f);
            var now = new DateTime(2026, 11, 12, 18, 0, 0, DateTimeKind.Utc);

            var json = mood.ToJson(now);
            StringAssert.DoesNotContain("intimate", json);
            var back = CompanionMoodState.FromJson(json, now);

            Assert.IsNotNull(back);
            Assert.AreEqual(0.6f, back.Strength(CompanionMood.Annoyed), 0.0001f);
            Assert.AreEqual(0.2f, back.Strength(CompanionMood.Sad), 0.0001f);
            Assert.AreEqual(0f, back.Strength(CompanionMood.Intimate));
            Assert.AreEqual(CompanionMood.Annoyed, back.Shown);
        }

        [Test]
        public void Restore_FadesForTheTimeTheAppWasClosed()
        {
            var mood = new CompanionMoodState();
            mood.Push(CompanionMood.Sad, 0.8f);
            var saved = new DateTime(2026, 11, 12, 18, 0, 0, DateTimeKind.Utc);
            var json = mood.ToJson(saved);

            // Sad halves every 20 minutes: 40 minutes closed is a quarter.
            var back = CompanionMoodState.FromJson(json, saved.AddMinutes(40));
            Assert.AreEqual(0.2f, back.Strength(CompanionMood.Sad), 0.001f);
            Assert.AreEqual(CompanionMood.Sad, back.Shown);

            // A day closed: back to Content.
            var later = CompanionMoodState.FromJson(json, saved.AddDays(1));
            Assert.AreEqual(CompanionMood.Content, later.Shown);

            // A clock that went backwards does not add strength.
            var earlier = CompanionMoodState.FromJson(json, saved.AddHours(-3));
            Assert.AreEqual(0.8f, earlier.Strength(CompanionMood.Sad), 0.0001f);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"version\":0}")]
        [TestCase("{\"version\":1}")]
        public void Restore_ABrokenFile_IsNull_SoSheStartsContent(string json)
        {
            Assert.IsNull(CompanionMoodState.FromJson(json, DateTime.UtcNow));
        }

        [Test]
        public void Restore_IgnoresUnknownNamesAndBadNumbers()
        {
            const string json = "{\"version\":1,\"savedUtcTicks\":0,\"shown\":\"grumpy\",\"moods\":[\"happy\",\"dancing\",\"intimate\",\"sad\"],\"strengths\":[0.5,0.9,0.9,7]}";

            var back = CompanionMoodState.FromJson(json, DateTime.UtcNow);

            Assert.AreEqual(0.5f, back.Strength(CompanionMood.Happy), 0.0001f);
            Assert.AreEqual(0f, back.Strength(CompanionMood.Intimate));
            Assert.AreEqual(1f, back.Strength(CompanionMood.Sad), 0.0001f, "clamped to 1");
        }

        [Test]
        public void Store_EncryptedRoundTripPerPersona()
        {
            var folder = Path.Combine(Path.GetTempPath(), "leia-mood-" + Guid.NewGuid().ToString("N"), "ProjectLeia");
            Directory.CreateDirectory(folder);
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = (byte)(i * 7 + 1);
            InstallKeyFile.KeyForPath = _ => key;
            try
            {
                var lola = CompanionMoodStore.FileFor(folder, "Lola Bunny");
                var other = CompanionMoodStore.FileFor(folder, string.Empty);
                Assert.AreEqual("mood-lola-bunny.json", Path.GetFileName(lola));
                Assert.AreEqual("mood.json", Path.GetFileName(other));

                var mood = new CompanionMoodState();
                mood.Push(CompanionMood.Flustered, 0.7f);
                var now = DateTime.UtcNow;
                Assert.IsTrue(CompanionMoodStore.Save(lola, mood, now));

                // Encrypted on disk, not readable JSON.
                var bytes = File.ReadAllBytes(lola);
                Assert.IsTrue(LocalAesGcm.IsMagic(bytes));
                StringAssert.DoesNotContain("flustered", System.Text.Encoding.UTF8.GetString(bytes));

                var back = CompanionMoodStore.Load(lola, now);
                Assert.AreEqual(CompanionMood.Flustered, back.Shown);
                Assert.AreEqual(0.7f, back.Strength(CompanionMood.Flustered), 0.001f);

                // Another persona has its own mood.
                Assert.AreEqual(CompanionMood.Content, CompanionMoodStore.Load(other, now).Shown);
            }
            finally
            {
                try
                {
                    Directory.Delete(Path.GetDirectoryName(folder), true);
                }
                catch (IOException)
                {
                }
            }
        }

        [Test]
        public void Inventory_TreatsMoodFilesAsPrivate_SoExportAndWipeIncludeThem()
        {
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("mood.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("mood-lola.json"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("moodboard.png"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("portraits/mood.json"));
        }

        // ---- inputs -----------------------------------------------------------------------

        [Test]
        public void HerCue_PushesTheMatchingMood_ScaledByIntensity()
        {
            var strong = new CompanionMoodState();
            var weak = new CompanionMoodState();
            CompanionMoodTable.ApplyCue(strong, Cue("<animation emotion=\"happy\" intensity=\"1\" />"));
            CompanionMoodTable.ApplyCue(weak, Cue("<animation emotion=\"happy\" intensity=\"0\" />"));

            Assert.Greater(strong.Strength(CompanionMood.Happy), weak.Strength(CompanionMood.Happy));
            Assert.AreEqual(0.25f * 1.5f, strong.Strength(CompanionMood.Happy), 0.0001f);
            Assert.AreEqual(0.25f * 0.5f, weak.Strength(CompanionMood.Happy), 0.0001f);
        }

        [TestCase("<animation emotion=\"sad\" intensity=\"0.6\" />", CompanionMood.Sad)]
        [TestCase("<animation emotion=\"embarrassed\" intensity=\"0.6\" />", CompanionMood.Flustered)]
        [TestCase("<animation emotion=\"flustered\" intensity=\"0.6\" />", CompanionMood.Flustered)]
        [TestCase("<animation emotion=\"concerned\" intensity=\"0.6\" />", CompanionMood.Anxious)]
        [TestCase("<animation emotion=\"pouty\" intensity=\"0.6\" />", CompanionMood.Annoyed)]
        [TestCase("<animation emotion=\"loving\" intensity=\"0.6\" />", CompanionMood.Affectionate)]
        [TestCase("<animation emotion=\"crying\" intensity=\"0.6\" />", CompanionMood.Sad)]
        [TestCase("<animation emotion=\"scared\" intensity=\"0.6\" />", CompanionMood.Anxious)]
        [TestCase("<animation emotion=\"sleepy\" intensity=\"0.6\" />", CompanionMood.Sleepy)]
        public void HerCue_MapsToTheMoodInTheTable(string tag, CompanionMood expected)
        {
            var mood = new CompanionMoodState();
            CompanionMoodTable.ApplyCue(mood, Cue(tag));

            var top = new CompanionMood[3];
            var strengths = new float[3];
            mood.Top3(top, strengths);
            Assert.AreEqual(expected, top[0], tag);
            Assert.Greater(strengths[0], 0f, tag);
        }

        [Test]
        public void ANeutralOrThinkingCue_LeavesHerMoodAlone()
        {
            var mood = new CompanionMoodState();
            CompanionMoodTable.ApplyCue(mood, Cue("<animation emotion=\"neutral\" />"));
            CompanionMoodTable.ApplyCue(mood, Cue("<animation emotion=\"thinking\" />"));
            CompanionMoodTable.ApplyCue(mood, AnimationCueParser.NeutralCue);

            Assert.AreEqual("none", mood.DescribeTop3());
        }

        [TestCase(UserEmotion.Happy, CompanionMood.Happy)]
        [TestCase(UserEmotion.Sad, CompanionMood.Sad)]
        [TestCase(UserEmotion.Angry, CompanionMood.Annoyed)]
        [TestCase(UserEmotion.Anxious, CompanionMood.Anxious)]
        [TestCase(UserEmotion.Affectionate, CompanionMood.Affectionate)]
        public void TheUsersMood_PushesTheMatchingMood(UserEmotion user, CompanionMood expected)
        {
            var mood = new CompanionMoodState();
            CompanionMoodTable.ApplyUser(mood, new UserMood(user, 0.6f));

            Assert.Greater(mood.Strength(expected), 0f);
        }

        [TestCase(UserEmotion.Neutral)]
        [TestCase(UserEmotion.Surprised)]
        [TestCase(UserEmotion.Confused)]
        public void SomeUserMoods_DoNotMoveHers(UserEmotion user)
        {
            var mood = new CompanionMoodState();
            CompanionMoodTable.ApplyUser(mood, new UserMood(user, 0.9f));

            Assert.AreEqual("none", mood.DescribeTop3());
        }

        [Test]
        public void RepeatedSmallPushes_AddUp_ButNeverPastOne()
        {
            var mood = new CompanionMoodState();
            for (var i = 0; i < 6; i++)
                CompanionMoodTable.ApplyUser(mood, new UserMood(UserEmotion.Happy, 0.5f));
            Assert.AreEqual(CompanionMood.Happy, mood.Shown);

            for (var i = 0; i < 200; i++)
                mood.Push(CompanionMood.Happy, 0.9f);
            Assert.LessOrEqual(mood.Strength(CompanionMood.Happy), 1f);
        }

        [Test]
        public void Inputs_ReachAListener_AndTouchAndIgnoreAreHooksOnly()
        {
            var cues = 0;
            var users = 0;
            var touches = 0;
            Action<AnimationCue> onCue = c => cues++;
            Action<UserMood> onUser = u => users++;
            Action<CompanionTouch> onTouch = t => touches++;
            CompanionMoodInputs.CueApplied += onCue;
            CompanionMoodInputs.UserMoodDetected += onUser;
            CompanionMoodInputs.Touched += onTouch;
            try
            {
                CompanionMoodInputs.ReportCue(AnimationCueParser.NeutralCue);
                CompanionMoodInputs.ReportUserMood(UserMood.Neutral);
                CompanionMoodInputs.ReportTouch(CompanionTouch.Pat);
                CompanionMoodInputs.ReportIgnored(600f);
            }
            finally
            {
                CompanionMoodInputs.CueApplied -= onCue;
                CompanionMoodInputs.UserMoodDetected -= onUser;
                CompanionMoodInputs.Touched -= onTouch;
            }

            Assert.AreEqual(1, cues);
            Assert.AreEqual(1, users);
            Assert.AreEqual(1, touches);
            // Without a listener a report does nothing and does not throw.
            CompanionMoodInputs.ReportTouch(CompanionTouch.Poke);
        }

        [Test]
        public void Names_RoundTrip()
        {
            foreach (CompanionMood value in Enum.GetValues(typeof(CompanionMood)))
            {
                CompanionMood parsed;
                Assert.IsTrue(CompanionMoodNames.TryParse(CompanionMoodNames.Name(value), out parsed));
                Assert.AreEqual(value, parsed);
            }

            Assert.AreEqual(CompanionMoodNames.Count, Enum.GetValues(typeof(CompanionMood)).Length);
        }
    }
}
