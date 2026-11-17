using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-136 phase 1: nine sliders generate mood numbers, the old presets stay aliases, old
    /// persona files load, and a manual pick wins over Pisara's reading.
    /// </summary>
    public class TemperamentAxesTests
    {
        const float Day = 14f;

        [TearDown]
        public void TearDown()
        {
            InstallKeyFile.KeyForPath = null;
        }

        [Test]
        public void Middle_IsTheBalancedPreset_AndEveryPresetPointKeepsItsAuthoredMood()
        {
            Assert.AreSame(PersonaTemperament.Default, TemperamentMood.From(null));
            Assert.AreSame(PersonaTemperament.Default, TemperamentMood.From(TemperamentAxes.Neutral()));
            Assert.IsFalse(TemperamentAxes.HasOpinion(TemperamentAxes.Neutral()));

            foreach (var id in PersonaTemperaments.Ids)
            {
                var point = TemperamentAxes.ForPreset(id);
                var fromSliders = TemperamentMood.From(point);
                var authored = PersonaTemperaments.Get(id);
                Assert.AreSame(authored, fromSliders, id);
                Assert.AreEqual(1f, fromSliders.Gain(CompanionMood.Intimate, true), id);
                Assert.IsTrue(point.Same(TemperamentAxes.ForPreset(authored.Name)), id);
            }
        }

        [Test]
        public void TsunderePoint_IsHighWarmthAndLowDirectness()
        {
            var point = TemperamentAxes.ForPreset(PersonaTemperaments.TsundereId);
            Assert.GreaterOrEqual(point.Value(TemperamentAxes.Warmth), 75);
            Assert.LessOrEqual(point.Value(TemperamentAxes.Directness), 30);
            Assert.AreEqual(PersonaTemperaments.TsundereId, TemperamentAxes.MatchingPresetId(point));
        }

        [Test]
        public void Sliders_MoveTheMoods_AndCuriosityDoesNot()
        {
            var cold = TemperamentAxes.Neutral();
            cold.Set(TemperamentAxes.Warmth, 0);
            var warm = TemperamentAxes.Neutral();
            warm.Set(TemperamentAxes.Warmth, 100);
            Assert.Greater(
                TemperamentMood.From(warm).Gain(CompanionMood.Affectionate, true),
                TemperamentMood.From(cold).Gain(CompanionMood.Affectionate, true));

            var steady = TemperamentAxes.Neutral();
            steady.Set(TemperamentAxes.Volatility, 0);
            var reactive = TemperamentAxes.Neutral();
            reactive.Set(TemperamentAxes.Volatility, 100);
            Assert.Greater(
                TemperamentMood.From(reactive).Gain(CompanionMood.Annoyed, true),
                TemperamentMood.From(steady).Gain(CompanionMood.Annoyed, true));
            Assert.Greater(
                TemperamentMood.From(reactive).HalfLifeScale(CompanionMood.Annoyed),
                TemperamentMood.From(steady).HalfLifeScale(CompanionMood.Annoyed));

            var hinting = TemperamentAxes.Neutral();
            hinting.Set(TemperamentAxes.Warmth, 100);
            hinting.Set(TemperamentAxes.Directness, 0);
            var blunt = hinting.Clone();
            blunt.Set(TemperamentAxes.Directness, 100);
            Assert.Less(
                TemperamentMood.From(hinting).Gain(CompanionMood.Affectionate, true),
                TemperamentMood.From(blunt).Gain(CompanionMood.Affectionate, true));

            var yielding = TemperamentAxes.Neutral();
            yielding.Set(TemperamentAxes.Assertiveness, 0);
            var initiating = TemperamentAxes.Neutral();
            initiating.Set(TemperamentAxes.Assertiveness, 100);
            Assert.Greater(
                TemperamentMood.From(yielding).Gain(CompanionMood.Flustered, true),
                TemperamentMood.From(initiating).Gain(CompanionMood.Flustered, true));
            Assert.Greater(
                TemperamentMood.From(initiating).Gain(CompanionMood.Annoyed, true),
                TemperamentMood.From(yielding).Gain(CompanionMood.Annoyed, true));

            var unsure = TemperamentAxes.Neutral();
            unsure.Set(TemperamentAxes.Confidence, 0);
            var sure = TemperamentAxes.Neutral();
            sure.Set(TemperamentAxes.Confidence, 100);
            Assert.Greater(
                TemperamentMood.From(unsure).Gain(CompanionMood.Anxious, true),
                TemperamentMood.From(sure).Gain(CompanionMood.Anxious, true));

            var quiet = TemperamentAxes.Neutral();
            quiet.Set(TemperamentAxes.Warmth, 60);
            var asking = quiet.Clone();
            asking.Set(TemperamentAxes.Curiosity, 100);
            Assert.IsTrue(TemperamentMood.From(quiet).SameTuning(TemperamentMood.From(asking)));

            var onlyCurious = TemperamentAxes.Neutral();
            onlyCurious.Set(TemperamentAxes.Curiosity, 80);
            Assert.IsFalse(TemperamentAxes.HasOpinion(onlyCurious));
            Assert.AreSame(PersonaTemperament.Default, TemperamentMood.From(onlyCurious));

            var tsundereCurious = TemperamentAxes.ForPreset(PersonaTemperaments.TsundereId);
            tsundereCurious.Set(TemperamentAxes.Curiosity, 0);
            Assert.AreSame(PersonaTemperaments.Get(PersonaTemperaments.TsundereId), TemperamentMood.From(tsundereCurious));
            Assert.IsNull(TemperamentAxes.MatchingPresetId(tsundereCurious), "a different Curiosity is still stored as its own profile");
        }

        [Test]
        public void HighVolatility_SulksThenRecovers_AndNeverSetsAHostileFloor()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Volatility, 100);
            axes.Set(TemperamentAxes.Confidence, 0);
            var mood = TemperamentMood.From(axes);
            Assert.AreEqual(TemperamentAxes.CustomId, mood.Id);
            Assert.Greater(mood.Gain(CompanionMood.Annoyed, true), 1f);
            Assert.LessOrEqual(mood.Gain(CompanionMood.Annoyed, true), TemperamentMood.MaxMoodGain);
            Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Annoyed), TemperamentMood.MaxNegativeHalfLife);
            Assert.AreEqual(TemperamentMood.MaxNegativeHalfLife, mood.HalfLifeScale(CompanionMood.Anxious), 0.0001f);
            Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Sad), TemperamentMood.MaxNegativeHalfLife);
            Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Flustered), TemperamentMood.MaxNegativeHalfLife);
            Assert.AreEqual(0f, mood.RestBiasStrength, 0.0001f);
            Assert.AreNotEqual(CompanionMood.Annoyed, mood.Baseline);
            Assert.AreNotEqual(CompanionMood.Sad, mood.Baseline);
            Assert.AreEqual(1f, mood.Gain(CompanionMood.Intimate, true));
            Assert.Less(mood.RestFloor(CompanionMood.Annoyed), CompanionMoodTuning.LeaveStrength);

            // A custom profile near Tsundere keeps a low annoyed floor, then lets it fade off screen.
            var sulk = TemperamentAxes.Neutral();
            sulk.Set(TemperamentAxes.Warmth, 80);
            sulk.Set(TemperamentAxes.Volatility, 70);
            sulk.Set(TemperamentAxes.Assertiveness, 60);
            sulk.Set(TemperamentAxes.Directness, 20);
            var sulking = TemperamentMood.From(sulk);
            Assert.AreEqual(CompanionMood.Annoyed, sulking.RestBias);
            Assert.AreEqual(TemperamentMood.MaxBias, sulking.RestBiasStrength, 0.0001f);
            Assert.Less(sulking.RestBiasStrength, CompanionMoodTuning.LeaveStrength);

            var state = new CompanionMoodState { Temperament = sulking };
            state.Push(CompanionMood.Annoyed, 0.8f);
            Assert.AreEqual(CompanionMood.Annoyed, state.Shown);
            state.Tick(36000f, Day);
            Assert.AreEqual(CompanionMood.Content, state.Shown);
            Assert.AreEqual(TemperamentMood.MaxBias, state.Strength(CompanionMood.Annoyed), 0.0001f);
        }

        [Test]
        public void GeneratedCorners_StayInsideTheRecoveryCap()
        {
            var values = new int[TemperamentAxes.Count];
            var corners = new[] { 0, 100 };
            Walk(values, 0, corners);
        }

        static void Walk(int[] values, int axis, int[] corners)
        {
            if (axis == values.Length)
            {
                var axes = TemperamentAxes.Neutral();
                for (var i = 0; i < values.Length; i++)
                    axes.Set(i, values[i]);
                var mood = TemperamentMood.From(axes);
                Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Annoyed), TemperamentMood.MaxNegativeHalfLife);
                Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Anxious), TemperamentMood.MaxNegativeHalfLife);
                Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Sad), TemperamentMood.MaxNegativeHalfLife);
                Assert.LessOrEqual(mood.HalfLifeScale(CompanionMood.Flustered), TemperamentMood.MaxNegativeHalfLife);
                Assert.LessOrEqual(mood.Gain(CompanionMood.Annoyed, true), TemperamentMood.MaxMoodGain);
                Assert.LessOrEqual(mood.RestBiasStrength, TemperamentMood.MaxBias);
                Assert.AreNotEqual(CompanionMood.Annoyed, mood.Baseline);
                Assert.AreNotEqual(CompanionMood.Sad, mood.Baseline);
                Assert.AreNotEqual(CompanionMood.Anxious, mood.Baseline);
                Assert.AreEqual(1f, mood.Gain(CompanionMood.Intimate, true));
                return;
            }

            for (var i = 0; i < corners.Length; i++)
            {
                values[axis] = corners[i];
                Walk(values, axis + 1, corners);
            }
        }

        [Test]
        public void OldPersonaFile_LoadsThePresetPoint_AndACustomProfileRoundTripsEncrypted()
        {
            var old = PersonaJson.Parse("{\"id\":\"a\",\"displayName\":\"A\",\"temperament\":\"tsundere\"}");
            Assert.IsNull(old.Axes, "a file from before the sliders has no numbers");
            Assert.AreEqual(PersonaTemperaments.TsundereId, old.Temperament);
            var loaded = PisaraReading.Choose(old.TemperamentMode, old.Temperament, false, null, old.Axes);
            Assert.IsFalse(loaded.Auto);
            Assert.IsFalse(loaded.Custom);
            Assert.AreSame(PersonaTemperaments.Get(PersonaTemperaments.TsundereId), loaded.Temperament);
            Assert.IsTrue(loaded.Axes.Same(TemperamentAxes.ForPreset(PersonaTemperaments.TsundereId)));

            var named = new PersonaDefinition { Id = "b", DisplayName = "B", Temperament = "Moody" };
            StringAssert.DoesNotContain("temperamentAxes", PersonaJson.Write(named));
            Assert.AreEqual(PersonaTemperaments.MoodyId, PersonaJson.Parse(PersonaJson.Write(named)).Temperament);

            var custom = new PersonaDefinition { Id = "c", DisplayName = "C" };
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Warmth, 12);
            axes.Set(TemperamentAxes.Curiosity, 90);
            custom.SetAxes(axes);
            custom.TemperamentMode = "MANUAL";
            var json = PersonaJson.Write(custom);
            StringAssert.Contains("\"warmth\": 12", json);
            StringAssert.Contains("\"curiosity\": 90", json);
            StringAssert.DoesNotContain("\"temperament\": \"custom\"", json);
            var back = PersonaJson.Parse(json);
            Assert.AreEqual(TemperamentModes.Manual, back.TemperamentMode);
            Assert.AreEqual(string.Empty, back.Temperament);
            Assert.AreEqual(12, back.Axes.Value(TemperamentAxes.Warmth));
            Assert.AreEqual(90, back.Axes.Value(TemperamentAxes.Curiosity));
            Assert.AreEqual(50, back.Axes.Value(TemperamentAxes.Energy));
            Assert.IsTrue(back.Copy().Axes.Same(back.Axes));

            var collapsed = new PersonaDefinition { Id = "d", DisplayName = "D" };
            collapsed.SetAxes(TemperamentAxes.ForPreset("Tsundere"));
            Assert.IsNull(collapsed.Axes, "a point on a preset is stored as that preset id");
            Assert.AreEqual(PersonaTemperaments.TsundereId, collapsed.Temperament);

            var curiousPreset = new PersonaDefinition { Id = "e", DisplayName = "E", TemperamentMode = TemperamentModes.Manual };
            var kept = TemperamentAxes.ForPreset(PersonaTemperaments.TsundereId);
            kept.Set(TemperamentAxes.Curiosity, 90);
            curiousPreset.SetAxes(kept);
            Assert.AreEqual(90, curiousPreset.Axes.Value(TemperamentAxes.Curiosity));
            var keptChoice = PisaraReading.Choose(curiousPreset.TemperamentMode, curiousPreset.Temperament, false, null, curiousPreset.Axes);
            Assert.AreSame(PersonaTemperaments.Get(PersonaTemperaments.TsundereId), keptChoice.Temperament);
            Assert.AreEqual(90, keptChoice.Axes.Value(TemperamentAxes.Curiosity));
            Assert.IsFalse(keptChoice.Custom);

            var folder = Path.Combine(Path.GetTempPath(), "leia-axes-" + Guid.NewGuid().ToString("N"), "ProjectLeia");
            Directory.CreateDirectory(folder);
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = (byte)(i + 9);
            InstallKeyFile.KeyForPath = _ => key;
            try
            {
                var personas = SettingsLocations.PersonasFolder(folder);
                Assert.IsTrue(PersonaStore.Save(personas, custom));
                var bytes = File.ReadAllBytes(Path.Combine(personas, "c.json"));
                var plain = System.Text.Encoding.UTF8.GetString(bytes);
                StringAssert.DoesNotContain("warmth", plain);
                StringAssert.DoesNotContain("curiosity", plain);
                var catalog = PersonaCatalog.Load(new string[0], personas);
                var choice = PersonaRuntime.TemperamentChoiceFor(catalog, "c", null);
                Assert.IsFalse(choice.Auto);
                Assert.IsTrue(choice.Custom);
                Assert.AreEqual(12, choice.Axes.Value(TemperamentAxes.Warmth));
                Assert.AreEqual(TemperamentAxes.CustomId, choice.Temperament.Id);
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
        public void AutoAxes_WinOverPresetWeights_AndAManualPickWinsOverBoth()
        {
            var withAxes = CharacterSheet.Parse(
                "{\"temperament\":{\"calm\":0,\"cheerful\":0,\"shy\":0.9,\"tsundere\":0,\"moody\":0,\"reason\":\"quiet\"," +
                "\"axesSet\":true,\"axes\":{\"warmth\":10,\"volatility\":95,\"assertiveness\":50,\"playfulness\":50," +
                "\"openness\":50,\"energy\":50,\"directness\":50,\"confidence\":50,\"curiosity\":80}}}");
            Assert.IsNotNull(withAxes);
            Assert.AreEqual(10, withAxes.temperament.axes.Value(TemperamentAxes.Warmth));
            Assert.AreEqual(0.9f, withAxes.temperament.shy, 0.001f);

            var auto = PisaraReading.Choose("auto", "cheerful", false, withAxes, null);
            Assert.IsTrue(auto.Auto);
            Assert.AreEqual(TemperamentAxes.CustomId, auto.Temperament.Id);
            Assert.AreEqual(10, auto.Axes.Value(TemperamentAxes.Warmth));
            Assert.AreEqual(80, auto.Axes.Value(TemperamentAxes.Curiosity));
            StringAssert.Contains("Warmth 10", auto.Line);

            var manualPreset = PisaraReading.Choose("manual", "cheerful", false, withAxes, null);
            Assert.IsFalse(manualPreset.Auto);
            Assert.AreSame(PersonaTemperaments.Get(PersonaTemperaments.CheerfulId), manualPreset.Temperament);
            Assert.AreEqual("Temperament: Cheerful (your pick)", manualPreset.Line);

            var hand = TemperamentAxes.Neutral();
            hand.Set(TemperamentAxes.Warmth, 100);
            hand.Set(TemperamentAxes.Directness, 0);
            var manualAxes = PisaraReading.Choose("manual", "cheerful", false, withAxes, hand);
            Assert.IsFalse(manualAxes.Auto);
            Assert.IsTrue(manualAxes.Custom);
            Assert.AreEqual(100, manualAxes.Axes.Value(TemperamentAxes.Warmth));
            Assert.Greater(
                manualAxes.Temperament.Gain(CompanionMood.Affectionate, true),
                auto.Temperament.Gain(CompanionMood.Affectionate, true));

            // All 50s are "no opinion": the preset weights still decide, as they did before sliders.
            var flat = CharacterSheet.Parse(
                "{\"temperament\":{\"calm\":0.1,\"cheerful\":0.2,\"shy\":0,\"tsundere\":0.9,\"moody\":0.3,\"reason\":\"sharp\"," +
                "\"axesSet\":true,\"axes\":{\"warmth\":50,\"volatility\":50,\"assertiveness\":50,\"playfulness\":50," +
                "\"openness\":50,\"energy\":50,\"directness\":50,\"confidence\":50,\"curiosity\":50}}}");
            var fromWeights = PisaraReading.Choose("auto", "", false, flat, null);
            Assert.AreEqual(PersonaTemperaments.TsundereId, fromWeights.Temperament.Id);
            Assert.AreEqual("Temperament: Auto (Tsundere 90%): sharp", fromWeights.Line);

            var curiosityOnly = CharacterSheet.Parse(
                "{\"temperament\":{\"calm\":0.1,\"cheerful\":0.2,\"shy\":0,\"tsundere\":0.9,\"moody\":0.3,\"reason\":\"sharp\"," +
                "\"axesSet\":true,\"axes\":{\"warmth\":50,\"volatility\":50,\"assertiveness\":50,\"playfulness\":50," +
                "\"openness\":50,\"energy\":50,\"directness\":50,\"confidence\":50,\"curiosity\":80}}}");
            var curiosityChoice = PisaraReading.Choose("auto", "", false, curiosityOnly, null);
            Assert.AreSame(PersonaTemperaments.Get(PersonaTemperaments.TsundereId), curiosityChoice.Temperament);
            Assert.AreEqual(80, curiosityChoice.Axes.Value(TemperamentAxes.Curiosity));
            Assert.AreEqual("Temperament: Auto (Tsundere 90%): sharp", curiosityChoice.Line);

            var legacy = CharacterSheet.Parse(
                "{\"temperament\":{\"calm\":0.1,\"cheerful\":0.2,\"shy\":0,\"tsundere\":0.9,\"moody\":0.3,\"reason\":\"sharp\"}}");
            Assert.IsNull(legacy.temperament.axes);
            Assert.AreEqual(PersonaTemperaments.TsundereId, PisaraReading.Choose("auto", "", false, legacy, null).Temperament.Id);
        }

        [Test]
        public void AxesClamp_AndThePromptExampleParses()
        {
            var axes = TemperamentAxes.Neutral();
            axes.warmth = 140f;
            axes.volatility = float.NaN;
            axes.Clamp();
            Assert.AreEqual(100, axes.Value(TemperamentAxes.Warmth));
            Assert.AreEqual(50, axes.Value(TemperamentAxes.Volatility));

            var sheet = CharacterSheet.Parse(CharacterSheetPrompt.System);
            Assert.IsNotNull(sheet);
            Assert.IsTrue(sheet.temperament.axesSet);
            Assert.IsFalse(TemperamentAxes.HasOpinion(sheet.temperament.axes));
            StringAssert.Contains("\"curiosity\"", CharacterSheetPrompt.System);
            StringAssert.DoesNotContain("intimate", CharacterSheetPrompt.System.ToLowerInvariant());
        }
    }
}
