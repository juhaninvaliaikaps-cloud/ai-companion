using System.Collections.Generic;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class AnimationDirectorTests
    {
        readonly List<AnimationClipCatalog> _created = new List<AnimationClipCatalog>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Decide_GestureWithState_PlaysThatState()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", "Wave"));

            var decision = model.Decide(new AnimationCue(Emotion.Happy, 0.8f, "wave"));

            Assert.AreEqual(DirectorAction.GestureState, decision.Action);
            Assert.AreEqual("Wave", decision.StateName);
            Assert.AreEqual(Emotion.Happy, decision.Emotion);
            Assert.AreEqual("wave_hello", decision.EntryId);
        }

        [Test]
        public void Decide_GestureWithoutState_FallsBackToItsEmotion()
        {
            var model = NewModel(Entry("shrug", Emotion.Neutral, 0.4f, "shrug", string.Empty));

            var decision = model.Decide(new AnimationCue(Emotion.Surprised, 1f, "shrug"));

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Neutral, decision.Emotion);
            Assert.AreEqual(0.4f, decision.Intensity, 0.0001f);
            Assert.AreEqual("shrug", decision.EntryId);
        }

        [Test]
        public void Decide_UnknownGesture_FallsBackToNeutral()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", "Wave"));

            var decision = model.Decide(new AnimationCue(Emotion.Happy, 1f, "moonwalk"));

            Assert.AreEqual(DirectorAction.Neutral, decision.Action);
            Assert.AreEqual(Emotion.Neutral, decision.Emotion);
            Assert.AreEqual(AnimationDirectorModel.NeutralIntensity, decision.Intensity, 0.0001f);
        }

        [Test]
        public void Decide_EmotionCue_KeepsTheCueIntensity()
        {
            var model = NewModel();

            var decision = model.Decide(new AnimationCue(Emotion.Happy, 0.35f, string.Empty));

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Happy, decision.Emotion);
            Assert.AreEqual(0.35f, decision.Intensity, 0.0001f);
        }

        [Test]
        public void Decide_NeutralCue_IsNeutral()
        {
            var model = NewModel();

            var decision = model.Decide(AnimationCueParser.NeutralCue);

            Assert.AreEqual(DirectorAction.Neutral, decision.Action);
            Assert.AreEqual(Emotion.Neutral, decision.Emotion);
        }

        [Test]
        public void Decide_WithoutCatalog_StillHandlesAnEmotion()
        {
            var model = new AnimationDirectorModel(null);

            var decision = model.Decide(new AnimationCue(Emotion.Sad, 0.5f, string.Empty));

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Sad, decision.Emotion);
        }

        AnimationDirectorModel NewModel(params AnimationEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(entries);
            _created.Add(catalog);
            return new AnimationDirectorModel(catalog);
        }

        static AnimationEntry Entry(string id, Emotion emotion, float intensity, string gesture, string state)
        {
            return new AnimationEntry
            {
                id = id,
                description = id,
                emotion = emotion,
                intensity = intensity,
                gesture = gesture,
                animatorState = state,
            };
        }
    }
}
