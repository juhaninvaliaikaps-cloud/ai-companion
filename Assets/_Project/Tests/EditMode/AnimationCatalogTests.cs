using System.Collections.Generic;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class AnimationCatalogTests
    {
        private const string CatalogPath = "Assets/_Project/Settings/AnimationCatalog.asset";

        private readonly List<AnimationClipCatalog> _created = new List<AnimationClipCatalog>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void BuildPromptList_WritesOneLinePerNamedEntry()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row("idle", "calm", Emotion.Neutral, string.Empty, "mood"),
                Row("wave_hello", "friendly hello wave", Emotion.Happy, "wave", "greeting", "friendly"));

            string[] lines = catalog.BuildPromptList().Split('\n');

            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("- idle: calm; emotion: neutral; tags: mood", lines[0]);
            Assert.AreEqual("- wave_hello: friendly hello wave; gesture: wave; emotion: happy; tags: greeting, friendly", lines[1]);
        }

        [Test]
        public void BuildPromptList_SkipsRowsWithoutAnId()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row(string.Empty, "draft row", Emotion.Neutral, string.Empty),
                Row("happy", "cheerful", Emotion.Happy, string.Empty));

            Assert.AreEqual("- happy: cheerful; emotion: happy", catalog.BuildPromptList());
        }

        [Test]
        public void TryGetById_IgnoresCaseAndSurroundingSpace()
        {
            AnimationClipCatalog catalog = NewCatalog(Row("wave_hello", "wave", Emotion.Happy, "wave"));

            Assert.IsTrue(catalog.TryGetById("  WAVE_Hello ", out AnimationEntry entry));
            Assert.AreEqual("wave_hello", entry.id);
            Assert.IsFalse(catalog.TryGetById("missing", out _));
        }

        [Test]
        public void TryGetByGesture_OnlyMatchesGestureRows()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row("happy", "cheerful", Emotion.Happy, string.Empty),
                Row("nod_yes", "agreeing nod", Emotion.Neutral, "nod"));

            Assert.IsTrue(catalog.TryGetByGesture("NOD", out AnimationEntry entry));
            Assert.AreEqual("nod_yes", entry.id);
            Assert.IsFalse(catalog.TryGetByGesture("happy", out _));
            Assert.IsFalse(catalog.TryGetByGesture(string.Empty, out _));
        }

        [Test]
        public void TryGetByEmotion_SkipsGestureRows()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row("wave_hello", "wave", Emotion.Happy, "wave"),
                Row("happy", "cheerful", Emotion.Happy, string.Empty));

            Assert.IsTrue(catalog.TryGetByEmotion(Emotion.Happy, out AnimationEntry entry));
            Assert.AreEqual("happy", entry.id);
            Assert.IsFalse(catalog.TryGetByEmotion(Emotion.Sad, out _));
        }

        [Test]
        public void Validate_ReportsDuplicateIdsAndMissingDescriptions()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row("wave", string.Empty, Emotion.Happy, "wave"),
                Row("wave", "again", Emotion.Happy, "wave"));

            Assert.IsFalse(catalog.Validate(out List<string> problems));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("Duplicate id 'wave'")));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("has no description")));
        }

        [Test]
        public void StarterCatalogAsset_HasTwelveValidEntriesWithResolvedClips()
        {
            AnimationClipCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "Missing catalog asset at " + CatalogPath);
            Assert.AreEqual(12, catalog.Entries.Count);

            Assert.IsTrue(catalog.Validate(out List<string> problems), string.Join("\n", problems));
            Assert.AreEqual(12, catalog.BuildPromptList().Split('\n').Length);

            Assert.IsTrue(catalog.TryGetById("idle", out AnimationEntry idle));
            Assert.IsNotNull(idle.clip, "The idle row lost its clip reference.");
            Assert.AreEqual("Idle", idle.animatorState);

            Assert.IsTrue(catalog.TryGetByGesture("wave", out AnimationEntry wave));
            Assert.AreEqual("wave_hello", wave.id);
            Assert.IsNotNull(wave.clip, "The wave row lost its clip reference.");
            Assert.AreEqual("Wave", wave.animatorState);

            Assert.IsTrue(catalog.TryGetByGesture("walk", out AnimationEntry walk));
            Assert.AreEqual("Walk", walk.animatorState);
            Assert.IsTrue(catalog.TryGetByGesture("turn", out AnimationEntry turn));
            Assert.AreEqual("Turn180", turn.animatorState);
        }

        private AnimationClipCatalog NewCatalog(params AnimationEntry[] entries)
        {
            AnimationClipCatalog catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(entries);
            _created.Add(catalog);
            return catalog;
        }

        private static AnimationEntry Row(string id, string description, Emotion emotion, string gesture, params string[] tags)
        {
            return new AnimationEntry
            {
                id = id,
                description = description,
                emotion = emotion,
                gesture = gesture,
                tags = tags,
            };
        }
    }
}
