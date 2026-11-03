using AICompanion.AnimationDirector;
using AICompanion.Core;
using NUnit.Framework;
using UnityEditor;

namespace AICompanion.Tests.EditMode
{
    public class DirectorRulebookTests
    {
        const string CatalogPath = "Assets/_Project/Settings/AnimationCatalog.asset";

        static DirectorDecision Gesture(string state, string entryId, float duration, int priority = 0)
        {
            return new DirectorDecision(
                DirectorAction.GestureState,
                state,
                Emotion.Neutral,
                0.5f,
                entryId,
                false,
                null,
                duration,
                priority);
        }

        static DirectorDecision Mood()
        {
            return new DirectorDecision(DirectorAction.Emotion, string.Empty, Emotion.Happy, 0.6f, "happy");
        }

        [Test]
        public void EmotionCuesAlwaysPass()
        {
            var rules = new DirectorRulebook();

            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Mood(), 0f));
            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Mood(), 0f));
            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Mood(), 1f));
        }

        [Test]
        public void TheSameRowCoolsDown()
        {
            var rules = new DirectorRulebook(6f);
            var wave = Gesture("Wave", "wave_hello", 1.4f);

            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(wave, 0f));
            Assert.AreEqual(RuleVerdict.Cooldown, rules.Allow(wave, 2f));
            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(wave, 7f));
        }

        [Test]
        public void APlayingGestureBlocksAnEqualPriorityOne()
        {
            var rules = new DirectorRulebook();

            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Gesture("Wave", "wave_hello", 1.4f), 0f));
            Assert.AreEqual(RuleVerdict.Busy, rules.Allow(Gesture("Nod", "nod_yes", 1f), 0.3f));
        }

        [Test]
        public void HigherPriorityInterrupts()
        {
            var rules = new DirectorRulebook();

            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Gesture("Wave", "wave_hello", 1.4f), 0f));
            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Gesture("Wave", "wave_bye", 1.4f, priority: 1), 0.3f));
        }

        [Test]
        public void AfterTheClipEndsANewGestureIsAllowed()
        {
            var rules = new DirectorRulebook();

            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Gesture("Wave", "wave_hello", 1.4f), 0f));
            Assert.AreEqual(RuleVerdict.Allow, rules.Allow(Gesture("Nod", "nod_yes", 1f), 2f));
        }

        [Test]
        public void AWrongCueFallsBackToATagFamily()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "Missing " + CatalogPath);

            Assert.IsTrue(catalog.TryGetByTag("greeting", out var entry), "No row carries the greeting tag.");
            Assert.AreEqual("wave_hello", entry.id);
        }
    }
}
