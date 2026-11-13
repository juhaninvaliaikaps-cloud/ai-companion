using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    // Happi is the default persona: new installs and the wizard start with her, existing users keep theirs.
    public sealed class HappiPersonaTests
    {
        static PersonaCatalog BuiltIns()
        {
            return PersonaCatalog.Load(PersonaFiles.ReadBuiltInTexts(), Path.Combine(Path.GetTempPath(), "happi-none-" + Guid.NewGuid().ToString("N")));
        }

        [Test]
        public void FreshInstall_PicksHappi()
        {
            Assert.AreEqual(PersonaDefaults.HappiId, PersonaDefaults.ForLaunch(false, string.Empty));
            Assert.AreEqual(PersonaDefaults.HappiId, PersonaDefaults.ForLaunch(false, null));
            // A brand-new settings object (what a new install loads) then starts as Happi.
            var settings = CompanionSettingsJson.Parse(null, SettingsLegacyPrefs.None);
            settings.PersonaId = PersonaDefaults.ForLaunch(false, settings.PersonaId);
            Assert.AreEqual("happi", settings.PersonaId);
        }

        [Test]
        public void ExistingSelection_IsKept()
        {
            Assert.AreEqual("custom-5", PersonaDefaults.ForLaunch(true, "custom-5"));
            Assert.AreEqual("friend", PersonaDefaults.ForLaunch(true, "friend"), "Leia stays Leia");
            Assert.AreEqual(string.Empty, PersonaDefaults.ForLaunch(true, string.Empty), "the setup-name companion is not switched");

            var saved = CompanionSettings.CreateDefault();
            saved.PersonaId = "coach";
            var loaded = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(saved), SettingsLegacyPrefs.None);
            Assert.AreEqual("coach", PersonaDefaults.ForLaunch(true, loaded.PersonaId));
        }

        [Test]
        public void Happi_IsCheerful()
        {
            var catalog = BuiltIns();
            var happi = catalog.Find(PersonaDefaults.HappiId);
            Assert.IsNotNull(happi);
            Assert.AreEqual(PersonaTemperaments.CheerfulId, happi.Temperament);
            Assert.AreEqual(PersonaTemperaments.CheerfulId, PersonaRuntime.TemperamentFor(catalog, PersonaDefaults.HappiId).Id);
            Assert.AreEqual("Mood temperament: happi = cheerful", MoodTemperamentLog.Line("happi", PersonaRuntime.TemperamentFor(catalog, "happi")));
        }

        [Test]
        public void Happi_IsABuiltInWithTheGivenPrompt_AndLeiaStays()
        {
            var catalog = BuiltIns();
            var entry = catalog.FindEntry(PersonaDefaults.HappiId);
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.BuiltIn);
            Assert.AreEqual("Happi", entry.Persona.DisplayName);
            StringAssert.StartsWith("You are Happi, a cheerful catgirl", entry.Persona.Personality);
            StringAssert.Contains("Put actions in *asterisks*", entry.Persona.Personality);
            StringAssert.Contains("Stay in character as Happi.", entry.Persona.Personality);
            Assert.LessOrEqual(entry.Persona.Personality.Length, PersonaDefinition.MaxPersonalityLength, "the prompt is not cut");

            // Applying Happi keeps the user's provider, model, voices and avatar.
            Assert.AreEqual(string.Empty, entry.Persona.ProviderId);
            Assert.AreEqual(string.Empty, entry.Persona.Model);
            Assert.AreEqual(string.Empty, entry.Persona.CloudVoiceId);
            Assert.AreEqual(string.Empty, entry.Persona.PiperVoiceId);
            Assert.AreEqual(string.Empty, entry.Persona.AvatarSource);

            Assert.AreEqual("Leia", catalog.Find("friend").DisplayName, "Leia is still a built-in");
        }

        [Test]
        public void HappiSelected_UsesHerPromptAndKeepsTheConsentRules()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.PersonaId = PersonaDefaults.HappiId;
            var prompt = PersonaRuntime.SystemPrompt(settings, BuiltIns());
            StringAssert.StartsWith("You are Happi", prompt);
            var full = AICompanion.AnimationDirector.DirectorPrompt.Append(prompt, "suffix");
            StringAssert.Contains(IntimateBoundaries.PromptLine(false), full, "the consent rule is still added after any persona");
        }

        [Test]
        public void Wizard_DefaultsToHappi()
        {
            var settings = CompanionSettings.CreateDefault();
            Assert.AreEqual("Happi", PersonaDefaults.WizardName(settings));

            SetupWizardFlow.ApplySkip(settings, SetupWizardFlow.Personality);
            Assert.AreEqual(PersonaDefaults.HappiId, settings.PersonaId, "Skip keeps Happi");

            settings.PersonaId = string.Empty;
            SetupWizardFlow.ApplyPersonality(settings, "Happi", RelationshipStyles.Friend);
            Assert.AreEqual(PersonaDefaults.HappiId, settings.PersonaId);
            Assert.AreEqual("Happi", PersonaDefaults.WizardName(settings));

            SetupWizardFlow.ApplyPersonality(settings, "  ", RelationshipStyles.Friend);
            Assert.AreEqual(PersonaDefaults.HappiId, settings.PersonaId, "an empty name keeps Happi");
        }

        [Test]
        public void Wizard_OwnNameOrGirlfriend_MakesTheUsersOwnCompanion()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.PersonaId = PersonaDefaults.HappiId;
            SetupWizardFlow.ApplyPersonality(settings, "Mika", RelationshipStyles.Friend);
            Assert.AreEqual(string.Empty, settings.PersonaId);
            Assert.AreEqual("Mika", settings.CompanionName);
            Assert.AreEqual("Mika", PersonaDefaults.WizardName(settings));

            settings.PersonaId = PersonaDefaults.HappiId;
            SetupWizardFlow.ApplyPersonality(settings, "Happi", RelationshipStyles.Girlfriend);
            Assert.AreEqual(string.Empty, settings.PersonaId, "Girlfriend uses the setup-name companion as before");
            Assert.AreEqual(RelationshipStyles.Girlfriend, settings.Relationship);
        }

        [Test]
        public void ExistingSettingsFile_IsUnchangedOnDisk()
        {
            // No new settings key: an old file round-trips with its persona id as it was.
            var old = "{\n  \"version\": 1,\n  \"personaId\": \"custom-5\"\n}\n";
            var loaded = CompanionSettingsJson.Parse(old, SettingsLegacyPrefs.None);
            Assert.AreEqual("custom-5", loaded.PersonaId);
            StringAssert.Contains("\"personaId\": \"custom-5\"", CompanionSettingsJson.Write(loaded));
        }
    }
}
