using System;
using System.IO;
using System.Text;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AICompanion.Tests.EditMode
{
    public class LocalStorageTests
    {
        readonly System.Collections.Generic.List<string> _cleanup = new System.Collections.Generic.List<string>();

        [TearDown]
        public void TearDown()
        {
            InstallKeyFile.KeyForPath = null;
            SecretWritePolicy.IsWindowsPlayer = () => Application.platform == RuntimePlatform.WindowsPlayer;
            for (var i = 0; i < _cleanup.Count; i++)
            {
                try
                {
                    if (Directory.Exists(_cleanup[i]))
                        Directory.Delete(_cleanup[i], true);
                }
                catch (IOException)
                {
                }
            }

            _cleanup.Clear();
        }

        [Test]
        public void AesGcm_MatchesKnownAnswers()
        {
            AssertRoundTrip(
                Hex("0000000000000000000000000000000000000000000000000000000000000000"),
                Hex("000000000000000000000000"),
                new byte[0],
                new byte[0],
                new byte[0],
                Hex("530f8afbc74536b9a963b4f1c4cb738b"));

            AssertRoundTrip(
                Hex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"),
                Hex("000102030405060708090a0b"),
                Encoding.UTF8.GetBytes("memory.json"),
                Encoding.UTF8.GetBytes("hello"),
                Hex("2f67ba77aa"),
                Hex("030e3ebb753877bf7d35950b256888b1"));
        }

        [Test]
        public void AesGcm_RejectsATamperedTagAndAWrongName()
        {
            var key = Hex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
            var nonce = Hex("000102030405060708090a0b");
            var aad = Encoding.UTF8.GetBytes("memory.json");
            var envelope = LocalAesGcm.Seal(key, nonce, aad, Encoding.UTF8.GetBytes("hello"));
            envelope[envelope.Length - 1] ^= 0x01;
            byte[] plain;
            Assert.IsFalse(LocalAesGcm.TryOpen(key, aad, envelope, out plain));

            var again = LocalAesGcm.Seal(key, nonce, aad, Encoding.UTF8.GetBytes("hello"));
            Assert.IsFalse(LocalAesGcm.TryOpen(key, Encoding.UTF8.GetBytes("chat-history.json"), again, out plain));
        }

        [Test]
        public void KeyPolicy_RefusesAnUnprotectedKeyOnAWindowsPlayer()
        {
            Assert.AreEqual(LocalKeyDecision.Dpapi, LocalKeyPolicy.Choose(true, true));
            Assert.AreEqual(LocalKeyDecision.Refuse, LocalKeyPolicy.Choose(true, false));
            Assert.AreEqual(LocalKeyDecision.LabeledFallback, LocalKeyPolicy.Choose(false, false));
            Assert.AreEqual(LocalKeyDecision.Dpapi, LocalKeyPolicy.Choose(false, true));

            Assert.IsTrue(SecretWritePolicy.AllowUnprotectedPayload(false, false, true));
            Assert.IsFalse(SecretWritePolicy.AllowUnprotectedPayload(true, true, true));
            Assert.IsTrue(SecretWritePolicy.AllowUnprotectedPayload(true, true, false));
        }

        [Test]
        public void InstallKey_LabeledFallbackIsObviousAndRoundTrips()
        {
            var root = NewFolder("keys");
            var first = InstallKeyFile.LoadOrCreate(root, false, false, null, null);
            var text = File.ReadAllText(Path.Combine(root, InstallKeyFile.FileName));
            StringAssert.StartsWith(LocalKeyPolicy.DevFallbackLabel, text);
            var second = InstallKeyFile.LoadOrCreate(root, false, false, null, null);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void InstallKey_DpapiBlobRoundTripsWithAStandIn()
        {
            var root = NewFolder("dpapi");
            var first = InstallKeyFile.LoadOrCreate(root, false, true, Wrap, Unwrap);
            var bytes = File.ReadAllBytes(Path.Combine(root, InstallKeyFile.FileName));
            Assert.AreEqual((byte)'L', bytes[0]);
            Assert.AreEqual((byte)'E', bytes[1]);
            Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains(LocalKeyPolicy.DevFallbackLabel));
            CollectionAssert.AreEqual(first, InstallKeyFile.LoadOrCreate(root, false, true, Wrap, Unwrap));
        }

        [Test]
        public void InstallKey_WindowsPlayerDoesNotWriteAFallbackWhenProtectionFails()
        {
            var root = NewFolder("refuse");
            LogAssert.Expect(LogType.Warning, "Install key was not created. Windows data protection failed, so private files stay unwritten.");
            var key = InstallKeyFile.LoadOrCreate(root, true, true, _ => throw new InvalidOperationException("no"), Unwrap);
            Assert.IsNull(key);
            Assert.IsFalse(File.Exists(Path.Combine(root, InstallKeyFile.FileName)));
        }

        [Test]
        public void InstallKey_WindowsPlayerIgnoresALabeledFallbackFile()
        {
            var root = NewFolder("ignore-fallback");
            InstallKeyFile.LoadOrCreate(root, false, false, null, null);
            LogAssert.Expect(LogType.Warning, "Install key is an unprotected fallback. The Windows player will not use it.");
            Assert.IsNull(InstallKeyFile.LoadOrCreate(root, true, true, Wrap, Unwrap));
            StringAssert.StartsWith(LocalKeyPolicy.DevFallbackLabel, File.ReadAllText(Path.Combine(root, InstallKeyFile.FileName)));
        }

        [Test]
        public void ApiKeyFile_WindowsPlayerDoesNotStoreAPlainKeyWhenProtectionThrows()
        {
            var path = Path.Combine(NewFolder("secret"), "secrets.json");
            SecretWritePolicy.IsWindowsPlayer = () => true;
            Assert.Throws<InvalidOperationException>(() => ApiKeyFile.Save(path, "test-export-key", new ThrowingProtector()));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void Write_RefusesPlaintextWhenTheKeyIsUnavailable()
        {
            var path = Path.Combine(NewFolder("nokey"), "note.json");
            InstallKeyFile.KeyForPath = _ => null;
            LogAssert.Expect(LogType.Warning, "Local file was not written because the install key is unavailable: note.json");
            Assert.IsFalse(ProtectedLocalFile.Write(path, "ZephyrUnique"));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void Commit_KeepsTheOriginalWhenTheEncryptedCopyDoesNotVerify()
        {
            var path = Path.Combine(NewFolder("commit"), "memory.json");
            File.WriteAllText(path, "ORIGINAL");
            var kept = ProtectedLocalFile.CommitIfVerified(
                path,
                Encoding.UTF8.GetBytes("ORIGINAL"),
                Encoding.UTF8.GetBytes("nope"),
                _ => Encoding.UTF8.GetBytes("WRONG"));
            Assert.IsFalse(kept);
            Assert.AreEqual("ORIGINAL", File.ReadAllText(path));

            var next = Encoding.UTF8.GetBytes("NEXT");
            Assert.IsTrue(ProtectedLocalFile.CommitIfVerified(path, next, next, bytes => bytes));
            CollectionAssert.AreEqual(next, File.ReadAllBytes(path));
        }

        [Test]
        public void RoundTrip_EncryptsTextAndKeepsTheKeyLabelOffTheDataFile()
        {
            var root = NewRoot();
            var path = Path.Combine(root, "chat-history.json");
            const string text = "hello \"there\"\nhéllo";
            Assert.IsTrue(ProtectedLocalFile.Write(path, text));
            var raw = File.ReadAllBytes(path);
            Assert.IsTrue(LocalAesGcm.IsMagic(raw));
            Assert.IsFalse(Encoding.UTF8.GetString(raw).Contains("hello"));
            var read = ProtectedLocalFile.Read(path);
            Assert.AreEqual(LocalReadStatus.Ready, read.Status);
            Assert.AreEqual(text, read.Text);

            var keyBytes = File.ReadAllBytes(Path.Combine(root, InstallKeyFile.FileName));
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                Assert.IsTrue(Encoding.ASCII.GetString(keyBytes, 0, 8) == "LEIAKEY1");
            else
                StringAssert.StartsWith(LocalKeyPolicy.DevFallbackLabel, Encoding.UTF8.GetString(keyBytes));
        }

        [Test]
        public void Migration_RewritesPlaintextOnlyAfterTheCopyDecrypts()
        {
            var root = NewRoot();
            var path = Path.Combine(root, "memory.json");
            File.WriteAllText(path, MemoryJson("ZephyrUnique fact"), new UTF8Encoding(false));
            var store = MemoryStore.Load(path);
            Assert.AreEqual(1, store.Count);
            Assert.AreEqual("ZephyrUnique fact", store.Entries[0].text);
            Assert.IsTrue(store.Entries[0].isCore);

            var raw = File.ReadAllBytes(path);
            Assert.IsTrue(LocalAesGcm.IsMagic(raw));
            Assert.IsFalse(Encoding.UTF8.GetString(raw).Contains("ZephyrUnique"));
            Assert.AreEqual("ZephyrUnique fact", MemoryStore.Load(path).Entries[0].text);
        }

        [Test]
        public void SettingsPersonaAndHistory_RoundTripWithoutLeavingTheTextOnDisk()
        {
            var root = NewRoot();
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = CompanionSettings.CreateDefault();
            settings.CompanionName = "ZephyrUnique";
            settings.PrivateMode = true;
            settings.ScreenLookEnabled = true;
            settings.ScreenCloudConsent = true;
            CompanionSettingsStore.Save(settingsPath, settings);
            Assert.IsFalse(Encoding.UTF8.GetString(File.ReadAllBytes(settingsPath)).Contains("ZephyrUnique"));
            var loaded = CompanionSettingsStore.Load(settingsPath, SettingsLegacyPrefs.None);
            Assert.AreEqual("ZephyrUnique", loaded.CompanionName);
            Assert.IsTrue(loaded.PrivateMode);
            Assert.IsTrue(loaded.ScreenLookEnabled);
            Assert.IsTrue(loaded.ScreenCloudConsent);

            var persona = new PersonaDefinition
            {
                Id = "zephyr",
                DisplayName = "Nia",
                Personality = "ZephyrUnique persona. Stay appropriate for any audience.",
                SpeakingStyle = "Brief."
            };
            Assert.IsTrue(PersonaStore.Save(SettingsLocations.PersonasFolder(root), persona));
            var personaPath = Path.Combine(root, "personas", "zephyr.json");
            Assert.IsTrue(LocalAesGcm.IsMagic(File.ReadAllBytes(personaPath)));
            Assert.IsFalse(Encoding.UTF8.GetString(File.ReadAllBytes(personaPath)).Contains("ZephyrUnique"));
            var found = PersonaCatalog.Load(new string[0], SettingsLocations.PersonasFolder(root)).Find("zephyr");
            Assert.IsNotNull(found);
            StringAssert.Contains("ZephyrUnique", found.Personality);

            var history = Path.Combine(root, "chat-history-zephyr.json");
            Assert.IsTrue(ProtectedLocalFile.Write(history, "{\"version\":1,\"lines\":[{\"role\":\"user\",\"text\":\"ZephyrUnique chat\"}]}"));
            Assert.IsFalse(Encoding.UTF8.GetString(File.ReadAllBytes(history)).Contains("ZephyrUnique"));
            Assert.IsTrue(ProtectedLocalFile.Read(history).Text.Contains("ZephyrUnique chat"));
        }

        [Test]
        public void Tamper_MovesTheFileAsideAndTheNextWriteStartsFresh()
        {
            var root = NewRoot();
            var path = Path.Combine(root, "memory.json");
            Assert.IsTrue(ProtectedLocalFile.Write(path, "ZephyrUnique"));
            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            LogAssert.Expect(LogType.Warning, "Unreadable local file was kept aside: memory.json");
            var read = ProtectedLocalFile.Read(path);
            Assert.AreEqual(LocalReadStatus.Unreadable, read.Status);
            Assert.IsFalse(File.Exists(path));
            var aside = Aside(root, "memory.json.unreadable-");
            Assert.IsNotNull(aside);
            Assert.IsTrue(LocalAesGcm.IsMagic(File.ReadAllBytes(aside)));

            Assert.IsTrue(ProtectedLocalFile.Write(path, "fresh line"));
            Assert.AreEqual("fresh line", ProtectedLocalFile.Read(path).Text);
        }

        [Test]
        public void SwappedFile_DoesNotDecryptUnderTheOtherName()
        {
            var root = NewRoot();
            var memory = Path.Combine(root, "memory.json");
            var history = Path.Combine(root, "chat-history.json");
            Assert.IsTrue(ProtectedLocalFile.Write(memory, "one-sentinel"));
            Assert.IsTrue(ProtectedLocalFile.Write(history, "two-sentinel"));
            File.Copy(memory, history, true);
            LogAssert.Expect(LogType.Warning, "Unreadable local file was kept aside: chat-history.json");
            Assert.AreEqual(LocalReadStatus.Unreadable, ProtectedLocalFile.Read(history).Status);
            Assert.AreEqual("one-sentinel", ProtectedLocalFile.Read(memory).Text);
        }

        [Test]
        public void MissingKey_LeavesCiphertextInPlaceUntilTheKeyReturns()
        {
            var root = NewRoot();
            var path = Path.Combine(root, "personality.json");
            var history = Path.Combine(root, "chat-history.json");
            Assert.IsTrue(ProtectedLocalFile.Write(path, "{\"version\":1,\"personaName\":\"ZephyrUnique\",\"mood\":\"bright\",\"warmth\":0.5,\"updatedUtc\":\"\"}"));
            Assert.IsTrue(ProtectedLocalFile.Write(history, "ZephyrUnique chat"));
            var personality = File.ReadAllBytes(path);
            var chat = File.ReadAllBytes(history);
            var keyPath = Path.Combine(root, InstallKeyFile.FileName);
            var keyCopy = File.ReadAllBytes(keyPath);
            InstallKeyFile.Forget(root);
            File.Delete(keyPath);

            LogAssert.Expect(LogType.Warning, "Install key is missing. Encrypted files were left in place.");
            LogAssert.Expect(LogType.Warning, "Local file was not decrypted because the install key is unavailable: personality.json");
            Assert.AreEqual(LocalReadStatus.Unreadable, ProtectedLocalFile.Read(path).Status);
            LogAssert.Expect(LogType.Warning, "Local file was not decrypted because the install key is unavailable: chat-history.json");
            Assert.AreEqual(LocalReadStatus.Unreadable, ProtectedLocalFile.Read(history).Status);
            Assert.IsFalse(File.Exists(keyPath));
            Assert.IsNull(Aside(root, "personality.json.unreadable-"));
            Assert.IsNull(Aside(root, "chat-history.json.unreadable-"));
            CollectionAssert.AreEqual(personality, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(chat, File.ReadAllBytes(history));

            File.WriteAllBytes(keyPath, keyCopy);
            InstallKeyFile.Forget(root);
            Assert.AreEqual("{\"version\":1,\"personaName\":\"ZephyrUnique\",\"mood\":\"bright\",\"warmth\":0.5,\"updatedUtc\":\"\"}", ProtectedLocalFile.Read(path).Text);
            Assert.AreEqual("ZephyrUnique chat", ProtectedLocalFile.Read(history).Text);
        }

        [Test]
        public void OmitSecret_RemovesTheCopiedKeyFile()
        {
            var root = NewRoot();
            var copied = Path.Combine(root, "secrets.json");
            File.WriteAllText(copied, "{\"protected\":true,\"xaiApiKey\":\"blob\"}");
            File.WriteAllText(copied + ".writing", "partial");
            var result = new LocalActionResult { Ok = true, Files = 2 };
            LocalDataPortal.OmitSecret(copied, result);
            Assert.IsFalse(File.Exists(copied));
            Assert.IsFalse(File.Exists(copied + ".writing"));
            Assert.AreEqual(1, result.Files);
            CollectionAssert.Contains(result.Notes, "secrets.json was not included");
        }

        [Test]
        public void Export_WritesDecryptedFilesAndAZipWithoutTheInstallKey()
        {
            var root = NewRoot();
            var memory = Path.Combine(root, "memory.json");
            File.WriteAllText(memory, MemoryJson("ZephyrUnique fact"), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "notes.txt"), "leave this");
            Directory.CreateDirectory(Path.Combine(root, "portraits"));
            File.WriteAllBytes(Path.Combine(root, "portraits", "face.png"), new byte[] { 1, 2, 3, 4 });
            ApiKeyFile.Save(Path.Combine(root, "secrets.json"), "test-export-key", new PlainSecretProtector());

            var parent = Path.GetDirectoryName(root);
            var dest = Path.Combine(parent, "export");
            var exported = LocalDataPortal.ExportFolder(root, dest);
            Assert.IsTrue(exported.Ok);
            Assert.IsTrue(File.ReadAllText(Path.Combine(dest, "memory.json")).Contains("ZephyrUnique fact"));
            Assert.IsFalse(File.Exists(Path.Combine(dest, InstallKeyFile.FileName)));
            Assert.IsFalse(File.Exists(Path.Combine(dest, "notes.txt")));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(Path.Combine(dest, "portraits", "face.png")));
            var secrets = File.ReadAllText(Path.Combine(dest, "secrets.json"));
            StringAssert.Contains("test-export-key", secrets);
            StringAssert.Contains("\"protected\":false", secrets);
            Assert.IsTrue(LocalAesGcm.IsMagic(File.ReadAllBytes(memory)));

            var zipPath = Path.Combine(parent, "ProjectLeia-export.zip");
            var zipped = LocalDataPortal.ExportZip(root, zipPath);
            Assert.IsTrue(zipped.Ok);
            var zip = File.ReadAllBytes(zipPath);
            Assert.AreEqual(0x50, zip[0]);
            Assert.AreEqual(0x4B, zip[1]);
            var listing = Encoding.UTF8.GetString(zip);
            StringAssert.Contains("memory.json", listing);
            StringAssert.Contains("ZephyrUnique fact", listing);
            Assert.IsFalse(listing.Contains(InstallKeyFile.FileName));

            var inside = LocalDataActions.ExportFolder(root, Path.Combine(root, "inside"));
            Assert.IsFalse(inside.Ok);
        }

        [Test]
        public void Wipe_RemovesPrivateFilesAndLeavesEverythingOutside()
        {
            var root = NewRoot();
            var parent = Path.GetDirectoryName(root);
            File.WriteAllText(Path.Combine(parent, "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "not app data");
            CompanionSettingsStore.Save(Path.Combine(root, "settings.json"), CompanionSettings.CreateDefault());
            Assert.IsTrue(ProtectedLocalFile.Write(Path.Combine(root, "memory-lola.json"), "lola"));
            Assert.IsTrue(ProtectedLocalFile.Write(Path.Combine(root, "chat-history.json"), "chat"));
            Assert.IsTrue(ProtectedLocalFile.Write(Path.Combine(root, "personality.json"), "mood"));
            Directory.CreateDirectory(Path.Combine(root, "portraits"));
            File.WriteAllBytes(Path.Combine(root, "portraits", "face.png"), new byte[] { 9 });
            ApiKeyFile.Save(Path.Combine(root, "secrets.json"), "test-export-key", new PlainSecretProtector());
            Assert.IsTrue(PersonaStore.Save(SettingsLocations.PersonasFolder(root), new PersonaDefinition
            {
                Id = "zephyr",
                DisplayName = "Nia",
                Personality = "A short line appropriate for any audience.",
                SpeakingStyle = "Brief."
            }));

            var refused = LocalDataActions.Wipe(parent);
            Assert.IsFalse(refused.Ok);
            Assert.IsTrue(File.Exists(Path.Combine(root, "settings.json")));

            var wiped = LocalDataActions.Wipe(root);
            Assert.IsTrue(wiped.Ok);
            Assert.IsFalse(File.Exists(Path.Combine(root, "settings.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "memory-lola.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "chat-history.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "personality.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "secrets.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, InstallKeyFile.FileName)));
            Assert.IsFalse(File.Exists(Path.Combine(root, "personas", "zephyr.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "portraits", "face.png")));
            Assert.IsTrue(File.Exists(Path.Combine(root, "notes.txt")));
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(parent, "keep.txt")));

            Assert.IsTrue(ProtectedLocalFile.Write(Path.Combine(root, "memory.json"), "after wipe"));
            Assert.AreEqual("after wipe", ProtectedLocalFile.Read(Path.Combine(root, "memory.json")).Text);
        }

        [Test]
        public void Inventory_MatchesThePrivateFilesAndSkipsScreenCaptureBytes()
        {
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("settings.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("personality.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("memory.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("memory-lola.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("chat-history.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("chat-history-friend.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("personas/friend.json"));
            Assert.IsTrue(LocalDataInventory.IsSecretFile("secrets.json"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("secrets.json"));
            Assert.IsTrue(LocalDataInventory.IsRawCopy("portraits/neutral.png"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("portraits/portraits.json"));
            Assert.IsTrue(LocalDataInventory.IsExcluded("install-key.leia"));
            Assert.IsTrue(LocalDataInventory.IsExcluded("memory.json.unreadable-1"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("notes.txt"));

            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "AI", "ScreenObservationMemory.cs"));
            StringAssert.Contains("never written to disk", source);
            Assert.IsFalse(source.Contains("File.Write"));
            Assert.IsFalse(source.Contains("PlayerPrefs"));
        }

        [Test]
        public void Paths_TreatPersonasAsPartOfTheProjectLeiaRoot()
        {
            var root = NewRoot();
            var persona = Path.Combine(root, "personas", "friend.json");
            Assert.AreEqual(Path.GetFullPath(root), Path.GetFullPath(InstallKeyFile.RootFor(persona)));
            Assert.AreEqual(Path.GetFullPath(root), Path.GetFullPath(InstallKeyFile.RootFor(Path.Combine(root, "memory.json"))));
            Assert.IsTrue(LocalDataActions.IsDataRoot(root));
            Assert.IsFalse(LocalDataActions.IsDataRoot(Path.Combine(root, "personas")));
        }

        static void AssertRoundTrip(byte[] key, byte[] nonce, byte[] aad, byte[] plaintext, byte[] cipher, byte[] tag)
        {
            var envelope = LocalAesGcm.Seal(key, nonce, aad, plaintext);
            Assert.AreEqual(1, envelope[8]);
            CollectionAssert.AreEqual(nonce, Slice(envelope, 9, 12));
            CollectionAssert.AreEqual(tag, Slice(envelope, 21, 16));
            CollectionAssert.AreEqual(cipher, Slice(envelope, LocalAesGcm.HeaderBytes, cipher.Length));
            byte[] back;
            Assert.IsTrue(LocalAesGcm.TryOpen(key, aad, envelope, out back));
            CollectionAssert.AreEqual(plaintext, back);
        }

        static string MemoryJson(string text)
        {
            return "{\"version\":1,\"entries\":[{\"id\":\"a\",\"text\":\"" + text + "\",\"kind\":0,\"createdAtUtc\":\"2026-01-01T00:00:00Z\",\"isPrivate\":false,\"isCore\":true}]}";
        }

        string NewRoot()
        {
            var parent = Path.Combine(Path.GetTempPath(), "leia-vault-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(parent, "ProjectLeia");
            Directory.CreateDirectory(root);
            _cleanup.Add(parent);
            return root;
        }

        string NewFolder(string name)
        {
            var folder = Path.Combine(Path.GetTempPath(), "leia-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _cleanup.Add(folder);
            return folder;
        }

        static string Aside(string root, string prefix)
        {
            var files = Directory.GetFiles(root);
            for (var i = 0; i < files.Length; i++)
            {
                if (Path.GetFileName(files[i]).StartsWith(prefix, StringComparison.Ordinal))
                    return files[i];
            }

            return null;
        }

        static byte[] Wrap(byte[] raw)
        {
            var blob = new byte[raw.Length + 1];
            blob[0] = 0xAB;
            Buffer.BlockCopy(raw, 0, blob, 1, raw.Length);
            return blob;
        }

        static byte[] Unwrap(byte[] blob)
        {
            var raw = new byte[blob.Length - 1];
            Buffer.BlockCopy(blob, 1, raw, 0, raw.Length);
            return raw;
        }

        static byte[] Hex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        static byte[] Slice(byte[] bytes, int offset, int count)
        {
            var slice = new byte[count];
            if (count > 0)
                Buffer.BlockCopy(bytes, offset, slice, 0, count);
            return slice;
        }

        sealed class ThrowingProtector : ISecretProtector
        {
            public bool Protects
            {
                get { return true; }
            }

            public string Protect(string plain)
            {
                throw new InvalidOperationException("dpapi");
            }

            public bool TryUnprotect(string payload, out string plain)
            {
                plain = null;
                return false;
            }
        }
    }
}
