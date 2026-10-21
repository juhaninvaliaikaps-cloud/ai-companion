using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class VoiceSpeechTests
    {
        [Test]
        public void Clean_StripsMarkdownActionsAndEmoji()
        {
            Assert.AreEqual("Hello there friend", SpeechText.Clean("**Hello** *waves* there \U0001F600\u2605 friend"));
            Assert.AreEqual("See docs please", SpeechText.Clean("See [docs](https://example.com) please"));
            Assert.AreEqual("Title quote", SpeechText.Clean("# Title\n> quote"));
            Assert.AreEqual("code sample", SpeechText.Clean("`code` sample"));
            Assert.AreEqual(string.Empty, SpeechText.Clean("*smiles*"));
            Assert.IsFalse(SpeechText.HasSpokenCharacter("..."));
        }

        [Test]
        public void Sentences_SplitOnPunctuationButNotDecimals()
        {
            var buffer = new SentenceBuffer();
            var emitted = new List<string>();

            buffer.Append("Hel", emitted);
            buffer.Append("lo. It is 3.", emitted);
            Assert.AreEqual(1, emitted.Count);
            Assert.AreEqual("Hello.", emitted[0]);

            buffer.Append("14 now! Yes", emitted);
            Assert.AreEqual(2, emitted.Count);
            Assert.AreEqual("It is 3.14 now!", emitted[1]);

            buffer.Flush(emitted);
            Assert.AreEqual(3, emitted.Count);
            Assert.AreEqual("Yes", emitted[2]);
        }

        [Test]
        public void Sentences_WaitWhenANumberMightStillBeGrowing()
        {
            var buffer = new SentenceBuffer();
            var emitted = new List<string>();

            buffer.Append("Wait 3.", emitted);
            Assert.AreEqual(0, emitted.Count);

            buffer.Flush(emitted);
            Assert.AreEqual(1, emitted.Count);
            Assert.AreEqual("Wait 3.", emitted[0]);
        }

        [Test]
        public void Sentences_BreakALongRunOnTheLastSpace()
        {
            var buffer = new SentenceBuffer();
            var emitted = new List<string>();
            var text = new StringBuilder();
            while (text.Length < SentenceBuffer.ForceLength + 20)
                text.Append("word ");

            buffer.Append(text.ToString(), emitted);

            Assert.IsTrue(emitted.Count > 0);
            Assert.IsTrue(emitted[0].Length <= SentenceBuffer.ForceLength);
            StringAssert.DoesNotContain("wordword", emitted[0]);
        }

        [Test]
        public void Turn_SpeaksCleanSentencesInOrderAndDropsThemOnCancel()
        {
            var turn = new SpeechTurn();
            turn.Begin(true);
            var generation = turn.Generation;
            turn.Append("One. **Two.** *smiles*");
            turn.Complete();

            Assert.IsTrue(turn.TryTake(out var firstGeneration, out var first));
            Assert.AreEqual(generation, firstGeneration);
            Assert.AreEqual("One.", first);
            Assert.IsTrue(turn.TryTake(out _, out var second));
            Assert.AreEqual("Two.", second);
            Assert.IsFalse(turn.TryTake(out _, out _));

            turn.Begin(true);
            var live = turn.Generation;
            turn.Append("Keep. Drop.");
            Assert.IsTrue(turn.TryTake(out _, out var kept));
            Assert.AreEqual("Keep.", kept);
            turn.Cancel();

            Assert.IsFalse(turn.IsCurrent(live));
            Assert.IsFalse(turn.TryTake(out _, out _));
            turn.Append("More.");
            turn.Complete();
            Assert.IsFalse(turn.TryTake(out _, out _));
        }

        [Test]
        public void Turn_StaysQuietWhenSpeakingIsOff()
        {
            var turn = new SpeechTurn();
            turn.Begin(false);
            turn.Append("Hello.");
            turn.Complete();

            Assert.IsFalse(turn.TryTake(out _, out _));
        }

        [Test]
        public void Playback_KeepsTalkingAcrossClipsThenStops()
        {
            var playback = new SpeechPlayback();
            playback.EnqueueClip();
            playback.EnqueueClip();
            Assert.IsFalse(playback.ShouldTalk);

            playback.Pump();
            Assert.IsTrue(playback.ShouldTalk);
            Assert.AreEqual(1, playback.QueuedCount);

            playback.ClipFinished();
            Assert.IsTrue(playback.ShouldTalk);
            Assert.AreEqual(0, playback.QueuedCount);

            playback.ClipFinished();
            Assert.IsFalse(playback.ShouldTalk);

            playback.EnqueueClip();
            playback.Pump();
            playback.Stop();
            Assert.IsFalse(playback.ShouldTalk);
            Assert.AreEqual(0, playback.QueuedCount);
        }

        [Test]
        public void Policy_SkipsCloudTtsForTheLocalProviderUnlessAllowed()
        {
            var chat = ScriptableObject.CreateInstance<ChatSettings>();
            var voice = ScriptableObject.CreateInstance<VoiceSettings>();
            try
            {
                chat.providerId = "local";
                voice.enabled = true;
                Assert.IsFalse(voice.allowCloudTtsForLocalReplies);
                Assert.IsFalse(SpeechPolicy.ShouldSpeak(chat, voice, false));

                voice.allowCloudTtsForLocalReplies = true;
                Assert.IsTrue(SpeechPolicy.ShouldSpeak(chat, voice, false));

                chat.providerId = "LOCAL";
                voice.allowCloudTtsForLocalReplies = false;
                Assert.IsFalse(SpeechPolicy.ShouldSpeak(chat, voice, false));

                chat.providerId = ChatDefaults.GrokId;
                Assert.IsTrue(SpeechPolicy.ShouldSpeak(chat, voice, false));

                voice.enabled = false;
                Assert.IsFalse(SpeechPolicy.ShouldSpeak(chat, voice, false));
                voice.enabled = true;
                Assert.IsFalse(SpeechPolicy.ShouldSpeak(chat, voice, true));
                Assert.IsFalse(SpeechPolicy.ShouldSpeak(chat, null, false));
            }
            finally
            {
                Object.DestroyImmediate(chat);
                Object.DestroyImmediate(voice);
            }
        }

        [Test]
        public void RequestJson_AsksForWavAndLeavesTheKeyOut()
        {
            var json = TtsRequestJson.Write(new TtsRequest
            {
                Text = "say \"hi\"",
                VoiceId = "eve",
                Language = "en",
                Speed = 3f,
                SampleRate = 999
            });

            StringAssert.Contains("\"voice_id\":\"eve\"", json);
            StringAssert.Contains("\"language\":\"en\"", json);
            StringAssert.Contains("\"codec\":\"wav\"", json);
            StringAssert.Contains("\"sample_rate\":24000", json);
            StringAssert.Contains("\"speed\":1.5", json);
            StringAssert.Contains("\\\"hi\\\"", json);
            StringAssert.DoesNotContain("xai-", json);
            StringAssert.DoesNotContain("Bearer", json);
            StringAssert.DoesNotContain("api_key", json);
        }

        [Test]
        public void Wav_Decodes16BitMonoPcm()
        {
            var wav = Wav16(24000, 0, 16000);
            Assert.IsTrue(WavPcm.TryDecode(wav, 24000, out var samples, out var channels, out var rate));
            Assert.AreEqual(1, channels);
            Assert.AreEqual(24000, rate);
            Assert.AreEqual(2, samples.Length);
            Assert.AreEqual(0f, samples[0], 0.0001f);
            Assert.AreEqual(16000f / 32768f, samples[1], 0.0001f);
            Assert.IsFalse(WavPcm.TryDecode(Encoding.UTF8.GetBytes("{\"error\":\"nope\"}"), 24000, out _, out _, out _));
        }

        [Test]
        public void Errors_ScrubKeysAndStopTheQueueForAuthFailures()
        {
            var body = Encoding.UTF8.GetBytes("{\"error\":\"bad Bearer xai-abcdefghijklmnopqrstuvwxyz key\"}");
            var denied = TtsErrors.FromHttp(TtsHttpResult.Http(401, body));

            Assert.AreEqual(ChatErrors.InvalidApiKey, denied.UserMessage);
            Assert.IsTrue(denied.StopQueue);
            StringAssert.Contains("[redacted]", denied.LogMessage);
            StringAssert.DoesNotContain("xai-abc", denied.LogMessage);
            StringAssert.DoesNotContain("Bearer xai", denied.LogMessage);

            var down = TtsErrors.FromHttp(TtsHttpResult.Http(503, Encoding.UTF8.GetBytes("busy")));
            Assert.AreEqual(ChatErrors.Unreachable("the voice"), down.UserMessage);
            Assert.IsFalse(down.StopQueue);

            var missing = TtsErrors.MissingKey();
            Assert.AreEqual(ChatErrors.MissingApiKey, missing.UserMessage);
            Assert.IsTrue(missing.StopQueue);
        }

        [Test]
        public void Provider_PostsTheSentenceWithoutPuttingTheKeyInTheBody()
        {
            var keys = new MemoryKeys { Key = "xai-testkey1234" };
            var transport = new MemoryTransport { Next = TtsHttpResult.Ok(200, Wav16(24000, 0)) };
            var provider = new XaiTtsProvider(keys, transport, "https://api.x.ai/v1/tts", 20f);

            var result = provider.Synthesize(new TtsRequest
            {
                Text = "Hello.",
                VoiceId = "eve",
                Language = "en",
                Speed = 1f,
                SampleRate = 24000
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(1, transport.Calls);
            StringAssert.Contains("https://api.x.ai/v1/tts", transport.Url);
            StringAssert.Contains("Hello.", transport.Body);
            StringAssert.DoesNotContain(keys.Key, transport.Body);
            Assert.AreEqual(keys.Key, transport.Bearer);

            var blocked = new XaiTtsProvider(new MemoryKeys(), transport, 20f);
            var missing = blocked.Synthesize(new TtsRequest { Text = "Hello." }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(missing.Ok);
            Assert.AreEqual(ChatErrors.MissingApiKey, missing.UserMessage);
            Assert.IsTrue(missing.StopQueue);
            Assert.AreEqual(1, transport.Calls);
        }

        static byte[] Wav16(int sampleRate, params short[] samples)
        {
            var dataBytes = samples.Length * 2;
            var bytes = new byte[44 + dataBytes];
            WriteAscii(bytes, 0, "RIFF");
            WriteInt(bytes, 4, 36 + dataBytes);
            WriteAscii(bytes, 8, "WAVE");
            WriteAscii(bytes, 12, "fmt ");
            WriteInt(bytes, 16, 16);
            WriteShort(bytes, 20, 1);
            WriteShort(bytes, 22, 1);
            WriteInt(bytes, 24, sampleRate);
            WriteInt(bytes, 28, sampleRate * 2);
            WriteShort(bytes, 32, 2);
            WriteShort(bytes, 34, 16);
            WriteAscii(bytes, 36, "data");
            WriteInt(bytes, 40, dataBytes);
            for (var i = 0; i < samples.Length; i++)
                WriteShort(bytes, 44 + i * 2, samples[i]);
            return bytes;
        }

        static void WriteAscii(byte[] bytes, int offset, string text)
        {
            for (var i = 0; i < text.Length; i++)
                bytes[offset + i] = (byte)text[i];
        }

        static void WriteShort(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        sealed class MemoryKeys : IApiKeySource
        {
            public string Key;

            public bool TryGet(out string key)
            {
                key = Key;
                return !string.IsNullOrEmpty(Key);
            }
        }

        sealed class MemoryTransport : ITtsTransport
        {
            public int Calls;
            public string Url;
            public string Body;
            public string Bearer;
            public TtsHttpResult Next;

            public Task<TtsHttpResult> PostAsync(string url, string json, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken)
            {
                Calls++;
                Url = url;
                Body = json;
                Bearer = bearerToken;
                return Task.FromResult(Next);
            }
        }
    }
}
