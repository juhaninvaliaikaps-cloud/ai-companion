using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-136 follow-ups: sliders a reading sends without the flag, and the nine sliders in Profile.</summary>
    public sealed class TemperamentFollowUpTests
    {
        const string Sliders = "\"axes\":{\"warmth\":15,\"volatility\":90,\"assertiveness\":50,\"playfulness\":50," +
                               "\"openness\":50,\"energy\":50,\"directness\":50,\"confidence\":50,\"curiosity\":50}";

        [TestCase("")]
        [TestCase("\"axesSet\":false,")]
        [TestCase("\"axesSet\":\"true\",")]
        [TestCase("\"axesSet\":1,")]
        public void ASheetWithSliders_KeepsThem_EvenWithoutAProperFlag(string flag)
        {
            var sheet = CharacterSheet.Parse("{\"temperament\":{\"calm\":0,\"cheerful\":0,\"shy\":0.9,\"tsundere\":0,\"moody\":0," + flag + Sliders + "}}");
            Assert.IsNotNull(sheet, "a reading with an odd flag is still a reading");
            Assert.IsTrue(sheet.temperament.axesSet);
            Assert.IsNotNull(sheet.temperament.axes);
            Assert.AreEqual(15, sheet.temperament.axes.Value(TemperamentAxes.Warmth));
            Assert.AreEqual(90, sheet.temperament.axes.Value(TemperamentAxes.Volatility));

            var auto = PisaraReading.Choose("auto", "", false, sheet, null);
            Assert.AreEqual(15, auto.Axes.Value(TemperamentAxes.Warmth), "Auto uses the sliders, not the preset blend");
        }

        [Test]
        public void AllMiddleSliders_WithoutTheFlag_StillMeanNoOpinion()
        {
            var sheet = CharacterSheet.Parse("{\"temperament\":{\"tsundere\":0.9,\"axes\":{\"warmth\":50,\"volatility\":50}}}");
            Assert.IsFalse(sheet.temperament.axesSet);
            Assert.IsNull(sheet.temperament.axes);
            Assert.AreEqual(PersonaTemperaments.TsundereId, PisaraReading.Choose("auto", "", false, sheet, null).Temperament.Id);
        }

        [Test]
        public void ASavedSheet_KeepsTheStrictFlag()
        {
            var sheet = CharacterSheet.FromJson("{\"version\":1,\"temperament\":{\"axesSet\":false," + Sliders + "}}");
            Assert.IsNotNull(sheet);
            Assert.IsFalse(sheet.temperament.axesSet, "only a model reading is read generously");
        }

        [Test]
        public void Profile_ShowsTheNineSliders_WithTheirEnds()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Warmth, 80);
            axes.Set(TemperamentAxes.Volatility, 20);
            var view = ProfilePersonality.Build(null, "Temperament: Custom (your pick)", false, 50f, axes);
            Assert.IsTrue(view.HasSliders);
            Assert.AreEqual(TemperamentAxes.Count, view.Sliders.Length);
            Assert.AreEqual(80f, view.Sliders[TemperamentAxes.Warmth]);
            Assert.AreEqual("warm", ProfilePersonality.SliderWord(TemperamentAxes.Warmth, 80f));
            Assert.AreEqual("steady", ProfilePersonality.SliderWord(TemperamentAxes.Volatility, 20f));
            Assert.AreEqual("in between", ProfilePersonality.SliderWord(TemperamentAxes.Energy, 50f));

            for (var i = 0; i < TemperamentAxes.Count; i++)
            {
                string low;
                string high;
                ProfilePersonality.SliderEnds(i, out low, out high);
                Assert.IsNotEmpty(low, TemperamentAxes.Names[i]);
                Assert.IsNotEmpty(high, TemperamentAxes.Names[i]);
                Assert.IsFalse(char.IsDigit(low[0]), "the number is stripped: " + low);
            }

            Assert.IsFalse(ProfilePersonality.Build(null, "x", false, 50f).HasSliders, "no sliders known: the radar is the fallback");
        }
    }
}
