using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-103 phase 3: persona temperaments. The default is exactly phase 1/2, the presets
    /// scale pushes and half-lives, baselines and biases hold floors, Intimate is never boosted
    /// while the gate is closed, and the temperament is saved with the persona.
    /// </summary>
    public class PersonaTemperamentTests
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

        static PersonaTemperament Preset(string id)
        {
            return PersonaTemperaments.Get(id);
        }

        // ---- the default is phase 1/2 ----------------------------------------------------------

        [Test]
        public void Default_IsNeutral_AndEveryBlankOrUnknownIdIsTheDefault()
        {
            Assert.IsTrue(PersonaTemperament.Default.IsNeutral);
            Assert.AreEqual(PersonaTemperaments.DefaultId, PersonaTemperament.Default.Id);
            Assert.AreSame(PersonaTemperament.Default, Preset(null));
            Assert.AreSame(PersonaTemperament.Default, Preset("  "));
            Assert.AreSame(PersonaTemperament.Default, Preset("grumpy-cat"));
            Assert.AreSame(PersonaTemperament.Default, new CompanionMoodState().Temperament);
            foreach (var id in PersonaTemperaments.Ids)
            {
                if (id != PersonaTemperaments.DefaultId)
                    Assert.IsFalse(Preset(id).IsNeutral, id);
            }
        }

        [Test]
        public void Default_GivesExactlyThePhase1Numbers()
        {
            // A fresh state and one with the default set explicitly behave the same, and both match
            // the phase 1 formulas: push = gain * (0.5 + intensity), halve every half-life.
            var plain = new CompanionMoodState();
            var set = new CompanionMoodState { Temperament = PersonaTemperament.Default };
            foreach (var state in new[] { plain, set })
            {
                CompanionMoodTable.ApplyCue(state, Cue("<animation emotion=\"happy\" intensity=\"0.6\" />"));
                Assert.AreEqual(0.25f * 1.1f, state.Strength(CompanionMood.Happy), 0.0001f);
                CompanionMoodTable.ApplyCue(state, Cue("<animation emotion=\"pouty\" intensity=\"0.6\" />"));
                Assert.AreEqual(0.3f * 1.1f, state.Strength(CompanionMood.Annoyed), 0.0001f);
                CompanionMoodTable.ApplyUser(state, new UserMood(UserEmotion.Sad, 1f));
                Assert.AreEqual(0.15f * 1.5f, state.Strength(CompanionMood.Sad), 0.0001f);

                state.Tick(180f, Day);
                Assert.AreEqual(0.25f * 1.1f * 0.5f, state.Strength(CompanionMood.Happy), 0.0001f);
                Assert.AreEqual(0.3f * 1.1f * (float)Math.Pow(0.5, 180.0 / 900.0), state.Strength(CompanionMood.Annoyed), 0.0001f);
            }

            Assert.AreEqual(plain.Shown, set.Shown);
            Assert.AreEqual(plain.DescribeTop3(), set.DescribeTop3());
        }

        // ---- multipliers ------------------------------------------------------------------------

        [Test]
        public void Tsundere_BuildsAnnoyedFaster_AndAffectionSlower()
        {
            var tsundere = new CompanionMoodState { Temperament = Preset(PersonaTemperaments.TsundereId) };
            var balanced = new CompanionMoodState();
            var pout = Cue("<animation emotion=\"pouty\" intensity=\"0.6\" />");
            var love = Cue("<animation emotion=\"loving\" intensity=\"0.6\" />");
            CompanionMoodTable.ApplyCue(tsundere, pout);
            CompanionMoodTable.ApplyCue(balanced, pout);
            CompanionMoodTable.ApplyCue(tsundere, love);
            CompanionMoodTable.ApplyCue(balanced, love);

            // Tsundere starts on her annoyed bias (0.15); the push covers its share of the rest.
            Assert.AreEqual(0.15f + 0.3f * 1.1f * 1.4f * 0.85f, tsundere.Strength(CompanionMood.Annoyed), 0.0001f);
            Assert.AreEqual(0.3f * 1.1f * 0.7f, tsundere.Strength(CompanionMood.Affectionate), 0.0001f);
            Assert.Greater(tsundere.Strength(CompanionMood.Annoyed), balanced.Strength(CompanionMood.Annoyed));
            Assert.Less(tsundere.Strength(CompanionMood.Affectionate), balanced.Strength(CompanionMood.Affectionate));
        }

        [Test]
        public void HalfLifeScale_ChangesHowFastAMoodFades()
        {
            var tsundere = new CompanionMoodState { Temperament = Preset(PersonaTemperaments.TsundereId) };
            var calm = new CompanionMoodState { Temperament = Preset(PersonaTemperaments.CalmId) };
            tsundere.Push(CompanionMood.Annoyed, 0.8f);
            calm.Push(CompanionMood.Annoyed, 0.8f);
            var tsundereBefore = tsundere.Strength(CompanionMood.Annoyed);
            var calmBefore = calm.Strength(CompanionMood.Annoyed);

            tsundere.Tick(900f, Day);
            calm.Tick(900f, Day);

            Assert.AreEqual(tsundereBefore * (float)Math.Pow(0.5, 1.0 / 1.3), tsundere.Strength(CompanionMood.Annoyed), 0.001f);
            Assert.AreEqual(calmBefore * (float)Math.Pow(0.5, 1.0 / 0.6), calm.Strength(CompanionMood.Annoyed), 0.001f);
        }

        [Test]
        public void Baseline_StartsAtOnce_HoldsAtRest_AndHysteresisStillApplies()
        {
            // She starts on her baseline, without a long climb (Bugbot on #68).
            var cheerful = new CompanionMoodState { Temperament = Preset(PersonaTemperaments.CheerfulId) };
            Assert.AreEqual(CompanionMood.Happy, cheerful.Temperament.Baseline);
            Assert.AreEqual(PersonaTemperament.BaselineStrength, cheerful.Strength(CompanionMood.Happy), 0.0001f);
            Assert.AreEqual(CompanionMood.Happy, cheerful.Shown);

            // A strong happy moment fades back to the baseline, never below it.
            cheerful.Push(CompanionMood.Happy, 0.9f);
            cheerful.Tick(36000f, Day);
            Assert.AreEqual(PersonaTemperament.BaselineStrength, cheerful.Strength(CompanionMood.Happy), 0.0001f);
            for (var i = 0; i < 600; i++)
                cheerful.Tick(1f, Day);
            Assert.AreEqual(PersonaTemperament.BaselineStrength, cheerful.Strength(CompanionMood.Happy), 0.0001f);
            Assert.AreEqual(CompanionMood.Happy, cheerful.Shown);

            // Another mood still needs the usual margin to replace the shown one.
            cheerful.Push(CompanionMood.Sad, 0.4f);
            Assert.AreEqual(CompanionMood.Happy, cheerful.Shown);
            cheerful.Push(CompanionMood.Sad, 0.2f);
            Assert.AreEqual(CompanionMood.Sad, cheerful.Shown);
        }

        [Test]
        public void SwitchingTemperament_LiftsToTheNewFloors_AtOnce()
        {
            var state = new CompanionMoodState();
            Assert.AreEqual(CompanionMood.Content, state.Shown);
            state.Temperament = Preset(PersonaTemperaments.CheerfulId);
            Assert.AreEqual(CompanionMood.Happy, state.Shown);

            // The default never lifts anything.
            var plain = new CompanionMoodState { Temperament = PersonaTemperament.Default };
            Assert.AreEqual("none", plain.DescribeTop3());
        }

        [Test]
        public void ClosedTime_EndsOnTheFloors_SoTheBaselineSurvivesARestart()
        {
            var cheerful = Preset(PersonaTemperaments.CheerfulId);
            var mood = new CompanionMoodState { Temperament = cheerful };
            var saved = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
            var json = mood.ToJson(saved);

            var back = CompanionMoodState.FromJson(json, saved.AddHours(3), cheerful);
            Assert.AreEqual(PersonaTemperament.BaselineStrength, back.Strength(CompanionMood.Happy), 0.0001f);
            Assert.AreEqual(CompanionMood.Happy, back.Shown);

            // A file from before temperaments (or from another temperament) is lifted too.
            var old = new CompanionMoodState().ToJson(saved);
            Assert.AreEqual(CompanionMood.Happy, CompanionMoodState.FromJson(old, saved, cheerful).Shown);
            Assert.AreEqual(CompanionMood.Content, CompanionMoodState.FromJson(old, saved).Shown);
        }

        [Test]
        public void RestBias_KeepsALowFloor_ThatDoesNotShowOnItsOwn()
        {
            var shy = new CompanionMoodState { Temperament = Preset(PersonaTemperaments.ShyId) };
            Assert.AreEqual(0.15f, shy.Strength(CompanionMood.Flustered), 0.0001f);
            shy.Tick(36000f, Day);
            Assert.AreEqual(0.15f, shy.Strength(CompanionMood.Flustered), 0.0001f);
            Assert.AreEqual(CompanionMood.Content, shy.Shown);

            // From the floor, one push is enough to show.
            CompanionMoodTable.ApplyCue(shy, Cue("<animation emotion=\"embarrassed\" intensity=\"0.3\" />"));
            Assert.AreEqual(CompanionMood.Flustered, shy.Shown);
        }

        [Test]
        public void BuiltTemperaments_ClampTheirNumbers()
        {
            var custom = new PersonaTemperament("test", "Test")
                .WithGain(CompanionMood.Sad, 99f)
                .WithHalfLife(CompanionMood.Sad, 0f)
                .WithRestBias(CompanionMood.Sad, 0.9f)
                .WithGain(CompanionMood.Content, 5f);
            Assert.AreEqual(PersonaTemperament.MaxScale, custom.Gain(CompanionMood.Sad, false));
            Assert.AreEqual(PersonaTemperament.MinScale, custom.HalfLifeScale(CompanionMood.Sad));
            Assert.AreEqual(PersonaTemperament.MaxRestBias, custom.RestBiasStrength);
            Assert.AreEqual(1f, custom.Gain(CompanionMood.Content, false));
        }

        // ---- the Intimate gate --------------------------------------------------------------------

        [Test]
        public void Intimate_IsNeverBoosted_WhileTheGateIsClosed_AndIsNeverABaselineOrBias()
        {
            var boosted = new PersonaTemperament("test-intimate", "Test")
                .WithGain(CompanionMood.Intimate, 2f)
                .WithBaseline(CompanionMood.Intimate)
                .WithRestBias(CompanionMood.Intimate, 0.2f);
            Assert.AreEqual(CompanionMood.Content, boosted.Baseline);
            Assert.AreEqual(CompanionMood.Content, boosted.RestBias);
            Assert.AreEqual(0f, boosted.RestFloor(CompanionMood.Intimate));
            Assert.AreEqual(1f, boosted.Gain(CompanionMood.Intimate, false));
            Assert.AreEqual(2f, boosted.Gain(CompanionMood.Intimate, true));

            var flirty = Cue("<animation emotion=\"flirty\" intensity=\"0.6\" />");
            var closed = new CompanionMoodState { Temperament = boosted };
            IntimateExpressions.Reset();
            closed.SetIntimateAllowed(IntimateExpressions.Active);
            CompanionMoodTable.ApplyCue(closed, flirty);
            closed.Tick(36000f, Day);
            Assert.AreEqual(0f, closed.Strength(CompanionMood.Intimate));
            Assert.AreNotEqual(CompanionMood.Intimate, closed.Shown);

            IntimateExpressions.Configure(true, true, true);
            var open = new CompanionMoodState { Temperament = boosted };
            open.SetIntimateAllowed(IntimateExpressions.Active);
            CompanionMoodTable.ApplyCue(open, flirty);
            Assert.AreEqual(0.3f * 1.1f * 2f, open.Strength(CompanionMood.Intimate), 0.0001f);

            // No preset touches Intimate.
            foreach (var id in PersonaTemperaments.Ids)
            {
                Assert.AreEqual(1f, Preset(id).Gain(CompanionMood.Intimate, true), id);
                Assert.AreEqual(1f, Preset(id).HalfLifeScale(CompanionMood.Intimate), id);
                Assert.AreEqual(0f, Preset(id).RestFloor(CompanionMood.Intimate), id);
            }
        }

        // ---- persona: the stored id, built-ins, switching -----------------------------------------

        [Test]
        public void PersonaFile_StoresThePresetId_NotNumbers()
        {
            var persona = new PersonaDefinition { Id = "lola", DisplayName = "Lola", Temperament = "Tsundere" };
            var json = PersonaJson.Write(persona);
            StringAssert.Contains("\"temperament\": \"tsundere\"", json);
            Assert.AreEqual(PersonaTemperaments.TsundereId, PersonaJson.Parse(json).Temperament);

            Assert.AreEqual(string.Empty, PersonaJson.Parse("{\"id\":\"a\",\"temperament\":\"unknown\"}").Temperament);
            Assert.AreEqual(string.Empty, PersonaJson.Parse("{\"id\":\"a\",\"temperament\":\"default\"}").Temperament);
            Assert.AreEqual(string.Empty, PersonaJson.Parse("{\"id\":\"a\"}").Temperament);
            Assert.AreEqual(PersonaTemperaments.MoodyId, PersonaJson.Parse("{\"id\":\"a\",\"temperament\":\"Moody\"}").Temperament);
            Assert.AreEqual(PersonaTemperaments.TsundereId, persona.Copy().Temperament);
        }

        [Test]
        public void BuiltIns_HaveTemperaments_AndSwitchingPersonaSwitchesIt()
        {
            var catalog = PersonaCatalog.Load(PersonaFiles.ReadBuiltInTexts(), Path.Combine(Path.GetTempPath(), "leia-none-" + Guid.NewGuid().ToString("N")));
            Assert.AreEqual(PersonaTemperaments.CheerfulId, PersonaRuntime.TemperamentFor(catalog, "friend").Id);
            Assert.AreEqual(PersonaTemperaments.CalmId, PersonaRuntime.TemperamentFor(catalog, "calm").Id);
            Assert.AreEqual(PersonaTemperaments.CalmId, PersonaRuntime.TemperamentFor(catalog, "coach").Id);
            Assert.AreSame(PersonaTemperament.Default, PersonaRuntime.TemperamentFor(catalog, string.Empty));
            Assert.AreSame(PersonaTemperament.Default, PersonaRuntime.TemperamentFor(catalog, "missing"));
            Assert.AreSame(PersonaTemperament.Default, PersonaRuntime.TemperamentFor(null, "friend"));

            Assert.AreEqual("Mood temperament: friend = cheerful", MoodTemperamentLog.Line("friend", PersonaRuntime.TemperamentFor(catalog, "friend")));
            Assert.AreEqual("Mood temperament: (default) = default", MoodTemperamentLog.Line(string.Empty, PersonaTemperament.Default));
        }

        [Test]
        public void SaveAndLoad_TemperamentRidesWithThePersona_AndFadesTheClosedTime()
        {
            var folder = Path.Combine(Path.GetTempPath(), "leia-temper-" + Guid.NewGuid().ToString("N"), "ProjectLeia");
            Directory.CreateDirectory(folder);
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = (byte)(i * 5 + 3);
            InstallKeyFile.KeyForPath = _ => key;
            try
            {
                // The persona file (encrypted) carries the preset.
                var personas = SettingsLocations.PersonasFolder(folder);
                Assert.IsTrue(PersonaStore.Save(personas, new PersonaDefinition { Id = "lola", DisplayName = "Lola", Temperament = "moody" }));
                Assert.IsTrue(PersonaStore.Save(personas, new PersonaDefinition { Id = "kiki", DisplayName = "Kiki" }));
                var bytes = File.ReadAllBytes(Path.Combine(personas, "lola.json"));
                StringAssert.DoesNotContain("moody", System.Text.Encoding.UTF8.GetString(bytes));

                var catalog = PersonaCatalog.Load(new string[0], personas);
                var lola = PersonaRuntime.TemperamentFor(catalog, "lola");
                Assert.AreEqual(PersonaTemperaments.MoodyId, lola.Id);
                Assert.AreSame(PersonaTemperament.Default, PersonaRuntime.TemperamentFor(catalog, "kiki"));

                // The mood file is unchanged; the temperament fades the time she was closed.
                var mood = new CompanionMoodState { Temperament = lola };
                mood.Push(CompanionMood.Sad, 0.8f);
                var sad = mood.Strength(CompanionMood.Sad);
                var saved = DateTime.UtcNow;
                var path = CompanionMoodStore.FileFor(folder, "lola");
                Assert.IsTrue(CompanionMoodStore.Save(path, mood, saved));

                var later = saved.AddSeconds(1200);
                var moody = CompanionMoodStore.Load(path, later, lola);
                var plain = CompanionMoodStore.Load(path, later);
                Assert.AreSame(lola, moody.Temperament);
                Assert.AreSame(PersonaTemperament.Default, plain.Temperament);
                Assert.AreEqual(sad * (float)Math.Pow(0.5, 1.0 / 1.4), moody.Strength(CompanionMood.Sad), 0.002f);
                Assert.AreEqual(sad * 0.5f, plain.Strength(CompanionMood.Sad), 0.002f);

                // A missing mood file still gets the temperament.
                Assert.AreSame(lola, CompanionMoodStore.Load(CompanionMoodStore.FileFor(folder, "nobody"), later, lola).Temperament);
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
    }
}
