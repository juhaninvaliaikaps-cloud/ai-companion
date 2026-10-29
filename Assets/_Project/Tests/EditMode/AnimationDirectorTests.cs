using System.Collections.Generic;
using System.Reflection;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Director = AICompanion.AnimationDirector.AnimationDirector;

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
            Assert.IsTrue(decision.GestureLacksState);
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
        public void Decide_CatalogId_ResolvesTheGestureRow()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", string.Empty));

            var decision = model.Decide(new AnimationCue(Emotion.Sad, 0.2f, "wave_hello"));

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Happy, decision.Emotion);
            Assert.AreEqual(0.7f, decision.Intensity, 0.0001f);
            Assert.AreEqual("wave_hello", decision.EntryId);
        }

        [Test]
        public void Decide_EmotionId_StaysOnTheEmotionPath()
        {
            var model = NewModel(Entry("happy", Emotion.Happy, 0.8f, string.Empty, "Happy"));

            var decision = model.Decide(new AnimationCue(Emotion.Neutral, 0.2f, "happy"));

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(string.Empty, decision.StateName);
            Assert.AreEqual(Emotion.Happy, decision.Emotion);
            Assert.AreEqual(0.8f, decision.Intensity, 0.0001f);
            Assert.AreEqual("happy", decision.EntryId);
        }

        [Test]
        public void Decide_WithoutCatalog_StillHandlesAnEmotion()
        {
            var model = new AnimationDirectorModel(null);

            var decision = model.Decide(new AnimationCue(Emotion.Sad, 0.5f, string.Empty));

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Sad, decision.Emotion);
        }

        [Test]
        public void Log_GestureWithState_SaysPlayed()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", "Wave"));
            var cue = new AnimationCue(Emotion.Happy, 0.8f, "wave");

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), true);

            Assert.AreEqual("AnimationDirector: cue wave -> played", line);
        }

        [Test]
        public void Log_GestureWithoutState_SaysSkipped()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", string.Empty));
            var cue = new AnimationCue(Emotion.Happy, 0.8f, "wave");

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), false);

            Assert.AreEqual("AnimationDirector: cue wave -> no animator state, skipped", line);
            Assert.IsTrue(model.Decide(cue).GestureLacksState);
        }

        [Test]
        public void Log_NamedStateMissingOnTheController_SaysSkipped()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", "Wave"));
            var cue = new AnimationCue(Emotion.Happy, 0.8f, "wave");

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), false);

            Assert.AreEqual("AnimationDirector: cue wave -> no animator state, skipped", line);
        }

        [Test]
        public void Log_EmotionCue_SaysApplied()
        {
            var model = NewModel();
            var cue = new AnimationCue(Emotion.Happy, 0.35f, string.Empty);

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), false);

            Assert.AreEqual("AnimationDirector: emotion happy -> applied", line);
        }

        [Test]
        public void Log_EmotionId_SaysApplied()
        {
            var model = NewModel(Entry("happy", Emotion.Happy, 0.8f, string.Empty, "Happy"));
            var cue = new AnimationCue(Emotion.Neutral, 0.2f, "happy");

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), false);

            Assert.AreEqual("AnimationDirector: emotion happy -> applied", line);
        }

        [Test]
        public void Log_NoCue_SaysNeutral()
        {
            var model = NewModel();
            var cue = AnimationCueParser.NeutralCue;

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), false);

            Assert.AreEqual("AnimationDirector: no cue -> neutral", line);
        }

        [Test]
        public void Log_UnknownGesture_SaysNeutral()
        {
            var model = NewModel(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", "Wave"));
            var cue = new AnimationCue(Emotion.Happy, 1f, "moonwalk");

            var line = AnimationDirectorLog.Format(cue, model.Decide(cue), false);

            Assert.AreEqual("AnimationDirector: cue moonwalk -> unknown, neutral", line);
        }

        [Test]
        public void Log_DoesNotEchoReplyText()
        {
            var cue = new AnimationCue(Emotion.Happy, 0.8f, "Hello there, this is the whole reply");
            var decision = new DirectorDecision(DirectorAction.Neutral, string.Empty, Emotion.Neutral, 0.4f, string.Empty);

            var line = AnimationDirectorLog.Format(cue, decision, false);

            Assert.AreEqual("AnimationDirector: cue invalid -> skipped", line);
            StringAssert.DoesNotContain("Hello", line);
            StringAssert.DoesNotContain("reply", line);
        }

        [Test]
        public void SystemPromptSuffix_InstructsTheModelToEmitACueTag()
        {
            var host = new GameObject("director-prompt");
            try
            {
                var director = host.AddComponent<Director>();
                AssignCatalog(director, NewCatalog(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", string.Empty)));

                var prompt = director.SystemPromptSuffix;

                StringAssert.Contains(AnimationCueParser.Instruction, prompt);
                StringAssert.Contains("<animation emotion=\"happy\" intensity=\"0.8\" gesture=\"wave_hello\" />", prompt);
                StringAssert.Contains("End every reply with one cue tag on its own line", prompt);
                StringAssert.Contains("- wave_hello:", prompt);
                StringAssert.Contains("gesture: wave", prompt);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ShippedCatalog_SuffixAsksForTheCueTag()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimationClipCatalog>("Assets/_Project/Settings/AnimationCatalog.asset");
            Assert.IsNotNull(catalog);
            var host = new GameObject("shipped-director");
            try
            {
                var director = host.AddComponent<Director>();
                AssignCatalog(director, catalog);

                var prompt = director.SystemPromptSuffix;

                StringAssert.Contains("<animation emotion=\"happy\" intensity=\"0.8\" gesture=\"wave_hello\" />", prompt);
                StringAssert.Contains("End every reply with one cue tag on its own line", prompt);
                StringAssert.Contains("Leave the gesture attribute out for an emotion only", prompt);
                StringAssert.Contains("- wave_hello:", prompt);
                StringAssert.Contains("- happy:", prompt);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Play_EmotionCue_SetsTheFaceEmotionAndLogsApplied()
        {
            var host = new GameObject("director");
            try
            {
                var avatar = host.AddComponent<RecordingAvatar>();
                var director = host.AddComponent<Director>();
                AssignCatalog(director, NewCatalog());
                LogAssert.Expect(LogType.Log, "AnimationDirector: emotion happy -> applied");

                director.Play(new AnimationCue(Emotion.Happy, 0.35f, string.Empty));

                Assert.AreEqual(Emotion.Happy, avatar.Emotion);
                Assert.AreEqual(0.35f, avatar.EmotionIntensity, 0.0001f);
                Assert.AreEqual(0, avatar.PlayCalls);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Play_GestureWithoutState_SetsTheRowEmotionAndSkipsTheClip()
        {
            var host = new GameObject("director");
            try
            {
                var avatar = host.AddComponent<RecordingAvatar>();
                var director = host.AddComponent<Director>();
                AssignCatalog(director, NewCatalog(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", string.Empty)));
                LogAssert.Expect(LogType.Log, "AnimationDirector: cue wave -> no animator state, skipped");

                director.Play(new AnimationCue(Emotion.Surprised, 1f, "wave"));

                Assert.AreEqual(Emotion.Happy, avatar.Emotion);
                Assert.AreEqual(0.7f, avatar.EmotionIntensity, 0.0001f);
                Assert.AreEqual(0, avatar.PlayCalls);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Play_GestureState_LogsPlayedWhenTheControllerHasIt()
        {
            var host = new GameObject("director");
            try
            {
                var avatar = host.AddComponent<RecordingAvatar>();
                avatar.HasState = true;
                var director = host.AddComponent<Director>();
                AssignCatalog(director, NewCatalog(Entry("wave_hello", Emotion.Happy, 0.7f, "wave", "Wave")));
                LogAssert.Expect(LogType.Log, "AnimationDirector: cue wave -> played");

                director.Play(new AnimationCue(Emotion.Happy, 0.8f, "wave"));

                Assert.AreEqual(1, avatar.PlayCalls);
                Assert.AreEqual("Wave", avatar.LastState);
                Assert.AreEqual(Emotion.Happy, avatar.Emotion);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Play_NoCue_KeepsTheLastMood()
        {
            var host = new GameObject("director");
            try
            {
                var avatar = host.AddComponent<RecordingAvatar>();
                var director = host.AddComponent<Director>();
                AssignCatalog(director, NewCatalog());
                director.Play(new AnimationCue(Emotion.Happy, 0.8f, string.Empty));
                var before = avatar.Emotion;

                LogAssert.Expect(LogType.Log, "AnimationDirector: no cue -> kept");
                director.Play(AnimationCueParser.NeutralCue);

                Assert.AreEqual(before, avatar.Emotion);
                Assert.AreEqual(0, avatar.PlayCalls);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        AnimationClipCatalog NewCatalog(params AnimationEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(entries);
            _created.Add(catalog);
            return catalog;
        }

        AnimationDirectorModel NewModel(params AnimationEntry[] entries)
        {
            return new AnimationDirectorModel(NewCatalog(entries));
        }

        static void AssignCatalog(Director director, AnimationClipCatalog catalog)
        {
            typeof(Director)
                .GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, catalog);
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

    sealed class RecordingAvatar : MonoBehaviour, IAvatarAnimator, IAnimationStatePlayer
    {
        public bool IsTalking { get; private set; }
        public Emotion Emotion { get; private set; }
        public float EmotionIntensity { get; private set; }
        public int PlayCalls;
        public string LastState;
        public bool HasState = true;

        public void SetTalking(bool talking)
        {
            IsTalking = talking;
        }

        public void SetEmotion(Emotion emotion, float intensity)
        {
            Emotion = emotion;
            EmotionIntensity = intensity;
        }

        public bool TryPlayState(string stateName, float crossfadeSeconds)
        {
            PlayCalls++;
            LastState = stateName;
            return HasState;
        }
    }
}
