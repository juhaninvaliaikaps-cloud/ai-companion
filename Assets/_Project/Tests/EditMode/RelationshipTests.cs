using System;
using System.IO;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-119: relationship storage, readout and local-data coverage.</summary>
    public sealed class RelationshipTests
    {
        readonly System.Collections.Generic.List<string> _cleanup = new System.Collections.Generic.List<string>();

        [TearDown]
        public void TearDown()
        {
            InstallKeyFile.KeyForPath = null;
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
        public void Defaults_AreZero()
        {
            var state = new RelationshipState();
            Assert.AreEqual(0f, state.Affection);
            Assert.AreEqual(0f, state.Familiarity);
            Assert.AreEqual(0d, state.SecondsTogether);
        }

        [Test]
        public void Values_AreClamped_AndNaNBecomesTheDefault()
        {
            var state = new RelationshipState
            {
                Affection = 150f,
                Familiarity = 150f,
                SecondsTogether = -4d,
            };
            Assert.AreEqual(100f, state.Affection);
            Assert.AreEqual(100f, state.Familiarity);
            Assert.AreEqual(0d, state.SecondsTogether);

            state.Affection = -150f;
            state.Familiarity = -5f;
            state.Affection = float.NaN;
            state.Familiarity = float.NaN;
            state.SecondsTogether = double.NaN;
            Assert.AreEqual(0f, state.Affection);
            Assert.AreEqual(0f, state.Familiarity);
            Assert.AreEqual(0d, state.SecondsTogether);
        }

        [Test]
        public void Json_RoundTrip_KeepsTheNumbers()
        {
            var state = new RelationshipState
            {
                Affection = 35.5f,
                Familiarity = 12.25f,
            };
            state.AddSeconds(123.5d);
            var now = new DateTime(2026, 11, 13, 18, 0, 0, DateTimeKind.Utc);

            var back = RelationshipState.FromJson(state.ToJson(now));

            Assert.AreEqual(35.5f, back.Affection, 0.001f);
            Assert.AreEqual(12.25f, back.Familiarity, 0.001f);
            Assert.AreEqual(123.5d, back.SecondsTogether, 0.001d);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"affection\":500,\"familiarity\":-10,\"secondsTogether\":-7}")]
        public void BrokenOrOutOfRangeFile_GivesDefaultsOrClamps(string json)
        {
            var state = RelationshipState.FromJson(json);
            Assert.IsNotNull(state);
            Assert.GreaterOrEqual(state.Affection, -100f);
            Assert.LessOrEqual(state.Affection, 100f);
            Assert.GreaterOrEqual(state.Familiarity, 0f);
            Assert.LessOrEqual(state.Familiarity, 100f);
            Assert.GreaterOrEqual(state.SecondsTogether, 0d);
        }

        [Test]
        public void Readout_UsesTheSignedAffectionAndFamiliarity()
        {
            var state = new RelationshipState { Affection = 35f, Familiarity = 12f };
            Assert.AreEqual("Relationship: +35, familiarity 12", state.Readout());

            state.Affection = -4.5f;
            Assert.AreEqual("Relationship: -4.5, familiarity 12", state.Readout());
        }

        [Test]
        public void Store_EncryptedRoundTripPerPersona()
        {
            var folder = NewRoot();
            var key = Key();
            InstallKeyFile.KeyForPath = _ => key;

            var lola = RelationshipStore.FileFor(folder, "Lola Bunny");
            var other = RelationshipStore.FileFor(folder, string.Empty);
            Assert.AreEqual("relationship-lola-bunny.json", Path.GetFileName(lola));
            Assert.AreEqual("relationship.json", Path.GetFileName(other));

            var state = new RelationshipState { Affection = 20f, Familiarity = 5f };
            state.AddSeconds(60d);
            Assert.IsTrue(RelationshipStore.Save(lola, state, DateTime.UtcNow));

            var bytes = File.ReadAllBytes(lola);
            Assert.IsTrue(LocalAesGcm.IsMagic(bytes));
            StringAssert.DoesNotContain("affection", System.Text.Encoding.UTF8.GetString(bytes));

            var back = RelationshipStore.Load(lola);
            Assert.AreEqual(20f, back.Affection, 0.001f);
            Assert.AreEqual(5f, back.Familiarity, 0.001f);
            Assert.AreEqual(60d, back.SecondsTogether, 0.001d);

            // The default persona has its own file and does not inherit Lola's numbers.
            Assert.AreEqual(0f, RelationshipStore.Load(other).Affection);
        }

        [Test]
        public void Inventory_TreatsRelationshipFilesAsPrivate()
        {
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("relationship.json"));
            Assert.IsTrue(LocalDataInventory.IsEncryptedFile("relationship-lola.json"));
            Assert.IsTrue(LocalDataInventory.IsRelationshipName("relationship-lola.json"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("relationships.png"));
            Assert.IsFalse(LocalDataInventory.IsEncryptedFile("portraits/relationship.json"));
        }

        [Test]
        public void Wipe_RemovesTheRelationshipFile()
        {
            var root = NewRoot();
            InstallKeyFile.KeyForPath = _ => Key();
            var path = RelationshipStore.FileFor(root, "lola");
            Assert.IsTrue(RelationshipStore.Save(path, new RelationshipState { Affection = 10f }, DateTime.UtcNow));
            Assert.IsTrue(File.Exists(path));

            var result = LocalDataActions.Wipe(root);

            Assert.IsTrue(result.Ok);
            Assert.IsFalse(File.Exists(path));
        }

        static byte[] Key()
        {
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = (byte)(i * 9 + 3);
            return key;
        }

        string NewRoot()
        {
            var parent = Path.Combine(Path.GetTempPath(), "leia-relationship-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(parent, "ProjectLeia");
            Directory.CreateDirectory(root);
            _cleanup.Add(parent);
            return root;
        }
    }
}
