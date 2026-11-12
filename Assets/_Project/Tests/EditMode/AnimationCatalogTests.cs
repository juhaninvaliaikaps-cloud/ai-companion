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
        public void BuildPromptList_WritesThePortraitFaceWhenTheRowHasOne()
        {
            AnimationClipCatalog catalog = NewCatalog(Row("embarrassed", "a compliment", Emotion.Neutral, string.Empty));
            catalog.Entries[0].portrait = "embarrassed";

            Assert.AreEqual("- embarrassed: a compliment; emotion: neutral; face: embarrassed", catalog.BuildPromptList());
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
        public void Validate_ReportsDuplicateGesturesBrokenTokensAndPromptLineBreaks()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row("wave_hello", "friendly", Emotion.Happy, "wave"),
                Row("other", "also a wave", Emotion.Happy, "WAVE"),
                Row("bad id", "space in the id", Emotion.Neutral, "nod!"),
                Row("note", "line one\nline two", Emotion.Neutral, string.Empty));

            Assert.IsFalse(catalog.Validate(out List<string> problems));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("Duplicate gesture 'WAVE'")));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("has an id that cannot be written")));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("gesture that cannot be written")));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("line break in its description")));
        }

        [Test]
        public void Validate_ReportsAGestureThatCollidesWithAnotherId()
        {
            AnimationClipCatalog catalog = NewCatalog(
                Row("wave", "the emotion named wave", Emotion.Happy, string.Empty),
                Row("wave_hello", "friendly hello wave", Emotion.Happy, "wave"));

            Assert.IsFalse(catalog.Validate(out List<string> problems));
            Assert.IsTrue(problems.Exists(problem => problem.Contains("another row's id")));
        }

        [Test]
        public void StarterCatalogAsset_HasTwelveValidEntriesWithResolvedClips()
        {
            AnimationClipCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "Missing catalog asset at " + CatalogPath);
            Assert.AreEqual(40, catalog.Entries.Count);

            Assert.IsTrue(catalog.Validate(out List<string> problems), string.Join("\n", problems));
            Assert.AreEqual(40, catalog.BuildPromptList().Split('\n').Length);

            Assert.IsTrue(catalog.TryGetById("laughing", out AnimationEntry laughing));
            Assert.AreEqual("laughing", laughing.portrait);
            Assert.AreEqual(Emotion.Happy, laughing.emotion);
            Assert.IsFalse(laughing.HasGesture);
            Assert.IsFalse(laughing.HasAnimatorState);
            Assert.IsTrue(laughing.clip == null);
            StringAssert.Contains("- laughing: laughing, open smile on the portrait while the body stays still; emotion: happy; face: laughing; tags: amused, laugh", catalog.BuildPromptList());
            Assert.IsFalse(catalog.TryGetById("talk", out _), "Talking is face-only, so the body talk row stays out of the catalog.");

            Assert.IsTrue(catalog.TryGetById("idle", out AnimationEntry idle));
            Assert.IsNotNull(idle.clip, "The idle row lost its clip reference.");
            Assert.AreEqual("Idle", idle.animatorState);

            Assert.IsTrue(catalog.TryGetByGesture("wave", out AnimationEntry wave));
            Assert.AreEqual("wave_hello", wave.id);
            Assert.IsNotNull(wave.clip, "The wave row lost its clip reference.");
            Assert.AreEqual("Wave", wave.animatorState);

            Assert.IsTrue(catalog.TryGetByGesture("walk", out AnimationEntry walk));
            Assert.AreEqual("Walk", walk.animatorState);
            StringAssert.Contains("walk along the desktop", walk.description);
            Assert.IsTrue(catalog.TryGetByGesture("stop", out AnimationEntry stop));
            Assert.AreEqual("stop", stop.id);
            Assert.IsTrue(catalog.TryGetByGesture("turn", out AnimationEntry turn));
            Assert.AreEqual("Turn180", turn.animatorState);

            Assert.IsTrue(catalog.TryGetById("embarrassed", out AnimationEntry embarrassed));
            Assert.AreEqual("embarrassed", embarrassed.portrait);
            Assert.AreEqual(Emotion.Neutral, embarrassed.emotion);
            Assert.IsTrue(catalog.TryGetById("shy", out AnimationEntry shy));
            Assert.AreEqual("shy", shy.portrait);
            Assert.IsTrue(catalog.TryGetById("flustered", out AnimationEntry flustered));
            Assert.AreEqual("flustered", flustered.portrait);

            Assert.IsTrue(catalog.TryGetById("pouty", out AnimationEntry poutyRow));
            Assert.AreEqual("pouty", poutyRow.portrait, "The pouty row must keep the pouty still, not the angry one.");

            Assert.IsTrue(catalog.TryGetById("crying", out AnimationEntry crying));
            Assert.AreEqual("crying", crying.portrait);
            Assert.AreEqual(Emotion.Sad, crying.emotion);
            Assert.IsTrue(catalog.TryGetByGesture("crying", out AnimationEntry cryingGesture));
            Assert.AreEqual("crying", cryingGesture.portrait);
            Assert.IsTrue(catalog.TryGetByGesture("smug_wink", out AnimationEntry wink));
            Assert.AreEqual("smug_wink", wink.portrait);
            Assert.IsTrue(catalog.TryGetByGesture("kiss", out AnimationEntry kiss));
            Assert.AreEqual("kiss", kiss.portrait);
            Assert.IsTrue(catalog.TryGetById("embarrassed_worried", out AnimationEntry mixed));
            Assert.AreEqual(Emotion.Concerned, mixed.emotion);
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
