using System;
using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class SetupWizardTests
    {
        const string Secret = "xai-supersecretvalue1234";

        [Test]
        public void FirstRun_RequiresBothSettingsAndKeyToBeMissing()
        {
            Assert.IsTrue(SetupWizardGate.ShouldPresent(false, false, false));
            Assert.IsFalse(SetupWizardGate.ShouldPresent(true, false, false));
            Assert.IsFalse(SetupWizardGate.ShouldPresent(false, true, false));
            Assert.IsFalse(SetupWizardGate.ShouldPresent(true, true, false));
        }

        [Test]
        public void ForceFlag_ShowsTheWizardForAnExistingUser()
        {
            Assert.IsTrue(SetupWizardGate.ShouldPresent(true, true, true));
            Assert.IsTrue(SetupWizardGate.ShouldPresent(true, false, true));
            Assert.IsTrue(SetupWizardGate.ShouldPresent(false, true, true));
            Assert.IsTrue(SetupWizardGate.ShouldPresentOnLaunch(true, true, new[] { "BackseatCatgirl.exe", "--Setup" }));
            Assert.IsTrue(SetupWizardGate.CommandRequestsWizard(new[] { "--setup" }));
            Assert.IsFalse(SetupWizardGate.CommandRequestsWizard(new[] { "BackseatCatgirl.exe", "--setup-now", "-batchmode" }));
            Assert.IsFalse(SetupWizardGate.CommandRequestsWizard(null));
            Assert.IsFalse(SetupWizardGate.CommandRequestsWizard(new string[0]));
            Assert.AreEqual("--setup", SetupWizardGate.ForceArgument);
        }

        [Test]
        public void ExistingSettingsJson_IsNotAFirstRunEvenWithoutAKey()
        {
            var directory = TempDirectory();
            try
            {
                var path = Path.Combine(directory, "settings.json");
                var prior = CompanionSettings.CreateDefault();
                prior.ProviderId = ChatDefaults.GrokId;
                prior.AvatarSource = AvatarSourceNames.Vrm;
                CompanionSettingsStore.Save(path, prior);

                Assert.IsTrue(File.Exists(path));
                Assert.IsFalse(SetupWizardGate.ShouldPresent(File.Exists(path), false, false));

                var text = File.ReadAllText(path);
                Assert.IsFalse(text.Contains(Secret));
                Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(text));
                Assert.IsFalse(text.Contains("\"apiKey\""));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PreWizardSettings_KeepTheOriginalPersona()
        {
            var loaded = CompanionSettingsJson.Parse(
                "{\"version\":1,\"providerId\":\"grok\",\"privateMode\":true,\"avatarSource\":\"fbx\"}",
                SettingsLegacyPrefs.None);
            Assert.AreEqual(string.Empty, loaded.CompanionName);
            Assert.AreEqual(RelationshipStyles.Friend, loaded.Relationship);
            Assert.AreEqual("fbx", loaded.AvatarSource);
            Assert.IsTrue(loaded.PrivateMode);
            Assert.AreEqual(ChatDefaults.SystemPrompt, CompanionPersona.Build(loaded.CompanionName, loaded.Relationship));
            Assert.IsFalse(SetupWizardGate.ShouldPresent(true, false, false));
            Assert.IsFalse(SetupWizardGate.ShouldPresent(false, true, false));
        }

        [Test]
        public void Skip_AppliesDefaultsAndLeavesTheKeyOutOfSettings()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.AvatarSource = AvatarSourceNames.Vrm;
            settings.CompanionName = "Mika";
            settings.Relationship = RelationshipStyles.Girlfriend;
            settings.ProviderId = ChatDefaults.GrokId;

            SetupWizardFlow.ApplySkip(settings, SetupWizardFlow.Welcome);
            Assert.AreEqual(AvatarSourceNames.Vrm, settings.AvatarSource);
            Assert.AreEqual("Mika", settings.CompanionName);

            SetupWizardFlow.ApplySkip(settings, SetupWizardFlow.Ai);
            Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);
            Assert.AreEqual(AvatarSourceNames.Vrm, settings.AvatarSource);

            SetupWizardFlow.ApplySkip(settings, SetupWizardFlow.Avatar);
            Assert.AreEqual(AvatarSourceNames.Auto, settings.AvatarSource);
            Assert.AreEqual("Mika", settings.CompanionName);

            SetupWizardFlow.ApplySkip(settings, SetupWizardFlow.Personality);
            Assert.AreEqual(CompanionPersona.DefaultName, settings.CompanionName);
            Assert.AreEqual(RelationshipStyles.Friend, settings.Relationship);
            Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);

            SetupWizardFlow.ApplySkip(settings, SetupWizardFlow.Tour);
            var json = CompanionSettingsJson.Write(settings);
            Assert.IsFalse(json.Contains(Secret));
            Assert.IsFalse(json.ToLowerInvariant().Contains("apikey"));
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(json));
            Assert.IsFalse(json.Contains("xai-"));
            StringAssert.Contains("\"providerId\": \"local\"", json);
            StringAssert.Contains("\"avatarSource\": \"auto\"", json);
            StringAssert.Contains("\"companionName\": \"Leia\"", json);
            StringAssert.Contains("\"relationship\": \"friend\"", json);
        }

        [Test]
        public void BackNextAndSkip_KeepEarlierChoicesUntilThatStepIsSkipped()
        {
            var settings = CompanionSettings.CreateDefault();
            var step = SetupWizardFlow.Welcome;
            Assert.IsFalse(SetupWizardFlow.CanGoBack(step));
            Assert.AreEqual("Welcome", SetupWizardFlow.Title(step));

            step = SetupWizardFlow.Forward(step);
            Assert.AreEqual(SetupWizardFlow.Ai, step);
            Assert.IsTrue(SetupWizardFlow.CanGoBack(step));
            SetupWizardFlow.ApplyAiContinue(settings, false);
            Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);

            step = SetupWizardFlow.Forward(step);
            Assert.AreEqual(SetupWizardFlow.Avatar, step);
            SetupWizardFlow.ApplyAvatar(settings, AvatarSourceNames.Fbx);
            step = SetupWizardFlow.Back(step);
            Assert.AreEqual(SetupWizardFlow.Ai, step);
            Assert.AreEqual(AvatarSourceNames.Fbx, settings.AvatarSource);
            Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);

            SetupWizardFlow.ApplyAiContinue(settings, true);
            Assert.AreEqual(ChatDefaults.GrokId, settings.ProviderId);
            Assert.AreEqual(AvatarSourceNames.Fbx, settings.AvatarSource);

            step = SetupWizardFlow.Forward(step);
            step = SetupWizardFlow.Forward(step);
            Assert.AreEqual(SetupWizardFlow.Personality, step);
            SetupWizardFlow.ApplyPersonality(settings, "  Mika  ", "Girlfriend");
            Assert.AreEqual("Mika", settings.CompanionName);
            Assert.AreEqual(RelationshipStyles.Girlfriend, settings.Relationship);

            step = SetupWizardFlow.Back(step);
            Assert.AreEqual(SetupWizardFlow.Avatar, step);
            Assert.AreEqual("Mika", settings.CompanionName);
            Assert.AreEqual(AvatarSourceNames.Fbx, settings.AvatarSource);

            step = SetupWizardFlow.Forward(step);
            SetupWizardFlow.ApplySkip(settings, step);
            Assert.AreEqual(CompanionPersona.DefaultName, settings.CompanionName);
            Assert.AreEqual(RelationshipStyles.Friend, settings.Relationship);
            Assert.AreEqual(AvatarSourceNames.Fbx, settings.AvatarSource);

            step = SetupWizardFlow.Forward(step);
            Assert.IsTrue(SetupWizardFlow.IsLast(step));
            Assert.AreEqual("Controls", SetupWizardFlow.Title(step));
            step = SetupWizardFlow.Forward(step);
            Assert.IsTrue(SetupWizardFlow.IsComplete(step));
            Assert.IsFalse(SetupWizardFlow.CanGoBack(SetupWizardFlow.Welcome));
        }

        [Test]
        public void Persistence_RoundTripsWizardChoicesAndNeverWritesTheKey()
        {
            var directory = TempDirectory();
            try
            {
                var settingsPath = Path.Combine(directory, "settings.json");
                var secretsPath = Path.Combine(directory, "secrets.json");
                var settings = CompanionSettings.CreateDefault();
                SetupWizardFlow.ApplyAiContinue(settings, true);
                SetupWizardFlow.ApplyAvatar(settings, AvatarSourceNames.Vrm);
                SetupWizardFlow.ApplyPersonality(settings, "Mika", RelationshipStyles.Girlfriend);
                CompanionSettingsStore.Save(settingsPath, settings);
                ApiKeyFile.Save(secretsPath, Secret, new PlainSecretProtector());

                var settingsText = File.ReadAllText(settingsPath);
                var secretsText = File.ReadAllText(secretsPath);
                Assert.IsFalse(settingsText.Contains(Secret));
                Assert.IsFalse(settingsText.ToLowerInvariant().Contains("apikey"));
                Assert.IsFalse(settingsText.ToLowerInvariant().Contains("bearer"));
                Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(settingsText));
                Assert.AreNotEqual(settingsPath, secretsPath);
                StringAssert.Contains("secrets.json", secretsPath);
                Assert.IsFalse(settingsPath.Replace('\\', '/').Contains("/Assets/"));
                Assert.IsFalse(secretsPath.Replace('\\', '/').Contains("/Assets/"));

                string key;
                Assert.IsTrue(ApiKeyFile.TryReadKey(secretsPath, new PlainSecretProtector(), out key));
                Assert.AreEqual(Secret, key);
                Assert.IsTrue(secretsText.Contains(Secret));
                Assert.IsFalse(settingsText.Contains(Secret));

                var loaded = CompanionSettingsStore.Load(settingsPath, new SettingsLegacyPrefs { HasMuted = true, Muted = true });
                Assert.AreEqual(ChatDefaults.GrokId, loaded.ProviderId);
                Assert.AreEqual(AvatarSourceNames.Vrm, loaded.AvatarSource);
                Assert.AreEqual("Mika", loaded.CompanionName);
                Assert.AreEqual(RelationshipStyles.Girlfriend, loaded.Relationship);
                Assert.IsFalse(loaded.Muted);
                Assert.IsFalse(SetupWizardGate.ShouldPresent(true, true, false));

                var prompt = CompanionPersona.Build(loaded.CompanionName, loaded.Relationship);
                StringAssert.Contains("Mika", prompt);
                StringAssert.Contains("girlfriend", prompt.ToLowerInvariant());
                StringAssert.Contains("appropriate for any audience", prompt);
                Assert.IsFalse(prompt.Contains(Secret));
                Assert.IsFalse(prompt.ToLowerInvariant().Contains("uthana"));
                Assert.IsFalse(prompt.ToLowerInvariant().Contains("api key"));
                Assert.IsFalse(prompt.ToLowerInvariant().Contains("xai"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PastedKey_IsStrippedFromTheNameAndFromSettingsJson()
        {
            var settings = CompanionSettings.CreateDefault();
            SetupWizardFlow.ApplyPersonality(settings, Secret, RelationshipStyles.Girlfriend);
            Assert.AreEqual(string.Empty, settings.CompanionName);
            Assert.AreEqual(RelationshipStyles.Girlfriend, settings.Relationship);

            var json = CompanionSettingsJson.Write(settings);
            Assert.IsFalse(json.Contains(Secret));
            Assert.IsFalse(json.Contains("xai-"));
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(json));

            var prompt = CompanionPersona.Build(settings.CompanionName, settings.Relationship);
            Assert.IsFalse(prompt.Contains(Secret));
            StringAssert.Contains(CompanionPersona.DefaultName, prompt);

            var friend = CompanionSettings.CreateDefault();
            SetupWizardFlow.ApplyPersonality(friend, "bearer " + Secret, RelationshipStyles.Friend);
            Assert.AreEqual(string.Empty, friend.CompanionName);
            Assert.AreEqual(ChatDefaults.SystemPrompt, CompanionPersona.Build(friend.CompanionName, friend.Relationship));
            Assert.IsFalse(CompanionSettingsJson.Write(friend).Contains(Secret));
        }

        [Test]
        public void Persona_IsAppliedToChatSettings()
        {
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                var settings = CompanionSettings.CreateDefault();
                SettingsApplicator.ApplyPersona(chat, settings);
                Assert.AreEqual(ChatDefaults.SystemPrompt, chat.systemPrompt);

                settings.CompanionName = "Mika";
                settings.Relationship = RelationshipStyles.Girlfriend;
                SettingsApplicator.ApplyPersona(chat, settings);
                StringAssert.Contains("Mika", chat.systemPrompt);
                StringAssert.Contains("girlfriend", chat.systemPrompt.ToLowerInvariant());
                StringAssert.Contains("appropriate for any audience", chat.systemPrompt);
                Assert.IsFalse(chat.systemPrompt.Contains(Secret));
                Assert.IsFalse(chat.systemPrompt.ToLowerInvariant().Contains("ollama"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
            }
        }

        [Test]
        public void Relationship_NormalizesUnknownValuesToFriend()
        {
            Assert.AreEqual(RelationshipStyles.Friend, RelationshipStyles.Normalize(null));
            Assert.AreEqual(RelationshipStyles.Friend, RelationshipStyles.Normalize(" "));
            Assert.AreEqual(RelationshipStyles.Friend, RelationshipStyles.Normalize("partner"));
            Assert.AreEqual(RelationshipStyles.Girlfriend, RelationshipStyles.Normalize("GIRLFRIEND"));
            Assert.AreEqual("Friend", RelationshipStyles.Label("nope"));
            Assert.AreEqual("Girlfriend", RelationshipStyles.Label(RelationshipStyles.Girlfriend));
        }

        [Test]
        public void WelcomeAndTour_UseTheProductNameAndTheControlsList()
        {
            Assert.AreEqual("Backseat Catgirl", AppName.Display);
            StringAssert.StartsWith("Backseat Catgirl", SetupWizardCopy.WelcomeLead());
            StringAssert.Contains("no main menu", SetupWizardCopy.WelcomeLead());
            StringAssert.Contains("xAI", SetupWizardCopy.WelcomeCloud());
            StringAssert.Contains("Grok", SetupWizardCopy.WelcomeCloud());
            StringAssert.Contains("Ollama", SetupWizardCopy.WelcomeLocal());
            StringAssert.Contains("cloud TTS", SetupWizardCopy.WelcomeLocal());
            StringAssert.Contains("Uthana", SetupWizardCopy.WelcomeLocal());
            StringAssert.Contains("settings.json", SetupWizardCopy.AiNote());
            StringAssert.Contains("secrets.json", SetupWizardCopy.AiNote());
            StringAssert.Contains("local", SetupWizardCopy.AiSkipNote().ToLowerInvariant());

            var notes = SetupWizardCopy.TourNotes();
            Assert.IsTrue(Contains(notes, "tray"));
            Assert.IsTrue(Contains(notes, "Backseat Catgirl"));
            Assert.IsTrue(Contains(notes, "Drag"));
            Assert.IsTrue(Contains(notes, "rotate"));
            var hotkeys = SetupWizardFlow.TourHotkeys("F11");
            Assert.IsTrue(HasKey(hotkeys, "F6"));
            Assert.IsTrue(HasKey(hotkeys, "F7"));
            Assert.IsTrue(HasKey(hotkeys, "F8"));
            Assert.IsTrue(HasKey(hotkeys, "F10"));
            Assert.IsTrue(HasKey(hotkeys, "F12"));
            Assert.IsTrue(HasKey(hotkeys, "F11 hold"));
            Assert.AreEqual(HotkeyCatalog.Current("F11").Length, hotkeys.Length);
            StringAssert.Contains("Step 1 of 5", SetupWizardCopy.StepLabel(SetupWizardFlow.Welcome));
            StringAssert.Contains("Controls", SetupWizardCopy.StepLabel(SetupWizardFlow.Tour));
        }

        static string TempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "leia-setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        static bool Contains(string[] lines, string part)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && lines[i].IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static bool HasKey(HotkeyEntry[] entries, string keys)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Keys == keys)
                    return true;
            }

            return false;
        }
    }
}
