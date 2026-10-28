using System.Collections.Generic;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class ExpressionCueTests
    {
        static AnimationCue Gesture(string id, Emotion emotion = Emotion.Neutral)
        {
            return new AnimationCue(emotion, 0.5f, id);
        }

        static AnimationEntry Row(string id, Emotion emotion, string portrait)
        {
            return new AnimationEntry
            {
                id = id,
                description = id,
                tags = new[] { "test" },
                emotion = emotion,
                intensity = 0.5f,
                gesture = string.Empty,
                animatorState = "Idle",
                durationSeconds = 2f,
                portrait = portrait,
            };
        }

        [Test]
        public void PortraitRowCarriesItsExpressionName()
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(new List<AnimationEntry> { Row("embarrassed", Emotion.Neutral, "embarrassed") });
            var model = new AnimationDirectorModel(catalog);

            var decision = model.Decide(Gesture("embarrassed"));

            Assert.AreEqual("embarrassed", decision.Portrait);
            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Neutral, decision.Emotion);
        }

        [Test]
        public void NeutralFallbackHasNoPortraitName()
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(new List<AnimationEntry> { Row("teasing", Emotion.Happy, "teasing") });
            var model = new AnimationDirectorModel(catalog);

            var decision = model.Decide(Gesture("unknown_gesture"));

            Assert.AreEqual(string.Empty, decision.Portrait);
        }

        [Test]
        public void NormalRowFallsBackToTheEmotionName()
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(new List<AnimationEntry> { Row("happy", Emotion.Happy, string.Empty) });
            var model = new AnimationDirectorModel(catalog);

            var decision = model.Decide(Gesture("happy"));

            Assert.AreEqual(string.Empty, decision.Portrait);
            Assert.AreEqual(Emotion.Happy, decision.Emotion);
        }
    }
}
