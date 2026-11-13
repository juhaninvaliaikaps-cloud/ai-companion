using System;
using System.IO;
using System.Reflection;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-119 review: a wipe must drop both keepers' in-memory state.</summary>
    public sealed class KeeperForgetTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
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
        public void RelationshipKeeper_ForgetDropsTheStateAndStopsSaving()
        {
            var path = Path.Combine(NewFolder(), "relationship-happi.json");
            InstallKeyFile.KeyForPath = _ => Key();
            var go = new GameObject("relationship-forget-test");
            try
            {
                var keeper = go.AddComponent<RelationshipKeeper>();
                keeper.State.Affection = 55f;
                keeper.State.Familiarity = 12f;
                Set(keeper, "_loaded", true);
                Set(keeper, "_dirty", true);
                Set(keeper, "_path", path);
                Set(keeper, "_personaId", "happi");

                keeper.Forget();

                Assert.IsFalse(keeper.IsLoaded);
                Assert.AreEqual(0f, keeper.State.Affection);
                Assert.AreEqual(0f, keeper.State.Familiarity);
                Invoke(keeper, "Save");
                Assert.IsFalse(File.Exists(path), "Forget must prevent the old numbers from being saved");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CompanionMoodKeeper_ForgetDropsTheMoodAndStopsSaving()
        {
            var path = Path.Combine(NewFolder(), "mood-happi.json");
            InstallKeyFile.KeyForPath = _ => Key();
            var go = new GameObject("mood-forget-test");
            try
            {
                var keeper = go.AddComponent<CompanionMoodKeeper>();
                keeper.State.Push(CompanionMood.Happy, 1f);
                Set(keeper, "_loaded", true);
                Set(keeper, "_dirty", true);
                Set(keeper, "_path", path);
                Set(keeper, "_personaId", "happi");

                keeper.Forget();

                Assert.IsFalse(keeper.IsLoaded);
                Assert.AreEqual(CompanionMood.Content, keeper.State.Shown);
                Invoke(keeper, "Save");
                Assert.IsFalse(File.Exists(path), "Forget must prevent the old mood from being saved");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ActivityWindow_OnlyCountsRecentActivity()
        {
            Assert.IsTrue(RelationshipKeeper.IsWithinActivityWindow(200f, 100f));
            Assert.IsTrue(RelationshipKeeper.IsWithinActivityWindow(280f, 100f));
            Assert.IsFalse(RelationshipKeeper.IsWithinActivityWindow(280.1f, 100f));
            Assert.IsFalse(RelationshipKeeper.IsWithinActivityWindow(100f, float.NegativeInfinity));
        }

        static void Set(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, Private);
            Assert.IsNotNull(info, field);
            info.SetValue(target, value);
        }

        static void Invoke(object target, string method)
        {
            var info = target.GetType().GetMethod(method, Private);
            Assert.IsNotNull(info, method);
            info.Invoke(target, null);
        }

        static byte[] Key()
        {
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
                key[i] = (byte)(i * 5 + 1);
            return key;
        }

        string NewFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "leia-keeper-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _cleanup.Add(folder);
            return folder;
        }
    }
}
