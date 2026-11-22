using AICompanion.Settings;
using AICompanion.Voice;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-168: fewer cores for Kokoro while a game is in front.</summary>
    public sealed class KokoroCpuLimitTests
    {
        [Test]
        public void RequestThreads_OnlyWhenLimited()
        {
            Assert.AreEqual(2, KokoroCpuLimit.RequestThreads(true, 2));
            Assert.AreEqual(1, KokoroCpuLimit.RequestThreads(true, 0), "at least one");
            Assert.AreEqual(KokoroCpuLimit.MaxCores, KokoroCpuLimit.RequestThreads(true, 99));
            Assert.AreEqual(0, KokoroCpuLimit.RequestThreads(false, 2), "every core");
        }

        [Test]
        public void Request_SendsThreadsOnlyWhenLimited_AndWarmHasNoText()
        {
            var plain = new KokoroRequest { text = "Hi", speed = 1f, voice = "af_heart" }.ToJson();
            Assert.AreEqual("{\"text\":\"Hi\",\"speed\":1,\"voice\":\"af_heart\"}", plain, "unchanged for an older script");

            var limited = new KokoroRequest { text = "Hi", speed = 1f, voice = "af_heart", threads = 2 }.ToJson();
            StringAssert.EndsWith(",\"threads\":2}", limited);

            var warm = new KokoroRequest { warm = true, threads = 2, voice = "af_heart", speed = 1f }.ToJson();
            StringAssert.StartsWith("{\"warm\":true,\"text\":\"\"", warm);
            StringAssert.Contains("\"threads\":2", warm);
        }

        [Test]
        public void Reply_ReadsThreads_AndAnOlderScriptIsNoticed()
        {
            var reply = KokoroReply.Parse("{\"ok\": true, \"threads\": 2, \"wav\": \"UklGRg==\"}");
            Assert.IsTrue(reply.ok);
            Assert.AreEqual(2, reply.threads);
            Assert.AreEqual("UklGRg==", reply.wav);
            Assert.AreEqual(0, KokoroReply.Parse("{\"ok\": true, \"threads\": 0, \"wav\": \"\"}").threads);

            var old = KokoroReply.Parse("{\"ok\": true, \"wav\": \"UklGRg==\"}");
            Assert.AreEqual(-1, old.threads);
            Assert.IsTrue(KokoroCpuLimit.OldScript(2, old.threads), "asked for 2, the reply didn't say");
            Assert.IsFalse(KokoroCpuLimit.OldScript(0, old.threads), "not limited: nothing to notice");
            Assert.IsFalse(KokoroCpuLimit.OldScript(2, 2));
        }

        [Test]
        public void Want_OnlyWithTheSettingOnAndAGameInFront()
        {
            Assert.IsTrue(KokoroCpuLimit.Want(true, true));
            Assert.IsFalse(KokoroCpuLimit.Want(true, false));
            Assert.IsFalse(KokoroCpuLimit.Want(false, true));
        }

        [Test]
        public void Clean_KeepsOneToSixteen_AndTheLogLineSaysWhat()
        {
            Assert.AreEqual(1, KokoroCpuLimit.Clean(0));
            Assert.AreEqual(1, KokoroCpuLimit.Clean(-3));
            Assert.AreEqual(4, KokoroCpuLimit.Clean(4));
            Assert.AreEqual(KokoroCpuLimit.MaxCores, KokoroCpuLimit.Clean(99));
            StringAssert.Contains("2 threads", KokoroCpuLimit.LogLine(true, 2));
            StringAssert.Contains("1 thread ", KokoroCpuLimit.LogLine(true, 1));
            StringAssert.Contains("every core", KokoroCpuLimit.LogLine(false, 2));
        }

        [Test]
        public void Settings_DefaultOnWithTwoCores_AndRoundTrip()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsTrue(fresh.KokoroGameLimit);
            Assert.AreEqual(KokoroCpuLimit.DefaultCores, fresh.KokoroGameCores);

            // Older settings files have neither key: they load with the defaults.
            var old = CompanionSettingsJson.Parse("{\"version\":1,\"kokoroVoice\":\"af_bella\"}", SettingsLegacyPrefs.None);
            Assert.IsTrue(old.KokoroGameLimit);
            Assert.AreEqual(KokoroCpuLimit.DefaultCores, old.KokoroGameCores);

            fresh.KokoroGameLimit = false;
            fresh.KokoroGameCores = 4;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None);
            Assert.IsFalse(back.KokoroGameLimit);
            Assert.AreEqual(4, back.KokoroGameCores);

            var wild = CompanionSettingsJson.Parse("{\"version\":1,\"kokoroGameCores\":500}", SettingsLegacyPrefs.None);
            Assert.AreEqual(KokoroCpuLimit.MaxCores, wild.KokoroGameCores, "cleaned on load");
        }
    }
}
