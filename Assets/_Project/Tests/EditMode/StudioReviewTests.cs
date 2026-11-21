using System.IO;
using System.Text.RegularExpressions;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-152: the Studio's Voice, Look and Review steps, the greeting and the test chat.</summary>
    public sealed class StudioReviewTests
    {
        static string Source(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/" + relative));
        }

        [Test]
        public void TheGreeting_RoundTrips_AndIsCapped()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Greeting = "Oh, you're back!";
            Assert.AreEqual("Oh, you're back!", PersonaJson.Parse(PersonaJson.Write(persona)).Greeting);
            persona.Greeting = new string('a', PersonaDefinition.MaxGreetingLength + 50);
            persona.Normalize();
            Assert.AreEqual(PersonaDefinition.MaxGreetingLength, persona.Greeting.Length);
        }

        [Test]
        public void TheTestChat_UsesTheTextTheChatModelWillGet()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Description = new string('a', 3000) + "\n\n" + new string('b', 3000);
            persona.Personality = "You are Lola, condensed.";
            persona.Condensed = true;
            persona.ExampleLines = "You: Hi!\nHer: Hey.";
            var copy = persona.Copy();
            PersonaDescription.ApplyTo(copy);
            var prompt = PersonaText.Build(copy, "Lola", RelationshipRoles.Friend);
            StringAssert.StartsWith("You are Lola, condensed.", prompt);
            StringAssert.Contains("Her: Hey.", prompt);
            Assert.AreEqual(new string('a', 3000) + "\n\n" + new string('b', 3000), persona.Description, "the draft keeps its description");
        }

        [Test]
        public void TheTestChat_SavesNothing_AndKeepsThePrivacyRule()
        {
            var source = Source("UI/ChatPanel.Studio.cs");
            StringAssert.Contains("NoMemory = true", source);
            StringAssert.Contains("BlockCloud = keepsLocal", source);
            StringAssert.Contains("PisaraReader.MayUse(provider, keepsLocal)", source);
            StringAssert.Contains("IntimateBoundaries.PromptLine(false)", source);
            StringAssert.DoesNotContain("_conversation.Add", Regex.Match(source, @"RequestTestReply[\s\S]*").Value, "a test reply never enters the real chat");
        }

        [Test]
        public void SheGreets_AfterLaunch_ASwitch_AndClearingHistory()
        {
            Assert.AreEqual(1, Regex.Matches(Source("Settings/SettingsPanel.Persona.cs"), @"GreetIfEmpty\(\);").Count, "persona switch");
            Assert.AreEqual(2, Regex.Matches(Source("Settings/SettingsPanel.cs"), @"GreetIfEmpty\(\);").Count, "launch and clearing history");
            StringAssert.Contains("_conversation.Lines.Count > 0", Source("UI/ChatPanel.Studio.cs"), "a chat with lines is left alone");
        }

        [Test]
        public void TheStudio_HasAllSevenSteps()
        {
            StringAssert.Contains("{ \"Start\", \"Identity\", \"Description\", \"Pisara\", \"Voice\", \"Look\", \"Review\" }", Source("UI/NewUI/PersonaStudioView.cs"));
        }
    }
}
