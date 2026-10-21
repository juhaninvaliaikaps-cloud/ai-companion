using System;
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
        public void RequestJson_AsksForPcmAndLeavesTheKeyOut()
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
            StringAssert.Contains("\"codec\":\"pcm\"", json);
            StringAssert.Contains("\"sample_rate\":24000", json);
            StringAssert.Contains("\"speed\":1.5", json);
            StringAssert.Contains("\\\"hi\\\"", json);
            StringAssert.DoesNotContain("xai-", json);
            StringAssert.DoesNotContain("Bearer", json);
            StringAssert.DoesNotContain("api_key", json);
            StringAssert.DoesNotContain("\"codec\":\"wav\"", json);
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
            Assert.AreEqual("52 49 46 46 RIFF", SpeechAudio.DescribeHead(wav));
            Assert.AreEqual("empty", SpeechAudio.DescribeHead(null));
        }

        [Test]
        public void Wav_RejectsShortGarbageAndOversizedChunksWithoutThrowing()
        {
            Assert.IsFalse(WavPcm.TryDecode(null, 24000, out _, out _, out _));
            Assert.IsFalse(WavPcm.TryDecode(new byte[0], 24000, out _, out _, out _));
            Assert.IsFalse(WavPcm.TryDecode(Encoding.UTF8.GetBytes("garbage bytes"), 24000, out _, out _, out _));

            var whole = Wav16(24000, 0, 16000);
            var truncated = new byte[30];
            Buffer.BlockCopy(whole, 0, truncated, 0, truncated.Length);
            Assert.DoesNotThrow(() => WavPcm.TryDecode(truncated, 24000, out _, out _, out _));
            Assert.IsFalse(WavPcm.TryDecode(truncated, 24000, out _, out _, out _));

            var overflow = (byte[])whole.Clone();
            WriteUInt(overflow, 40, 0x7FFFFFFFu);
            float[] overflowSamples = null;
            Assert.DoesNotThrow(() => WavPcm.TryDecode(overflow, 24000, out overflowSamples, out _, out _));
            Assert.IsNotNull(overflowSamples);
            Assert.AreEqual(2, overflowSamples.Length);
            Assert.AreEqual(16000f / 32768f, overflowSamples[1], 0.0001f);

            var blocked = new byte[20];
            WriteAscii(blocked, 0, "RIFF");
            WriteUInt(blocked, 4, 0xFFFFFFFFu);
            WriteAscii(blocked, 8, "WAVE");
            WriteAscii(blocked, 12, "fact");
            WriteUInt(blocked, 16, 0x7FFFFFFFu);
            Assert.DoesNotThrow(() => WavPcm.TryDecode(blocked, 24000, out _, out _, out _));
            Assert.IsFalse(WavPcm.TryDecode(blocked, 24000, out _, out _, out _));
        }

        [Test]
        public void Wav_ReadsStreamingSizesAndSkipsOddListChunks()
        {
            AssertDecodes(StreamingWav(0u, 0, 16000));
            AssertDecodes(StreamingWav(0xFFFFFFFFu, 0, 16000));
            AssertDecodes(WavWithOddList(0, 16000));
        }

        [Test]
        public void SpeechAudio_DecodesJsonEnvelopeAndRawPcm()
        {
            var wav = Wav16(24000, 0, 16000);
            var envelope = "{\"audio\":\"" + Convert.ToBase64String(wav) + "\",\"content_type\":\"audio/wav\",\"duration\":0.1}";
            Assert.IsTrue(SpeechAudio.TryDecode(Encoding.UTF8.GetBytes(envelope), "application/json", 24000, true, out var fromJson, out var jsonChannels, out var jsonRate));
            Assert.AreEqual(1, jsonChannels);
            Assert.AreEqual(24000, jsonRate);
            Assert.AreEqual(16000f / 32768f, fromJson[1], 0.0001f);

            var pcm = Pcm16(0, 16000);
            var pcmEnvelope = "{\"content_type\":\"audio/L16; rate=24000\",\"audio\":\"" + Convert.ToBase64String(pcm) + "\"}";
            Assert.IsTrue(SpeechAudio.TryDecode(Encoding.UTF8.GetBytes(pcmEnvelope), "application/json", 24000, true, out var fromPcmJson, out var pcmJsonChannels, out var pcmJsonRate));
            Assert.AreEqual(1, pcmJsonChannels);
            Assert.AreEqual(24000, pcmJsonRate);
            Assert.AreEqual(0f, fromPcmJson[0], 0.0001f);

            Assert.IsTrue(SpeechAudio.TryDecode(pcm, "audio/pcm", 24000, true, out var raw, out var rawChannels, out var rawRate));
            Assert.AreEqual(1, rawChannels);
            Assert.AreEqual(24000, rawRate);
            Assert.AreEqual(2, raw.Length);
            Assert.IsTrue(SpeechAudio.TryDecode(pcm, string.Empty, 24000, true, out _, out _, out _));
            Assert.IsTrue(SpeechAudio.TryDecode(pcm, "application/octet-stream", 16000, true, out _, out _, out var octetRate));
            Assert.AreEqual(16000, octetRate);

            Assert.IsFalse(SpeechAudio.TryDecode(new byte[0], "audio/pcm", 24000, true, out _, out _, out _));
            Assert.IsFalse(SpeechAudio.TryDecode(Encoding.UTF8.GetBytes("{\"error\":\"nope\"}"), "application/json", 24000, true, out _, out _, out _));
            Assert.IsFalse(SpeechAudio.TryDecode(Encoding.UTF8.GetBytes("garbage!!"), "text/plain", 24000, true, out _, out _, out _));
            Assert.IsFalse(SpeechAudio.TryDecode(Encoding.UTF8.GetBytes("not-a-wav-body"), "audio/wav", 24000, true, out _, out _, out _));
            Assert.IsFalse(SpeechAudio.TryDecode(new byte[] { 1, 2, 3 }, "audio/pcm", 24000, true, out _, out _, out _));
            Assert.IsFalse(SpeechAudio.TryDecode(pcm, "audio/mpeg", 24000, true, out _, out _, out _));
            Assert.IsFalse(SpeechAudio.TryDecode(TruncatedHeader(wav), string.Empty, 24000, true, out _, out _, out _));

            var http = TtsErrors.FromHttp(TtsHttpResult.Ok(200, wav, "audio/wav"));
            Assert.AreEqual(200, http.StatusCode);
            Assert.AreEqual("audio/wav", http.ContentType);
            Assert.AreEqual(wav.Length, http.ByteCount);
            StringAssert.Contains("RIFF", http.Head);
            StringAssert.DoesNotContain("xai-", http.Head);
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
            Assert.AreEqual(401, denied.StatusCode);
            StringAssert.Contains("{\"er", denied.Head);
            Assert.LessOrEqual(denied.Head.Length, 24);

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

        static void AssertDecodes(byte[] wav)
        {
            Assert.IsTrue(WavPcm.TryDecode(wav, 24000, out var samples, out var channels, out var rate));
            Assert.AreEqual(1, channels);
            Assert.AreEqual(24000, rate);
            Assert.AreEqual(2, samples.Length);
            Assert.AreEqual(0f, samples[0], 0.0001f);
            Assert.AreEqual(16000f / 32768f, samples[1], 0.0001f);
            Assert.IsTrue(SpeechAudio.TryDecode(wav, "audio/wav", 24000, true, out _, out _, out _));
        }

        static byte[] TruncatedHeader(byte[] wav)
        {
            var truncated = new byte[32];
            Buffer.BlockCopy(wav, 0, truncated, 0, truncated.Length);
            return truncated;
        }

        static byte[] Pcm16(params short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++)
                WriteShort(bytes, i * 2, samples[i]);
            return bytes;
        }

        static byte[] StreamingWav(uint dataSize, params short[] samples)
        {
            var wav = Wav16(24000, samples);
            WriteUInt(wav, 4, 0u);
            WriteUInt(wav, 40, dataSize);
            return wav;
        }

        static byte[] WavWithOddList(params short[] samples)
        {
            var dataBytes = samples.Length * 2;
            var bytes = new byte[12 + 24 + 14 + 8 + dataBytes];
            WriteAscii(bytes, 0, "RIFF");
            WriteInt(bytes, 4, bytes.Length - 8);
            WriteAscii(bytes, 8, "WAVE");
            WriteAscii(bytes, 12, "fmt ");
            WriteInt(bytes, 16, 16);
            WriteShort(bytes, 20, 1);
            WriteShort(bytes, 22, 1);
            WriteInt(bytes, 24, 24000);
            WriteInt(bytes, 28, 48000);
            WriteShort(bytes, 32, 2);
            WriteShort(bytes, 34, 16);
            WriteAscii(bytes, 36, "LIST");
            WriteInt(bytes, 40, 5);
            WriteAscii(bytes, 44, "abcde");
            bytes[49] = 0;
            WriteAscii(bytes, 50, "data");
            WriteInt(bytes, 54, dataBytes);
            for (var i = 0; i < samples.Length; i++)
                WriteShort(bytes, 58 + i * 2, samples[i]);
            return bytes;
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

        static void WriteUInt(byte[] bytes, int offset, uint value)
        {
            WriteInt(bytes, offset, unchecked((int)value));
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
