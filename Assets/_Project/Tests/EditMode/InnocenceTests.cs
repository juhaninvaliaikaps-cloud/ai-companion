using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-116: Innocent to Perverted (adult mode only).</summary>
    public class InnocenceTests
    {
        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Configure(false, false, false);
            InnocenceNow.Reset();
        }

        sealed class FakeProvider : IChatProvider
        {
            public string Id = ChatDefaults.LocalId;
            public string Reply = "{\"innocence\": 30, \"reason\": \"She is 19, shy, and has never dated.\"}";
            public int Calls;
            public ChatRequest Last;

            public string ProviderId { get { return Id; } }
            public string DisplayName { get { return Id; } }
            public bool IsConfigured { get { return true; } }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                return Task.FromResult(ChatResult.Success(Reply));
            }
        }

        static AnimationCue Cue(string emotionField = null, string reaction = null, float intensity = 0.5f)
        {
            return new AnimationCue(Emotion.Neutral, intensity, string.Empty, 0f, emotionField, reaction);
        }

        // ---- Math ----------------------------------------------------------------------------

        [Test]
        public void AMoment_MovesHerTowardExperienced_SlowerNearTheTop()
        {
            var low = InnocenceMath.AfterMoment(10f, 0.5f) - 10f;
            var high = InnocenceMath.AfterMoment(90f, 0.5f) - 90f;
            Assert.Greater(low, 0f);
            Assert.Greater(high, 0f);
            Assert.Greater(low, high * 5f, "the closer to the top, the smaller the step");
            Assert.Greater(InnocenceMath.AfterMoment(10f, 1f), InnocenceMath.AfterMoment(10f, 0f), "a stronger moment moves her more");

            var value = 0f;
            for (var i = 0; i < 1000; i++)
                value = InnocenceMath.AfterMoment(value, 1f);
            Assert.LessOrEqual(value, InnocenceMath.Max);
        }

        [Test]
        public void KinkMoment_MovesHerMoreThanAnOrdinaryMoment()
        {
            var normal = InnocenceMath.AfterMoment(20f, 0.5f);
            var sensitive = InnocenceMath.AfterMoment(20f, 0.5f, InnocenceMath.SensitiveMomentBoost);
            var kink = InnocenceMath.AfterMoment(20f, 0.5f, InnocenceMath.KinkMomentBoost);
            Assert.Greater(sensitive, normal);
            Assert.Greater(kink, sensitive);
            Assert.LessOrEqual(kink, InnocenceMath.Max);
        }

        [Test]
        public void DriftBack_WaitsTwoDays_ThenHalvesTheGainEveryTwoWeeks_NeverBelowTheBaseline()
        {
            Assert.AreEqual(60f, InnocenceMath.DriftBack(60f, 20f, 1d), 0.001f, "nothing fades within the grace days");
            Assert.AreEqual(40f, InnocenceMath.DriftBack(60f, 20f, InnocenceMath.GraceDays + InnocenceMath.HalfLifeDays), 0.01f, "half of the 40 gained is gone");
            Assert.AreEqual(20f, InnocenceMath.DriftBack(60f, 20f, 10000d), 0.01f, "she ends at her baseline");
            Assert.AreEqual(10f, InnocenceMath.DriftBack(10f, 20f, 100d), 0.001f, "never pulled up by drifting");
            Assert.AreEqual(60f, InnocenceMath.DriftBack(60f, 20f, double.NaN), 0.001f);
        }

        [Test]
        public void Rebase_KeepsWhatSheGained()
        {
            Assert.AreEqual(55f, InnocenceMath.Rebase(35f, 20f, 40f), 0.001f);
            Assert.AreEqual(40f, InnocenceMath.Rebase(15f, 20f, 40f), 0.001f, "below the old baseline gains nothing");
            Assert.AreEqual(100f, InnocenceMath.Rebase(90f, 10f, 80f), 0.001f);
        }

        [Test]
        public void Bands_AndNames()
        {
            Assert.AreEqual("very innocent", InnocenceMath.BandName(5f));
            Assert.AreEqual("innocent", InnocenceMath.BandName(20f));
            Assert.AreEqual("in between", InnocenceMath.BandName(50f));
            Assert.AreEqual("experienced", InnocenceMath.BandName(75f));
            Assert.AreEqual("perverted", InnocenceMath.BandName(95f));
            Assert.AreEqual(InnocenceMath.Default, InnocenceMath.Clamp(float.NaN));
            Assert.AreEqual(100f, InnocenceMath.Clamp(250f));
        }

        [Test]
        public void IsMoment_OnlyIntimateCues_AndOnlyWithTheGateOpen()
        {
            Assert.IsTrue(InnocenceMath.IsMoment(Cue("finger_suck"), true));
            Assert.IsTrue(InnocenceMath.IsMoment(Cue(reaction: "strip"), true));
            Assert.IsTrue(InnocenceMath.IsMoment(Cue(reaction: "naked_idle"), true));
            Assert.IsFalse(InnocenceMath.IsMoment(Cue("happy"), true));
            Assert.IsFalse(InnocenceMath.IsMoment(Cue(reaction: "pout"), true));
            Assert.IsFalse(InnocenceMath.IsMoment(Cue("finger_suck"), false), "nothing counts while 18+ is off");
        }

        [Test]
        public void Moods_InnocentGetsFlustered_ExperiencedGetsHappy_IntimateNeverPushed()
        {
            Assert.AreEqual(CompanionMood.Flustered, Strongest(InnocenceMath.MomentPushes(5f)));
            Assert.AreEqual(CompanionMood.Flustered, Strongest(InnocenceMath.UndressedPushes(20f)));
            Assert.AreNotEqual(CompanionMood.Flustered, Strongest(InnocenceMath.MomentPushes(95f)));
            Assert.AreNotEqual(CompanionMood.Flustered, Strongest(InnocenceMath.UndressedPushes(95f)));
            Assert.Greater(Gain(InnocenceMath.UndressedPushes(5f), CompanionMood.Anxious), 0f, "very innocent and undressed: a little anxious");

            for (var v = 0f; v <= 100f; v += 5f)
            {
                foreach (var push in InnocenceMath.MomentPushes(v))
                    Assert.AreNotEqual(CompanionMood.Intimate, push.Mood);
                foreach (var push in InnocenceMath.UndressedPushes(v))
                    Assert.AreNotEqual(CompanionMood.Intimate, push.Mood);
            }
        }

        static CompanionMood Strongest(CompanionMoodPush[] pushes)
        {
            var best = pushes[0];
            for (var i = 1; i < pushes.Length; i++)
            {
                if (pushes[i].Gain > best.Gain)
                    best = pushes[i];
            }

            return best.Mood;
        }

        static float Gain(CompanionMoodPush[] pushes, CompanionMood mood)
        {
            for (var i = 0; i < pushes.Length; i++)
            {
                if (pushes[i].Mood == mood)
                    return pushes[i].Gain;
            }

            return 0f;
        }

        // ---- Prompt line ---------------------------------------------------------------------

        [Test]
        public void PromptLine_IsEmptyWithTheGateClosed()
        {
            Assert.AreEqual(string.Empty, InnocencePrompt.Line(10f, false, false));
            Assert.AreEqual(string.Empty, InnocencePrompt.Line(90f, true, false));
        }

        [Test]
        public void PromptLine_SaysExperience_FirstMove_AndComfortUndressed()
        {
            var innocent = InnocencePrompt.Line(5f, false, true);
            var perverted = InnocencePrompt.Line(95f, false, true);
            StringAssert.Contains("first move", innocent);
            StringAssert.Contains("very innocent", innocent);
            StringAssert.Contains("undress", perverted);
            Assert.AreNotEqual(innocent, perverted);
            StringAssert.Contains("naked now", InnocencePrompt.Line(5f, true, true));
            StringAssert.DoesNotContain("naked now", InnocencePrompt.Line(5f, false, true));

            for (var v = 0f; v <= 100f; v += 10f)
            {
                var line = " " + InnocencePrompt.Line(v, v > 50f, true) + " ";
                StringAssert.DoesNotContain(" he ", line, "the user's gender is never guessed");
                StringAssert.DoesNotContain(" his ", line);
            }
        }

        [Test]
        public void Now_AppendsTheLine_AndResets()
        {
            InnocenceNow.SetPromptLine("Line.");
            Assert.AreEqual("Persona.\n\nLine.", InnocenceNow.Append("Persona.  "));
            InnocenceNow.Reset();
            Assert.AreEqual("Persona.", InnocenceNow.Append("Persona."));
        }

        // ---- State and storage ---------------------------------------------------------------

        [Test]
        public void State_RoundTrips_AndBrokenFilesGiveTheDefaults()
        {
            var state = new InnocenceState();
            state.SetAt(30f, 20f);
            state.AtLastMoment = 35f;
            state.LastMomentTicks = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc).Ticks;
            state.UserSet = true;
            state.ReadHash = "abc";
            state.ReadValue = 25f;
            state.ReadReason = "Shy and 19.";

            var back = InnocenceState.FromJson(state.ToJson());
            Assert.AreEqual(30f, back.Current, 0.001f);
            Assert.AreEqual(20f, back.Baseline, 0.001f);
            Assert.AreEqual(35f, back.AtLastMoment, 0.001f);
            Assert.AreEqual(state.LastMomentTicks, back.LastMomentTicks);
            Assert.IsTrue(back.UserSet);
            Assert.IsTrue(back.HasReading);
            Assert.AreEqual("Shy and 19.", back.ReadReason);

            foreach (var broken in new[] { null, "", "not json", "{" })
            {
                var fresh = InnocenceState.FromJson(broken);
                Assert.AreEqual(InnocenceMath.Default, fresh.Current);
                Assert.IsFalse(fresh.HasReading);
                Assert.IsFalse(fresh.UserSet);
            }
        }

        [Test]
        public void OldFile_WithoutReadLibido_IsNotYetRead_AndAnExplicitZeroStaysZero()
        {
            // A file saved before JUH-170. The reason text is the quoted word readLibido, so
            // only a real object key counts. JsonUtility would load the missing float as 0;
            // that must not look already read.
            const string oldFile =
                "{\n" +
                "    \"version\": 1,\n" +
                "    \"current\": 42.0,\n" +
                "    \"baseline\": 40.0,\n" +
                "    \"atLastMoment\": 42.0,\n" +
                "    \"lastMomentTicks\": 0,\n" +
                "    \"userSet\": false,\n" +
                "    \"readHash\": \"abc\",\n" +
                "    \"readValue\": 40.0,\n" +
                "    \"readReason\": \"readLibido\"\n" +
                "}";

            var old = InnocenceState.FromJson(oldFile);
            Assert.AreEqual(42f, old.Current, 0.001f);
            Assert.AreEqual(40f, old.ReadValue, 0.001f);
            Assert.AreEqual("readLibido", old.ReadReason);
            Assert.IsTrue(old.HasReading);
            Assert.Less(old.ReadLibido, 0f, "a missing key is not yet read");

            const string explicitZeroFile =
                "{\n" +
                "    \"version\": 1,\n" +
                "    \"current\": 42.0,\n" +
                "    \"baseline\": 40.0,\n" +
                "    \"atLastMoment\": 42.0,\n" +
                "    \"lastMomentTicks\": 0,\n" +
                "    \"userSet\": false,\n" +
                "    \"readHash\": \"abc\",\n" +
                "    \"readValue\": 40.0,\n" +
                "    \"readReason\": \"readLibido\",\n" +
                "    \"readLibido\": 0.0\n" +
                "}";
            var explicitZero = InnocenceState.FromJson(explicitZeroFile);
            Assert.AreEqual(0f, explicitZero.ReadLibido, 0.001f, "an explicit 0 stays 0");

            var read = new InnocenceState();
            read.ReadHash = "abc";
            read.ReadValue = 40f;
            read.ReadLibido = 72f;
            Assert.AreEqual(72f, InnocenceState.FromJson(read.ToJson()).ReadLibido, 0.001f);
        }

        [Test]
        public void SetAt_StartsDriftingFromTheNewPlace()
        {
            var state = new InnocenceState { LastMomentTicks = 12345L };
            state.SetAt(70f, 40f);
            Assert.AreEqual(70f, state.AtLastMoment);
            Assert.AreEqual(0L, state.LastMomentTicks);
            Assert.IsTrue(double.IsNaN(state.DaysSinceMoment(DateTime.UtcNow)));
        }

        [Test]
        public void Files_ArePerPersona_AndInWipeAndExport()
        {
            StringAssert.EndsWith("innocence-mira.json", InnocenceStore.FileFor("/data", "mira").Replace('\\', '/'));
            StringAssert.EndsWith("innocence.json", InnocenceStore.FileFor("/data", string.Empty).Replace('\\', '/'));
            Assert.IsNull(InnocenceStore.FileFor(null, "mira"));
            Assert.IsTrue(LocalDataInventory.IsInnocenceName("innocence-mira.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("innocence.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("innocence-mira.json"));
            Assert.IsFalse(LocalDataInventory.IsInnocenceName("innocent.json"));
        }

        // ---- Reading -------------------------------------------------------------------------

        [Test]
        public void Parse_ReadsTheNumberAndReason()
        {
            float value;
            string reason;
            Assert.IsTrue(InnocenceReader.TryParse("Here:\n```json\n{\"innocence\": 72, \"reason\": \"Confident, 26, teases a lot.\"}\n```", out value, out reason));
            Assert.AreEqual(72f, value);
            Assert.AreEqual("Confident, 26, teases a lot.", reason);

            Assert.IsTrue(InnocenceReader.TryParse("{\"innocence\": 0.3, \"reason\": \"Shy.\"}", out value, out reason));
            Assert.AreEqual(30f, value, 0.01f, "a 0 to 1 answer is read as a fraction");
            Assert.IsTrue(InnocenceReader.TryParse("{\"innocence\": 140}", out value, out reason));
            Assert.AreEqual(100f, value);
            Assert.IsTrue(InnocenceReader.TryParse("{\"innocence\": 0, \"reason\": \"Never kissed anyone.\"}", out value, out reason));
            Assert.AreEqual(0f, value);

            Assert.IsFalse(InnocenceReader.TryParse("{\"reason\": \"no number\"}", out value, out reason));
            Assert.IsFalse(InnocenceReader.TryParse("{\"innocence\": -5}", out value, out reason));
            Assert.IsFalse(InnocenceReader.TryParse("fifty", out value, out reason));
            Assert.IsFalse(InnocenceReader.TryParse(null, out value, out reason));
        }

        [Test]
        public void Request_IsLocalOnly_AndSkipsMemory()
        {
            var request = InnocenceReader.BuildRequest("Aino", "Aino is a shy 19-year-old library assistant.");
            Assert.IsTrue(request.BlockCloud);
            Assert.IsTrue(request.NoMemory);
            Assert.AreEqual(InnocenceReader.TimeoutSeconds, request.TimeoutSeconds);
            StringAssert.Contains("age", request.SystemPrompt);
            StringAssert.Contains("Aino is a shy 19-year-old", request.Messages[0].Content);
        }

        [Test]
        public void Read_NeverRunsWithTheGateClosed_OrOnACloudModel()
        {
            var local = new FakeProvider();
            var closed = Task.Run(() => InnocenceReader.ReadAsync(local, "Aino", "Shy.", false, CancellationToken.None)).Result;
            Assert.IsFalse(closed.Ok);
            Assert.AreEqual(0, local.Calls, "nothing is sent while 18+ is off");

            var cloud = new FakeProvider { Id = ChatDefaults.GrokId };
            var refused = Task.Run(() => InnocenceReader.ReadAsync(cloud, "Aino", "Shy.", true, CancellationToken.None)).Result;
            Assert.IsFalse(refused.Ok);
            Assert.AreEqual(InnocenceReader.CloudRefused, refused.Failure);
            Assert.AreEqual(0, cloud.Calls, "a cloud model never reads this");

            var read = Task.Run(() => InnocenceReader.ReadAsync(local, "Aino", "Shy.", true, CancellationToken.None)).Result;
            Assert.IsTrue(read.Ok);
            Assert.AreEqual(30f, read.Value);
            Assert.AreEqual(1, local.Calls);
            Assert.IsTrue(local.Last.BlockCloud);

            var custom = new FakeProvider { Id = ChatDefaults.CustomId };
            Assert.IsTrue(Task.Run(() => InnocenceReader.ReadAsync(custom, "Aino", "Shy.", true, CancellationToken.None)).Result.Ok, "the user's own private server is fine");

            var bad = new FakeProvider { Reply = "She seems pretty innocent." };
            Assert.AreEqual(InnocenceReader.BadReply, Task.Run(() => InnocenceReader.ReadAsync(bad, "Aino", "Shy.", true, CancellationToken.None)).Result.Failure);
        }

        [Test]
        public void Describe_ShowsTheValueBandAndReason()
        {
            Assert.AreEqual("Read from her description: 30 (innocent): Shy and 19.", InnocenceReader.Describe(30f, "Shy and 19."));
            Assert.AreEqual("Read from her description: 70 (experienced)", InnocenceReader.Describe(70f, string.Empty));
        }
    }
}
