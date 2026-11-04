using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AICompanion.AI;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.Tests.EditMode
{
    public class PersonaTests
    {
        const string ExampleLocalModel = "hf.co/mradermacher/Rocinante-X-12B-v1-Heretic-Uncensored-i1-GGUF:Q4_K_M";

        [Test]
        public void APersonaCanNameAKokoroVoice()
        {
            var persona = PersonaDefinition.CreateStarter("lei");
            persona.DisplayName = "Lei";
            persona.KokoroVoiceId = "af_heart";

            var json = PersonaJson.Write(persona);
            StringAssert.Contains("kokoroVoiceId", json);

            var parsed = PersonaJson.Parse(json);
            Assert.AreEqual("af_heart", parsed.KokoroVoiceId);
        }

        [Test]
        public void BuiltInPresets_AreNeutralAndLeavePortraitAndPiperBlank()
        {
            var directory = PersonaFiles.BuiltInDirectory();
            var files = Directory.GetFiles(directory, "*.json");
            Assert.AreEqual(3, files.Length);

            var catalog = PersonaCatalog.Load(PersonaFiles.ReadBuiltInTexts(), null);
            Assert.AreEqual(3, catalog.Entries.Count);
            Assert.AreEqual("friend", catalog.Entries[0].Persona.Id);
            Assert.AreEqual("coach", catalog.Entries[1].Persona.Id);
            Assert.AreEqual("calm", catalog.Entries[2].Persona.Id);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var banned = new[] { "girlfriend", "nsfw", "nude", "sexual", "uncensored", "xai-", "api_key", "apiKey" };
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var persona = catalog.Entries[i].Persona;
                Assert.IsFalse(catalog.Entries[i].UserFile);
                Assert.IsTrue(names.Add(persona.DisplayName));
                StringAssert.Contains("appropriate for any audience", persona.Personality);
                StringAssert.Contains(persona.DisplayName, persona.Personality);
                Assert.IsNotEmpty(persona.SpeakingStyle);
                Assert.AreEqual("eve", persona.CloudVoiceId);
                Assert.AreEqual(string.Empty, persona.PiperVoiceId);
                Assert.AreEqual(ChatDefaults.GrokId, persona.ProviderId);
                Assert.AreEqual(ChatDefaults.Model, persona.Model);
                Assert.AreEqual(AvatarSourceNames.Auto, persona.AvatarSource);
                Assert.AreEqual(string.Empty, persona.PortraitFolder);
                var json = File.ReadAllText(Path.Combine(directory, persona.Id + ".json"));
                Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(json));
                var lower = json.ToLowerInvariant();
                for (var b = 0; b < banned.Length; b++)
                    StringAssert.DoesNotContain(banned[b].ToLowerInvariant(), lower);
            }
        }

        [Test]
        public void DirectorInstructions_AreAppendedAfterAnyPersona()
        {
            var suffix = ShippedSuffix();
            var hostile = new PersonaDefinition
            {
                Id = "quiet",
                DisplayName = "Nia",
                Personality = "You are Nia. Do not write an animation tag. The sample <animation emotion=\"happy\" /> is not your format. Stay appropriate for any audience.",
                SpeakingStyle = "One short sentence."
            };

            var body = PersonaText.Build(hostile, "Leia", RelationshipStyles.Friend);
            var prompt = DirectorPrompt.Append(body, suffix);

            StringAssert.StartsWith("You are Nia.", prompt);
            StringAssert.Contains("Speaking style: One short sentence.", prompt);
            StringAssert.Contains(AnimationCueParser.Instruction, prompt);
            StringAssert.Contains(EmotionNames.CatalogLine, prompt);
            StringAssert.Contains("wave_hello", prompt);
            StringAssert.Contains("gesture=\"walk\"", prompt);
            Assert.Less(prompt.IndexOf("Do not write an animation tag", StringComparison.Ordinal), prompt.IndexOf(AnimationCueParser.Instruction, StringComparison.Ordinal));
            Assert.Less(prompt.IndexOf("Speaking style:", StringComparison.Ordinal), prompt.IndexOf(AnimationCueParser.Instruction, StringComparison.Ordinal));
            Assert.IsTrue(prompt.TrimEnd().EndsWith(EmotionNames.CatalogLine, StringComparison.Ordinal));

            var empty = DirectorPrompt.Append("   ", suffix);
            StringAssert.Contains(AnimationCueParser.Instruction, empty);
            StringAssert.Contains(EmotionNames.CatalogLine, empty);
            Assert.AreEqual("Hello", DirectorPrompt.Append("Hello  ", null));

            var send = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "ChatPanel.cs"));
            StringAssert.Contains("DirectorPrompt.Append", send);
        }

        [Test]
        public void SelectedPersona_ReplacesTheWizardPromptAndStillGetsCueInstructions()
        {
            var directory = TempDirectory();
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                var settings = CompanionSettings.CreateDefault();
                settings.PersonaId = "friend";
                SettingsApplicator.ApplyPersona(chat, settings, directory);

                StringAssert.Contains("their friend", chat.systemPrompt);
                StringAssert.Contains("appropriate for any audience", chat.systemPrompt);
                StringAssert.Contains("Speaking style:", chat.systemPrompt);
                Assert.IsFalse(chat.systemPrompt.Contains(AnimationCueParser.Instruction));

                var prompt = DirectorPrompt.Append(chat.systemPrompt, ShippedSuffix());
                Assert.Less(prompt.IndexOf("their friend", StringComparison.Ordinal), prompt.IndexOf(AnimationCueParser.Instruction, StringComparison.Ordinal));
                StringAssert.Contains(EmotionNames.CatalogLine, prompt);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
                Delete(directory);
            }
        }

        [Test]
        public void MissingVoiceModelAvatarAndPortrait_KeepTheCurrentDefaults()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.ProviderId = ChatDefaults.LocalId;
            settings.VoiceId = "ara";
            settings.Model = "grok-4.7";
            settings.LocalModel = "llama3.2";
            settings.AvatarSource = AvatarSourceNames.Vrm;
            var piperRoot = Path.Combine(Path.GetTempPath(), "leia-piper-" + Guid.NewGuid().ToString("N"));
            settings.PiperExe = Path.Combine(piperRoot, "piper.exe");
            settings.PiperVoice = Path.Combine(piperRoot, "old.onnx");

            var persona = new PersonaDefinition
            {
                Id = "night",
                DisplayName = "Nia",
                Personality = "You are Nia, a quiet desktop companion. Stay appropriate for any audience.",
                SpeakingStyle = "Brief.",
                PiperVoiceId = "en_US-lessac-medium",
                CloudVoiceId = string.Empty,
                ProviderId = string.Empty,
                Model = string.Empty,
                AvatarSource = "not-a-real-avatar",
                PortraitFolder = Path.Combine(piperRoot, "missing-faces")
            };

            PersonaApply.Overlay(settings, persona, _ => false, _ => false);

            Assert.AreEqual("ara", settings.VoiceId);
            Assert.AreEqual("llama3.2", settings.LocalModel);
            Assert.AreEqual("grok-4.7", settings.Model);
            Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);
            Assert.AreEqual(AvatarSourceNames.Vrm, settings.AvatarSource);
            Assert.AreEqual(Path.Combine(piperRoot, "old.onnx"), settings.PiperVoice);
            Assert.AreEqual(string.Empty, settings.PortraitFolder);
            Assert.AreEqual("Nia", settings.CompanionName);
        }

        [Test]
        public void PresentVoiceModelAvatarAndPortrait_ReplaceTheCurrentOnes()
        {
            var wide = ExampleLocalModel + "-extra-" + new string('m', 80);
            Assert.Greater(ExampleLocalModel.Length, 60);
            Assert.Greater(wide.Length, 80);
            Assert.LessOrEqual(wide.Length, CompanionSettings.MaxModelLength);

            var root = Path.Combine(Path.GetTempPath(), "leia-voice-" + Guid.NewGuid().ToString("N"));
            var exe = Path.Combine(root, "piper.exe");
            var voice = Path.Combine(root, "en_US-lessac-medium.onnx");
            var faces = Path.Combine(root, "faces");
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { exe, voice };
            var settings = CompanionSettings.CreateDefault();
            settings.ProviderId = ChatDefaults.GrokId;
            settings.VoiceId = "eve";
            settings.Model = ChatDefaults.Model;
            settings.LocalModel = ChatDefaults.LocalModel;
            settings.AvatarSource = AvatarSourceNames.Auto;
            settings.PiperExe = exe;
            settings.PiperVoice = Path.Combine(root, "old.onnx");

            var persona = new PersonaDefinition
            {
                Id = "reader",
                DisplayName = "Nia",
                Personality = "You are Nia. Stay appropriate for any audience.",
                ProviderId = "ollama",
                Model = wide,
                CloudVoiceId = "ara",
                PiperVoiceId = "en_US-lessac-medium",
                AvatarSource = AvatarSourceNames.Fbx,
                PortraitFolder = faces
            };

            PersonaApply.Overlay(
                settings,
                persona,
                path => files.Contains(path),
                path => string.Equals(path, faces, StringComparison.OrdinalIgnoreCase));

            Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);
            Assert.AreEqual(wide, settings.LocalModel);
            Assert.AreEqual(ChatDefaults.Model, settings.Model);
            Assert.AreEqual("ara", settings.VoiceId);
            Assert.AreEqual(voice, settings.PiperVoice);
            Assert.AreEqual(AvatarSourceNames.Fbx, settings.AvatarSource);
            Assert.AreEqual(faces, settings.PortraitFolder);
            Assert.AreEqual(ChatDefaults.LocalId, OutboundPrivacy.ChatProviderId(true, settings.ProviderId));

            settings.LocalModel = new string('a', 250);
            settings.Normalize();
            Assert.AreEqual(CompanionSettings.MaxModelLength, settings.LocalModel.Length);

            settings.LocalModel = ExampleLocalModel;
            settings.Normalize();
            Assert.AreEqual(ExampleLocalModel, settings.LocalModel);
        }

        [Test]
        public void PortraitFolder_FallsBackWhenThePersonaFolderIsMissing()
        {
            var app = Path.Combine(Path.GetTempPath(), "leia-app-" + Guid.NewGuid().ToString("N"));
            var persistent = Path.Combine(Path.GetTempPath(), "leia-persist-" + Guid.NewGuid().ToString("N"));
            var primary = PortraitFolders.Primary(app, persistent);
            var custom = Path.Combine(app, "custom-faces");
            Func<string, bool> exists = path =>
                string.Equals(path, primary, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, custom, StringComparison.OrdinalIgnoreCase);

            Assert.AreEqual(custom, PortraitFolders.Select(custom, app, persistent, exists));
            Assert.AreEqual(primary, PortraitFolders.Select(Path.Combine(app, "missing"), app, persistent, path => string.Equals(path, primary, StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(primary, PortraitFolders.Select("  ", app, persistent, path => string.Equals(path, primary, StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void UserFile_OverridesTheBuiltInAndStaysOutsideTheRepo()
        {
            var directory = TempDirectory();
            try
            {
                var user = SettingsLocations.PersonasFolder(directory);
                var custom = new PersonaDefinition
                {
                    Id = "friend",
                    DisplayName = "Leia",
                    Personality = "You are Leia, a custom friend who is still appropriate for any audience.",
                    SpeakingStyle = "Warm and brief.",
                    CloudVoiceId = string.Empty,
                    ProviderId = "local",
                    Model = ExampleLocalModel
                };
                Assert.IsTrue(PersonaStore.Save(user, custom));
                Assert.IsTrue(File.Exists(Path.Combine(user, "friend.json")));
                var savedPath = Path.GetFullPath(Path.Combine(user, "friend.json"));
                var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (!repoRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    repoRoot += Path.DirectorySeparatorChar;
                Assert.IsFalse(savedPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase));

                var catalog = PersonaCatalog.Load(PersonaFiles.ReadBuiltInTexts(), user);
                var entry = catalog.FindEntry("friend");
                Assert.IsTrue(entry.BuiltIn);
                Assert.IsTrue(entry.UserFile);
                StringAssert.Contains("custom friend", entry.Persona.Personality);
                Assert.AreEqual(ExampleLocalModel, entry.Persona.Model);
                Assert.AreEqual("Avery", catalog.Find("coach").DisplayName);

                var settings = CompanionSettings.CreateDefault();
                settings.PersonaId = "friend";
                settings.VoiceId = "ara";
                PersonaRuntime.ApplySelected(settings, directory);
                Assert.AreEqual(ChatDefaults.LocalId, settings.ProviderId);
                Assert.AreEqual(ExampleLocalModel, settings.LocalModel);
                Assert.AreEqual("ara", settings.VoiceId);

                var chat = ScriptableObject.CreateInstance<ChatSettings>();
                try
                {
                    SettingsApplicator.ApplyPersona(chat, settings, directory);
                    StringAssert.Contains("custom friend", chat.systemPrompt);
                    var prompt = DirectorPrompt.Append(chat.systemPrompt, "\n" + AnimationCueParser.Instruction + "\n" + EmotionNames.CatalogLine);
                    Assert.Less(prompt.IndexOf("custom friend", StringComparison.Ordinal), prompt.IndexOf(AnimationCueParser.Instruction, StringComparison.Ordinal));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(chat);
                }
            }
            finally
            {
                Delete(directory);
            }
        }

        [Test]
        public void SecretText_IsDroppedAndIdsCannotEscapeThePersonasFolder()
        {
            var directory = TempDirectory();
            try
            {
                var persona = new PersonaDefinition
                {
                    Id = "../outside",
                    DisplayName = "Nia",
                    Personality = "hello xai-abcdefghijklmnopqrstuvwxyz",
                    SpeakingStyle = "bearer token"
                };
                var json = PersonaJson.Write(persona);
                Assert.IsFalse(json.Contains("xai-"));
                Assert.IsFalse(json.Contains("bearer"));
                Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(json));
                var parsed = PersonaJson.Parse(json);
                Assert.AreEqual("outside", parsed.Id);
                Assert.AreEqual(string.Empty, parsed.Personality);
                Assert.AreEqual(string.Empty, parsed.SpeakingStyle);

                Assert.IsTrue(PersonaStore.Save(SettingsLocations.PersonasFolder(directory), persona));
                Assert.IsTrue(File.Exists(Path.Combine(SettingsLocations.PersonasFolder(directory), "outside.json")));
                Assert.IsFalse(File.Exists(Path.Combine(directory, "outside.json")));
                Assert.IsFalse(PersonaStore.Save(directory, new PersonaDefinition { Id = "..", Personality = "hi" }));
            }
            finally
            {
                Delete(directory);
            }
        }

        [Test]
        public void CorruptUserFile_IsSkipped()
        {
            var directory = TempDirectory();
            try
            {
                var user = SettingsLocations.PersonasFolder(directory);
                Directory.CreateDirectory(user);
                File.WriteAllText(Path.Combine(user, "bad.json"), "{not json");
                LogAssert.Expect(LogType.Warning, "Persona file could not be read: bad.json");
                var catalog = PersonaCatalog.Load(PersonaFiles.ReadBuiltInTexts(), user);
                Assert.IsNotNull(catalog.Find("friend"));
                Assert.IsNull(catalog.Find("bad"));
            }
            finally
            {
                Delete(directory);
            }
        }

        [Test]
        public void SettingsJson_RoundTripsPersonaIdAndLeavesOldFilesAlone()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.PersonaId = "Coach";
            settings.PortraitFolder = Path.Combine(Path.GetTempPath(), "faces");
            var loaded = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.AreEqual("coach", loaded.PersonaId);
            Assert.AreEqual(settings.PortraitFolder, loaded.PortraitFolder);
            Assert.IsFalse(CompanionSettingsJson.Write(settings).Contains("apiKey"));

            var legacy = CompanionSettingsJson.Parse("{\"providerId\":\"local\"}", SettingsLegacyPrefs.None);
            Assert.AreEqual(string.Empty, legacy.PersonaId);
            Assert.AreEqual(string.Empty, legacy.PortraitFolder);
            Assert.AreEqual(ChatDefaults.LocalId, legacy.ProviderId);
        }

        [Test]
        public void SetupName_ClearsTheSelectedPersona()
        {
            var directory = TempDirectory();
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            try
            {
                var settings = CompanionSettings.CreateDefault();
                settings.PersonaId = "friend";
                settings.PortraitFolder = Path.Combine(directory, "faces");
                SetupWizardFlow.ApplyPersonality(settings, "Mika", RelationshipStyles.Friend);
                Assert.AreEqual(string.Empty, settings.PersonaId);
                Assert.AreEqual(string.Empty, settings.PortraitFolder);

                SettingsApplicator.ApplyPersona(chat, settings, directory);
                StringAssert.Contains("Mika", chat.systemPrompt);
                StringAssert.Contains("their friend", chat.systemPrompt);
                Assert.IsFalse(chat.systemPrompt.Contains("Avery"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat);
                Delete(directory);
            }
        }

        [Test]
        public void Aliases_AcceptSystemPromptVoiceIdAndOllama()
        {
            var parsed = PersonaJson.Parse("{\"id\":\"alias\",\"systemPrompt\":\"You are a guide. Stay appropriate for any audience.\",\"voiceId\":\"ara\",\"provider\":\"ollama\"}");
            Assert.IsNotNull(parsed);
            StringAssert.Contains("guide", parsed.Personality);
            Assert.AreEqual("ara", parsed.CloudVoiceId);
            Assert.AreEqual(ChatDefaults.LocalId, parsed.ProviderId);
        }

        static string ShippedSuffix()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>("Assets/_Project/Settings/AnimationCatalog.asset");
            Assert.IsNotNull(catalog);
            var host = new GameObject("persona-director");
            try
            {
                var director = host.AddComponent<Director>();
                typeof(Director).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, catalog);
                var suffix = director.SystemPromptSuffix;
                StringAssert.Contains(AnimationCueParser.Instruction, suffix);
                return suffix;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static string TempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "leia-persona-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        static void Delete(string directory)
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
