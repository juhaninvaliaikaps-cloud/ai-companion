using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-125: her 18+ secrets (kinks, sensitive areas, shy to do). Local model only.</summary>
    public sealed class IntimateSecretsTests
    {
        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Configure(false, false, false);
            IntimateSecretsNow.Reset();
        }

        sealed class FakeProvider : IChatProvider
        {
            public string Id = ChatDefaults.LocalId;
            public string Reply = "```json\n{\"kinks\": [{\"genre\": \"BDSM\", \"detail\": \"being tied up\"}, {\"genre\": \"Roleplay\", \"detail\": \"teacher and student\"}], " +
                                  "\"sensitive\": [\"her neck\"], \"shy\": [\"anal\"]}\n```";
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

        static IntimateSecretsState Read()
        {
            var state = new IntimateSecretsState();
            state.SetReading("hash1",
                new List<IntimateKink>
                {
                    new IntimateKink { genre = "BDSM", detail = "being tied up" },
                    new IntimateKink { genre = "Roleplay", detail = "teacher and student." },
                    new IntimateKink { genre = "", detail = "" },
                    new IntimateKink { genre = "Praise", detail = "" },
                },
                new List<string> { "her neck", "", "her ears" },
                new List<string> { "anal" },
                3, 2, 1);
            return state;
        }

        // ---- How many ------------------------------------------------------------------------

        [Test]
        public void Counts_AreOneToThree_TheSameForTheSameDescription()
        {
            int k1, s1, y1, k2, s2, y2;
            IntimateSecretCounts.For("mira", "abc", out k1, out s1, out y1);
            IntimateSecretCounts.For("mira", "abc", out k2, out s2, out y2);
            Assert.AreEqual(k1, k2);
            Assert.AreEqual(s1, s2);
            Assert.AreEqual(y1, y2);
            foreach (var n in new[] { k1, s1, y1 })
                Assert.That(n, Is.InRange(1, 3));
        }

        [Test]
        public void Counts_KinksAndSensitiveLeanHigher_ShyLower()
        {
            double kinks = 0, sensitive = 0, shy = 0;
            const int personas = 2000;
            for (var i = 0; i < personas; i++)
            {
                int k, s, y;
                IntimateSecretCounts.For("persona" + i, "hash" + (i * 7), out k, out s, out y);
                kinks += k;
                sensitive += s;
                shy += y;
            }

            // Expected means: 2.2 for 20/40/40, 1.65 for 50/35/15.
            Assert.AreEqual(2.2, kinks / personas, 0.1);
            Assert.AreEqual(2.2, sensitive / personas, 0.1);
            Assert.AreEqual(1.65, shy / personas, 0.1);
        }

        [Test]
        public void Pick_FollowsTheWeights()
        {
            Assert.AreEqual(1, IntimateSecretCounts.Pick(0.0, IntimateSecretCounts.MoreWeights));
            Assert.AreEqual(1, IntimateSecretCounts.Pick(0.19, IntimateSecretCounts.MoreWeights));
            Assert.AreEqual(2, IntimateSecretCounts.Pick(0.21, IntimateSecretCounts.MoreWeights));
            Assert.AreEqual(3, IntimateSecretCounts.Pick(0.99, IntimateSecretCounts.MoreWeights));
            Assert.AreEqual(1, IntimateSecretCounts.Pick(0.49, IntimateSecretCounts.FewerWeights));
            Assert.AreEqual(3, IntimateSecretCounts.Pick(0.9, IntimateSecretCounts.FewerWeights));
        }

        // ---- The reading ---------------------------------------------------------------------

        [Test]
        public void SetReading_CutsToTheCounts_GivesIds_AndSkipsBlanks()
        {
            var state = Read();
            Assert.IsTrue(state.HasReading);
            var items = state.Items();
            Assert.AreEqual(3 + 2 + 1, items.Count);
            Assert.AreEqual("k1", items[0].Id);
            Assert.AreEqual("BDSM: being tied up", items[0].Text);
            Assert.AreEqual("Roleplay: teacher and student", items[1].Text);
            Assert.AreEqual("k3", items[2].Id, "the blank kink is skipped");
            Assert.AreEqual("Praise", items[2].Text, "a genre alone is fine");
            Assert.AreEqual("s1", items[3].Id);
            Assert.AreEqual("s2", items[4].Id);
            Assert.AreEqual("her ears", items[4].Text);
            Assert.AreEqual("y1", items[5].Id);
            Assert.AreEqual(IntimateSecretGroup.Shy, items[5].Group);
            Assert.IsFalse(new IntimateSecretsState().HasReading);
        }

        [Test]
        public void Check_FirstThenAgain_AFindSurvivesAReRead_AndTheGroupIsInTheKey()
        {
            var state = Read();
            IntimateSecretItem item;
            Assert.AreEqual(SecretHitKind.First, state.Check("s1", out item));
            Assert.AreEqual("her neck", item.Text);
            Assert.IsTrue(state.Found.Reveal(item.Key));
            Assert.AreEqual(SecretHitKind.Again, state.Check("S1", out item));
            Assert.AreEqual(SecretHitKind.None, state.Check("s9", out item));
            Assert.AreEqual(SecretHitKind.None, state.Check("t1", out item), "her ordinary secrets are not here");

            state.SetReading("hash2", null, new List<string> { "her ears", "her neck" }, new List<string> { "her neck" }, 3, 3, 3);
            Assert.AreEqual(SecretHitKind.Again, state.Check("s2", out item), "her neck moved to s2 and is still found");
            Assert.AreEqual(SecretHitKind.First, state.Check("y1", out item), "the same words as a shy-to-do are a different secret");
        }

        [Test]
        public void State_RoundTripsThroughJson()
        {
            var state = Read();
            IntimateSecretItem item;
            state.Check("k2", out item);
            state.Found.Reveal(item.Key);

            var back = IntimateSecretsState.FromJson(state.ToJson());
            Assert.AreEqual("hash1", back.ReadHash);
            Assert.AreEqual(6, back.Items().Count);
            Assert.AreEqual(SecretHitKind.Again, back.Check("k2", out item));
            Assert.AreEqual(item.Key, back.Found.NewestKey);
            Assert.IsFalse(IntimateSecretsState.FromJson("junk").HasReading);
        }

        [Test]
        public void IntimateIds_AreKSY_AndHerOrdinarySecretsAreNot()
        {
            Assert.IsTrue(IntimateSecretsState.IsIntimateId("k1"));
            Assert.IsTrue(IntimateSecretsState.IsIntimateId("S2"));
            Assert.IsTrue(IntimateSecretsState.IsIntimateId("y3"));
            Assert.IsFalse(IntimateSecretsState.IsIntimateId("t1"));
            Assert.IsFalse(IntimateSecretsState.IsIntimateId("w1"));
            Assert.IsFalse(IntimateSecretsState.IsIntimateId("k"));
            Assert.IsFalse(IntimateSecretsState.IsIntimateId(null));
        }

        // ---- The prompt ----------------------------------------------------------------------

        [Test]
        public void PromptLine_OnlyWithTheGateOpen_AndShyFollowsHerInnocence()
        {
            var items = Read().Items();
            Assert.AreEqual(string.Empty, IntimateSecretsPrompt.Line(items, 50f, false));
            Assert.AreEqual(string.Empty, IntimateSecretsPrompt.Line(new List<IntimateSecretItem>(), 50f, true));

            var line = IntimateSecretsPrompt.Line(items, 10f, true);
            StringAssert.Contains("Kinks: (k1) BDSM: being tied up, (k2) Roleplay: teacher and student, (k3) Praise.", line);
            StringAssert.Contains("Sensitive areas: (s1) her neck, (s2) her ears.", line);
            StringAssert.Contains("Shy to do: (y1) anal.", line);
            StringAssert.Contains("shy away", line, "very innocent");
            StringAssert.Contains("secret=", line);
            StringAssert.Contains("coaxed", IntimateSecretsPrompt.Line(items, 50f, true), "in between");
            StringAssert.Contains("open to them", IntimateSecretsPrompt.Line(items, 95f, true), "perverted");
            Assert.IsFalse(line.Contains("\n"));
        }

        [Test]
        public void Now_AddsNothingWhileTheGateIsClosed_WhateverWasPublished()
        {
            IntimateSecretsNow.SetPromptLine("Your intimate secrets.");
            Assert.AreEqual("Persona.", IntimateSecretsNow.Append("Persona."), "gate closed");

            IntimateExpressions.Configure(true, true, true);
            Assert.AreEqual("Persona.\n\nYour intimate secrets.", IntimateSecretsNow.Append("Persona."));

            IntimateSecretsNow.Reset();
            Assert.AreEqual("Persona.", IntimateSecretsNow.Append("Persona."));
        }

        // ---- The reader: local model only --------------------------------------------------------

        [Test]
        public void Read_NeverRunsWithTheGateClosed_OrOnACloudModel()
        {
            var local = new FakeProvider();
            var closed = Task.Run(() => IntimateSecretsReader.ReadAsync(local, "Mira", "Bold.", 2, 1, 1, false, CancellationToken.None)).Result;
            Assert.IsFalse(closed.Ok);
            Assert.AreEqual(0, local.Calls, "nothing is sent while 18+ is off");

            var cloud = new FakeProvider { Id = ChatDefaults.GrokId };
            var refused = Task.Run(() => IntimateSecretsReader.ReadAsync(cloud, "Mira", "Bold.", 2, 1, 1, true, CancellationToken.None)).Result;
            Assert.AreEqual(IntimateSecretsReader.CloudRefused, refused.Failure);
            Assert.AreEqual(0, cloud.Calls, "a cloud model never reads this");

            var read = Task.Run(() => IntimateSecretsReader.ReadAsync(local, "Mira", "Bold.", 2, 1, 1, true, CancellationToken.None)).Result;
            Assert.IsTrue(read.Ok);
            Assert.AreEqual(2, read.Kinks.Count);
            Assert.AreEqual("her neck", read.Sensitive[0]);
            Assert.AreEqual("anal", read.Shy[0]);
            Assert.IsTrue(local.Last.BlockCloud);
            Assert.IsTrue(local.Last.NoMemory);
            StringAssert.Contains("kinks: exactly 2", local.Last.SystemPrompt);
            StringAssert.Contains("sensitive: exactly 1", local.Last.SystemPrompt);

            var custom = new FakeProvider { Id = ChatDefaults.CustomId };
            Assert.IsTrue(Task.Run(() => IntimateSecretsReader.ReadAsync(custom, "Mira", "Bold.", 1, 1, 1, true, CancellationToken.None)).Result.Ok, "the user's own private server is fine");

            var bad = new FakeProvider { Reply = "She likes many things." };
            Assert.AreEqual(IntimateSecretsReader.BadReply, Task.Run(() => IntimateSecretsReader.ReadAsync(bad, "Mira", "Bold.", 1, 1, 1, true, CancellationToken.None)).Result.Failure);
        }

        // ---- Rewards ------------------------------------------------------------------------

        [Test]
        public void HerKinkAgain_AlwaysRewardsMore_EvenOnACrit()
        {
            var plain = RelationshipMath.Land(LandingEvent.Flirted, 0.5f, 20f, 50f, false);
            var kink = RelationshipMath.Land(LandingEvent.Flirted, 0.5f, 20f, 50f, false, RelationshipMath.FavouriteMultiplier);
            Assert.AreEqual(plain.AffectionDelta * 1.25f, kink.AffectionDelta, 0.001f);
            Assert.IsFalse(kink.Critical);

            var crit = RelationshipMath.Land(LandingEvent.Flirted, 0.9f, 20f, 50f, false);
            var critKink = RelationshipMath.Land(LandingEvent.Flirted, 0.9f, 20f, 50f, false, RelationshipMath.FavouriteMultiplier);
            Assert.Greater(critKink.AffectionDelta, crit.AffectionDelta);

            var odd = RelationshipMath.Land(LandingEvent.Flirted, 0.5f, 20f, 50f, false, 0.5f);
            Assert.AreEqual(plain.AffectionDelta, odd.AffectionDelta, 0.001f, "a bonus below 1 is ignored");
            Assert.AreEqual(plain.AffectionDelta, RelationshipMath.Land(LandingEvent.Flirted, 0.5f, 20f, 50f, false, float.NaN).AffectionDelta, 0.001f);
        }

        // ---- What it looks like --------------------------------------------------------------

        [Test]
        public void Profile_ShowsFoundOnes_LocksTheRest_AndSaysHowShySheIs()
        {
            var state = Read();
            IntimateSecretItem item;
            state.Check("y1", out item);
            state.Found.Reveal(item.Key);

            var view = ProfileIntimateSecrets.Build(state, 50f, string.Empty);
            Assert.AreEqual("Her 18+ secrets · 1 of 6 found", view.Title);
            Assert.AreEqual(ProfileSecrets.Locked, view.Items[0].Text);
            Assert.AreEqual("anal", view.Items[5].Text);
            Assert.IsTrue(view.Items[5].Newest);
            Assert.AreEqual("How shy she is now: in between", view.ShyLine);
            Assert.AreEqual(0, ProfileIntimateSecrets.Build(null, 50f, "Reading").Total);
        }

        [Test]
        public void Files_ArePerPersona_AndEncrypted()
        {
            Assert.AreEqual("intimate-secrets.json", Path.GetFileName(IntimateSecretsStore.FileFor("data", string.Empty)));
            StringAssert.StartsWith("intimate-secrets-", Path.GetFileName(IntimateSecretsStore.FileFor("data", "mira")));
            Assert.IsTrue(LocalDataInventory.IsIntimateSecretsName("intimate-secrets-mira.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("intimate-secrets-mira.json"));
            Assert.IsFalse(LocalDataInventory.IsSecretFile("intimate-secrets.json"));
        }
    }
}
