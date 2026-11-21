using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-155: the custom scenario box, and Speaking style folded into the description.</summary>
    public sealed class PersonaScenarioTests
    {
        static string Source(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/" + relative));
        }

        [Test]
        public void TheScenario_RoundTrips_AndIsCapped()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Scenario = "A rainy port city. She runs the night ferry.";
            var parsed = PersonaJson.Parse(PersonaJson.Write(persona));
            Assert.AreEqual(persona.Scenario, parsed.Scenario);
            Assert.AreEqual(persona.Scenario, persona.Copy().Scenario);

            persona.Scenario = new string('x', PersonaDefinition.MaxScenarioLength + 50);
            persona.Normalize();
            Assert.AreEqual(PersonaDefinition.MaxScenarioLength, persona.Scenario.Length);
        }

        [Test]
        public void TheChatPrompt_HasTheScenario_AfterHerAndBeforeTheExamples()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Personality = "Lola is a ferry captain.";
            persona.SpeakingStyle = string.Empty;
            persona.ExampleLines = "You: Hi!\nHer: Ahoy.";
            var without = PersonaText.Build(persona, "Lola", "friend");
            StringAssert.DoesNotContain(PersonaText.ScenarioLabel, without, "no scenario, no heading: old personas keep the same prompt");

            persona.Scenario = "A rainy port city.";
            var prompt = PersonaText.Build(persona, "Lola", "friend");
            var her = prompt.IndexOf("Lola is a ferry captain.", StringComparison.Ordinal);
            var scenario = prompt.IndexOf(PersonaText.ScenarioLabel + "\nA rainy port city.", StringComparison.Ordinal);
            var examples = prompt.IndexOf(PersonaText.ExamplesLabel, StringComparison.Ordinal);
            Assert.Less(her, scenario);
            Assert.Less(scenario, examples);
        }

        [Test]
        public void Pisara_TakesTheScenarioAsFact()
        {
            StringAssert.StartsWith("The world and what's going on", CharacterSheetPrompt.ScenarioHeading);
            StringAssert.Contains("headed \"The world and what's going on\"", CharacterSheetPrompt.System);
            StringAssert.Contains("Take relationship, sharedHistory and world.rightNow from it", CharacterSheetPrompt.System);
        }

        [Test]
        public void FoldStyle_MovesTheStyleIntoTheDescriptionOnce()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Description = "Lola is a ferry captain.";
            persona.SpeakingStyle = "Gruff and brief.";
            Assert.IsTrue(PersonaDescription.FoldStyle(persona));
            Assert.AreEqual("Lola is a ferry captain.\n\n" + PersonaDescription.StyleHeading + "Gruff and brief.", persona.Description);
            Assert.AreEqual(string.Empty, persona.SpeakingStyle);
            Assert.IsFalse(PersonaDescription.FoldStyle(persona), "nothing left to move");

            var already = PersonaDefinition.CreateStarter("mia");
            already.Description = "Mia. Gruff and brief.";
            already.SpeakingStyle = "Gruff and brief.";
            Assert.IsTrue(PersonaDescription.FoldStyle(already));
            Assert.AreEqual("Mia. Gruff and brief.", already.Description, "a style already in the text isn't added twice");
        }

        [Test]
        public void FoldStyle_KeepsACondensedText_AndAddsTheStyleToIt()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Description = new string('a', PersonaDescription.ModelBudget + 100);
            persona.Personality = "Lola, condensed.";
            persona.Condensed = true;
            persona.SpeakingStyle = "Gruff.";
            Assert.IsTrue(PersonaDescription.FoldStyle(persona));
            Assert.IsTrue(persona.Condensed);
            Assert.AreEqual("Lola, condensed.\n\n" + PersonaDescription.StyleHeading + "Gruff.", persona.Personality);
            PersonaDescription.ApplyTo(persona);
            StringAssert.StartsWith("Lola, condensed.", persona.Personality, "saving keeps Pisara's condensed text");
        }

        [Test]
        public void Templates_PutHowSheTalksInTheDescription()
        {
            foreach (var id in PersonaTemplates.Ids)
            {
                var persona = PersonaTemplates.Create(id, "draft");
                Assert.AreEqual(string.Empty, persona.SpeakingStyle, id);
                StringAssert.Contains(PersonaDescription.StyleHeading, persona.Description, id);
                Assert.AreEqual(persona.Description, persona.Personality, id);
            }
        }

        [Test]
        public void TheStudio_HasAScenarioBox_AndNoSpeakingStyleBox()
        {
            var studio = Source("UI/NewUI/PersonaStudioView.cs");
            StringAssert.Contains("\"Add custom scenario\"", studio);
            StringAssert.Contains("PersonaDefinition.MaxScenarioLength", studio);
            StringAssert.DoesNotContain("_form.Area(\"Speaking style\"", studio);
            StringAssert.Contains("PersonaDescription.FoldStyle(_draft)", studio);

            var tab = Source("UI/NewUI/HappiSettingsView.cs");
            StringAssert.DoesNotContain("Area(\"Speaking style\"", tab);
        }
    }
}
