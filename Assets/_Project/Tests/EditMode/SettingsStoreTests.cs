using System;
using System.IO;
using AICompanion.AI;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.UI;
using AICompanion.Voice;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class SettingsStoreTests
    {
        [Test]
        public void Defaults_AreTheSafePublicPath()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.Normalize();

            Assert.AreEqual(1, settings.Version);
            Assert.AreEqual(ChatDefaults.GrokId, settings.ProviderId);
            Assert.AreEqual(ChatDefaults.Model, settings.Model);
            Assert.AreEqual(ChatDefaults.LocalModel, settings.LocalModel);
            Assert.AreEqual(ChatDefaults.LocalBaseUrl, settings.LocalBaseUrl);
            Assert.AreEqual(ChatDefaults.TimeoutSeconds, settings.TimeoutSeconds);
            Assert.IsFalse(settings.PrivateMode);
            Assert.IsFalse(settings.ChatHistory);
            Assert.IsTrue(settings.HideTaskbarButton);
            Assert.AreEqual(OverlayFrameRate.Idle, settings.IdleFrameRate);
            Assert.AreEqual(1f, settings.Scale, 0.001f);
            Assert.AreEqual("eve", settings.VoiceId);
            Assert.AreEqual(1f, settings.Volume, 0.001f);
            Assert.AreEqual("V", settings.PushToTalkKey);
            Assert.AreEqual(SttSettings.DefaultModelFile, settings.WhisperModel);
            Assert.AreEqual(OverlayClickThroughPolicy.SilhouetteName, settings.ClickThrough);
            Assert.IsTrue(settings.AlwaysOnTop);
            Assert.AreEqual(ChatDefaults.GrokId, OutboundPrivacy.ChatProviderId(settings.PrivateMode, settings.ProviderId));
        }

        [Test]
        public void RoundTrip_PreservesChoicesAndLeavesTheKeyOut()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.ProviderId = ChatDefaults.LocalId;
            settings.Model = "grok-4.7";
            settings.LocalModel = "llama3.2";
            settings.TimeoutSeconds = 45f;
            settings.AvatarSource = "vrm";
            settings.VoiceId = "ara";
            settings.Volume = 0.4f;
            settings.Muted = true;
            settings.PushToTalkKey = "F11";
            settings.WhisperModel = "ggml-small.bin";
            settings.Scale = 1.25f;
            settings.ClickThrough = "capture";
            settings.IdleFrameRate = 8;
            settings.HideTaskbarButton = false;
            settings.StartWithWindows = true;
            settings.Paused = true;
            settings.ChatHistory = true;
            settings.PrivateMode = true;
            settings.AlwaysOnTop = false;

            var json = CompanionSettingsJson.Write(settings);
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(json));
            Assert.IsFalse(json.Contains("xai-"));
            Assert.IsFalse(json.Contains("apiKey"));

            var loaded = CompanionSettingsJson.Parse(json, SettingsLegacyPrefs.None);
            Assert.AreEqual(ChatDefaults.LocalId, loaded.ProviderId);
            Assert.AreEqual("ara", loaded.VoiceId);
            Assert.AreEqual(0.4f, loaded.Volume, 0.001f);
            Assert.IsTrue(loaded.Muted);
            Assert.AreEqual("F11", loaded.PushToTalkKey);
            Assert.AreEqual("ggml-small.bin", loaded.WhisperModel);
            Assert.AreEqual(1.25f, loaded.Scale, 0.001f);
            Assert.AreEqual("capture", loaded.ClickThrough);
            Assert.AreEqual(8, loaded.IdleFrameRate);
            Assert.IsFalse(loaded.HideTaskbarButton);
            Assert.IsTrue(loaded.StartWithWindows);
            Assert.IsTrue(loaded.Paused);
            Assert.IsTrue(loaded.ChatHistory);
            Assert.IsTrue(loaded.PrivateMode);
            Assert.IsFalse(loaded.AlwaysOnTop);
            Assert.AreEqual("vrm", loaded.AvatarSource);
            Assert.AreEqual(45f, loaded.TimeoutSeconds, 0.001f);
            Assert.AreEqual(ChatDefaults.LocalId, OutboundPrivacy.ChatProviderId(loaded.PrivateMode, ChatDefaults.GrokId));
        }

        [Test]
        public void Save_WritesAUserFileAndReadsItBack()
        {
            var directory = Path.Combine(Path.GetTempPath(), "leia-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "settings.json");
                var settings = CompanionSettings.CreateDefault();
                settings.PrivateMode = true;
                settings.ChatHistory = true;
                CompanionSettingsStore.Save(path, settings);

                var loaded = CompanionSettingsStore.Load(path, new SettingsLegacyPrefs
                {
                    HasMuted = true,
                    Muted = true
                });
                Assert.IsTrue(loaded.PrivateMode);
                Assert.IsTrue(loaded.ChatHistory);
                Assert.IsFalse(loaded.Muted);
                var text = File.ReadAllText(path);
                Assert.IsFalse(text.Contains("xai-"));
                Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(text));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingFile_MigratesLegacyPrefs()
        {
            var legacy = new SettingsLegacyPrefs
            {
                HasHideTaskbar = true,
                HideTaskbar = false,
                HasScale = true,
                Scale = 1.5f,
                HasMuted = true,
                Muted = true,
                HasPaused = true,
                Paused = true
            };

            var loaded = CompanionSettingsJson.Parse("  ", legacy);
            Assert.IsFalse(loaded.HideTaskbarButton);
            Assert.AreEqual(1.5f, loaded.Scale, 0.001f);
            Assert.IsTrue(loaded.Muted);
            Assert.IsTrue(loaded.Paused);
            Assert.IsFalse(loaded.PrivateMode);
            Assert.AreEqual(ChatDefaults.GrokId, loaded.ProviderId);
            Assert.AreEqual(1, loaded.Version);
        }

        [Test]
        public void VersionZero_MigratesOldNamesAndDropsSecrets()
        {
            const string secret = "xai-supersecretvalue1234";
            var json = "{\"version\":0,\"hideTaskbar\":0,\"windowScale\":1.1,\"provider\":\"local\",\"apiKey\":\""
                + secret + "\",\"xaiApiKey\":\"" + secret + "\"}";
            var legacy = new SettingsLegacyPrefs { HasMuted = true, Muted = true, HasPaused = true, Paused = true };
            var loaded = CompanionSettingsJson.Parse(json, legacy);
            Assert.IsFalse(loaded.HideTaskbarButton);
            Assert.AreEqual(1.1f, loaded.Scale, 0.001f);
            Assert.AreEqual(ChatDefaults.LocalId, loaded.ProviderId);
            Assert.IsFalse(loaded.Muted);
            Assert.IsFalse(loaded.Paused);

            var saved = CompanionSettingsJson.Write(loaded);
            Assert.IsFalse(saved.Contains(secret));
            Assert.IsFalse(saved.Contains("apiKey"));
            Assert.IsFalse(saved.Contains("xaiApiKey"));
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(saved));
            Assert.AreEqual(1, loaded.Version);
        }

        [Test]
        public void CurrentFile_IgnoresLegacyPrefsAndUnknownFields()
        {
            var legacy = new SettingsLegacyPrefs { HasHideTaskbar = true, HideTaskbar = false, HasMuted = true, Muted = true };
            var loaded = CompanionSettingsJson.Parse(
                "{\"version\":2,\"providerId\":\"local\",\"hideTaskbarButton\":true,\"futureFlag\":true,\"model\":\"grok-4.7\",\"localModel\":\"mistral\"}",
                legacy);
            Assert.AreEqual(ChatDefaults.LocalId, loaded.ProviderId);
            Assert.IsTrue(loaded.HideTaskbarButton);
            Assert.IsFalse(loaded.Muted);
            Assert.AreEqual("mistral", loaded.LocalModel);
            Assert.AreEqual("grok-4.7", loaded.Model);
            var saved = CompanionSettingsJson.Write(loaded);
            Assert.IsFalse(saved.Contains("futureFlag"));
            Assert.IsFalse(saved.Contains("\"version\": 2"));
        }

        [Test]
        public void Normalize_RejectsBadKeysUrlsAndModels()
        {
            var loaded = CompanionSettingsJson.Parse(
                "{\"version\":1,\"providerId\":\"nope\",\"timeoutSeconds\":-4,\"volume\":9,\"scale\":9,\"idleFrameRate\":0,\"clickThrough\":\"nope\",\"avatarSource\":\"daz\",\"pushToTalkKey\":\"F8\",\"whisperModel\":\"evil.exe\",\"localBaseUrl\":\"https://user:xai-secretsecret@evil.test/v1\"}",
                SettingsLegacyPrefs.None);
            Assert.AreEqual(ChatDefaults.GrokId, loaded.ProviderId);
            Assert.AreEqual(ChatDefaults.TimeoutSeconds, loaded.TimeoutSeconds);
            Assert.AreEqual(1f, loaded.Volume, 0.001f);
            Assert.AreEqual(OverlayWindowSize.MaxScale, loaded.Scale, 0.001f);
            Assert.AreEqual(1, loaded.IdleFrameRate);
            Assert.AreEqual(OverlayClickThroughPolicy.SilhouetteName, loaded.ClickThrough);
            Assert.AreEqual("auto", loaded.AvatarSource);
            Assert.AreEqual("V", loaded.PushToTalkKey);
            Assert.AreEqual(SttSettings.DefaultModelFile, loaded.WhisperModel);
            Assert.AreEqual(ChatDefaults.LocalBaseUrl, loaded.LocalBaseUrl);
            Assert.IsFalse(CompanionSettingsJson.Write(loaded).Contains("xai-secret"));
        }

        [Test]
        public void Mask_DoesNotEchoTheSecret()
        {
            const string secret = "xai-supersecretvalue1234";
            Assert.AreEqual("Key saved", ApiKeyMask.Label(true));
            Assert.AreEqual("No key saved", ApiKeyMask.Label(false));
            Assert.IsFalse(ApiKeyMask.Label(true).Contains(secret));
            Assert.IsFalse(ApiKeyMask.Label(true).ToLowerInvariant().Contains("xai-"));
            Assert.AreEqual("••••••••", ApiKeyMask.Mask(secret));
            Assert.IsFalse(ApiKeyMask.Mask(secret).Contains(secret));
            Assert.IsFalse(ApiKeyMask.Mask(secret).ToLowerInvariant().Contains("xai"));
            Assert.AreEqual(string.Empty, ApiKeyMask.Mask(null));
            Assert.AreEqual(string.Empty, ApiKeyMask.Mask(string.Empty));
        }

        [Test]
        public void Probe_DoesNotLogTheKeyOrSendItInPrivateMode()
        {
            const string secret = "xai-abcdefghijklmnopqrstuvwxyz";
            var local = ConnectionProbe.Plan(true, ChatDefaults.GrokId, "https://api.x.ai/v1", ChatDefaults.LocalBaseUrl);
            Assert.IsTrue(local.Allowed);
            Assert.IsFalse(local.SendAuthorization);
            Assert.IsTrue(local.Url.StartsWith("http://127.0.0.1:11434/v1/models"));
            Assert.IsFalse(local.Url.Contains("api.x.ai"));
            Assert.IsFalse(local.Summary.Contains(secret));

            var cloud = ConnectionProbe.Plan(false, ChatDefaults.GrokId, "https://api.x.ai/v1", ChatDefaults.LocalBaseUrl);
            Assert.IsTrue(cloud.SendAuthorization);
            Assert.IsTrue(cloud.Url.StartsWith("https://api.x.ai/v1/models"));
            Assert.IsFalse(cloud.Summary.Contains(secret));
            Assert.IsFalse(cloud.Summary.ToLowerInvariant().Contains("bearer"));

            var unsafePlan = ConnectionProbe.Plan(false, ChatDefaults.GrokId, "https://api.x.ai/v1?api_key=" + secret, ChatDefaults.LocalBaseUrl);
            Assert.IsFalse(unsafePlan.Allowed);
            Assert.IsFalse(unsafePlan.Summary.Contains(secret));
            Assert.IsFalse(unsafePlan.Url.Contains(secret));

            var logged = ConnectionProbe.FormatResult(401, "Authorization: Bearer " + secret, "{\"error\":\"bad Bearer " + secret + " key\"}");
            Assert.IsFalse(logged.Contains(secret));
            Assert.IsFalse(logged.Contains("Bearer " + secret.Substring(0, 8)));
            StringAssert.Contains("HTTP 401", logged);
        }

        [Test]
        public void Hotkeys_ListTheCurrentBindingsWithoutRebinding()
        {
            var entries = HotkeyCatalog.Current("b");
            Assert.IsTrue(Contains(entries, "F6", "Mute"));
            Assert.IsTrue(Contains(entries, "F7", "avatar"));
            Assert.IsTrue(Contains(entries, "F8", "top"));
            Assert.IsTrue(Contains(entries, "F12", "settings"));
            Assert.IsTrue(Contains(entries, "B hold", "Push"));
            StringAssert.Contains("later", HotkeyCatalog.RebindNote);
        }

        [Test]
        public void History_RoundTripIgnoresSecretFields()
        {
            const string secret = "xai-supersecretvalue1234";
            var conversation = new ChatConversation();
            Assert.IsTrue(conversation.ImportLine(ChatLineKind.User, "hello"));
            Assert.IsTrue(conversation.ImportLine(ChatLineKind.Assistant, "hi there"));
            var written = ChatHistoryCodec.Write(conversation.Lines);
            Assert.IsFalse(written.Contains(secret));
            Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(written));

            var stuffed = "{\"apiKey\":\"" + secret + "\",\"lines\":[{\"role\":\"user\",\"text\":\"hello\"},{\"role\":\"assistant\",\"text\":\"hi\"}]}";
            var restored = new ChatConversation();
            ChatHistoryCodec.ReadInto(stuffed, restored);
            Assert.AreEqual(2, restored.Lines.Count);
            Assert.AreEqual("hello", restored.Lines[0].Text);
            Assert.AreEqual("hi", restored.Lines[1].Text);
            var again = ChatHistoryCodec.Write(restored.Lines);
            Assert.IsFalse(again.Contains(secret));

            conversation.ClearTranscript();
            Assert.AreEqual(0, conversation.Lines.Count);
        }

        [Test]
        public void Locations_StayOutsideTheRepo()
        {
            var folder = SettingsLocations.Folder(@"C:\Users\me\AppData\Roaming", @"C:\ignored");
            Assert.AreEqual(Path.Combine(@"C:\Users\me\AppData\Roaming", "ProjectLeia"), folder);
            Assert.AreEqual(Path.Combine(folder, "settings.json"), SettingsLocations.SettingsFile(folder));
            Assert.AreEqual(Path.Combine(folder, "chat-history.json"), SettingsLocations.HistoryFile(folder));
            Assert.IsFalse(folder.Contains("ignored"));
            Assert.IsNull(SettingsLocations.Folder(null, null));
            Assert.AreEqual(Path.Combine(@"C:\fallback", "ProjectLeia"), SettingsLocations.Folder(null, @"C:\fallback"));
        }

        static bool Contains(HotkeyEntry[] entries, string keys, string actionPart)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Keys == keys && entries[i].Action.IndexOf(actionPart, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
