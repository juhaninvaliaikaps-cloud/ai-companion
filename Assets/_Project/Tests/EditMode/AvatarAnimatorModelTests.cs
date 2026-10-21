using System;
using System.IO;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class AvatarAnimatorModelTests
    {
        [Test]
        public void EmotionValues_MatchTheControllerThresholds()
        {
            Assert.AreEqual(0, (int)Emotion.Neutral);
            Assert.AreEqual(1, (int)Emotion.Happy);
            Assert.AreEqual(2, (int)Emotion.Sad);
            Assert.AreEqual(3, (int)Emotion.Surprised);
            Assert.AreEqual(4, (int)Emotion.Thinking);
            Assert.AreEqual(AvatarAnimatorStates.Idle, AvatarAnimatorStates.ForEmotion(Emotion.Neutral));
            Assert.AreEqual(AvatarAnimatorStates.Happy, AvatarAnimatorStates.ForEmotion(Emotion.Happy));
            Assert.AreEqual(AvatarAnimatorStates.Sad, AvatarAnimatorStates.ForEmotion(Emotion.Sad));
            Assert.AreEqual(AvatarAnimatorStates.Surprised, AvatarAnimatorStates.ForEmotion(Emotion.Surprised));
            Assert.AreEqual(AvatarAnimatorStates.Thinking, AvatarAnimatorStates.ForEmotion(Emotion.Thinking));
        }

        [Test]
        public void SetEmotion_ClampsIntensityIntoTheParameterBlock()
        {
            var values = AvatarAnimatorParameterValues.From(true, Emotion.Sad, 2f);

            Assert.IsTrue(values.Talking);
            Assert.AreEqual(2, values.Emotion);
            Assert.AreEqual(1f, values.EmotionIntensity, 0.0001f);
            Assert.IsTrue(AvatarAnimatorRules.Matches(values, AvatarAnimatorParameters.Emotion, AvatarAnimatorRules.Equals, 2f));
            Assert.IsFalse(AvatarAnimatorRules.Matches(values, AvatarAnimatorParameters.Emotion, AvatarAnimatorRules.Equals, 1f));
            Assert.IsTrue(AvatarAnimatorRules.Matches(values, AvatarAnimatorParameters.Talking, AvatarAnimatorRules.If, 0f));
            Assert.IsFalse(AvatarAnimatorRules.Matches(values, AvatarAnimatorParameters.Talking, AvatarAnimatorRules.IfNot, 0f));
        }

        [Test]
        public void BlendProgress_ReachesTheTargetAtTheTransitionLength()
        {
            Assert.AreEqual(0f, AvatarAnimatorRules.BlendProgress(0f), 0.0001f);
            Assert.AreEqual(0.5f, AvatarAnimatorRules.BlendProgress(AvatarAnimatorRules.TransitionSeconds * 0.5f), 0.0001f);
            Assert.AreEqual(1f, AvatarAnimatorRules.BlendProgress(AvatarAnimatorRules.TransitionSeconds), 0.0001f);
            Assert.AreEqual(1f, AvatarAnimatorRules.BlendProgress(2f), 0.0001f);
        }

        [Test]
        public void DebugKeys_SelectEmotionsAndToggleTalkWithoutUsingF7OrF8()
        {
            var model = new AvatarAnimatorModel();
            model.SetEmotion(Emotion.Happy, 0.25f);

            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.Alpha2));
            Assert.AreEqual(Emotion.Happy, model.Emotion);
            Assert.AreEqual(1f, model.EmotionIntensity, 0.0001f);

            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.Keypad3));
            Assert.AreEqual(Emotion.Sad, model.Emotion);
            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.T));
            Assert.IsTrue(model.IsTalking);
            Assert.AreEqual(Emotion.Sad, model.Emotion);
            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.T));
            Assert.IsFalse(model.IsTalking);

            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.Alpha4));
            Assert.AreEqual(Emotion.Surprised, model.Emotion);
            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.Alpha5));
            Assert.AreEqual(Emotion.Thinking, model.Emotion);
            Assert.IsTrue(model.TryApplyDebugKey(KeyCode.Alpha1));
            Assert.AreEqual(Emotion.Neutral, model.Emotion);

            Assert.IsFalse(model.TryApplyDebugKey(KeyCode.F7));
            Assert.IsFalse(model.TryApplyDebugKey(KeyCode.F8));
            Assert.AreEqual(Emotion.Neutral, model.Emotion);
            StringAssert.DoesNotContain("F7", AvatarAnimatorModel.DebugHint);
            StringAssert.DoesNotContain("F8", AvatarAnimatorModel.DebugHint);
        }

        [Test]
        public void FormatLabel_ShowsEmotionIntensityAndTalk()
        {
            Assert.AreEqual("Happy 50%  Talk off", AvatarAnimatorModel.FormatLabel(Emotion.Happy, 0.5f, false));
            Assert.AreEqual("Sad 100%  Talk on", AvatarAnimatorModel.FormatLabel(Emotion.Sad, 4f, true));
        }

        [Test]
        public void ControllerAsset_WiresIdleTalkAndEmotionClips()
        {
            var root = Path.Combine(Application.dataPath, "_Project/Animations");
            var controller = File.ReadAllText(Path.Combine(root, "Companion.controller")).Replace("\r\n", "\n");

            StringAssert.Contains("m_Name: Talking", controller);
            StringAssert.Contains("m_Name: EmotionIntensity", controller);
            StringAssert.Contains("m_Name: Idle", controller);
            StringAssert.Contains("m_Name: Happy", controller);
            StringAssert.Contains("m_Name: Sad", controller);
            StringAssert.Contains("m_Name: Surprised", controller);
            StringAssert.Contains("m_Name: Thinking", controller);
            StringAssert.Contains("m_Name: Silent", controller);
            StringAssert.Contains("m_Name: Talk\n  m_Speed: 1", controller);
            StringAssert.Contains("m_DefaultState: {fileID: 110200001}", controller);
            StringAssert.Contains("m_TransitionDuration: 0.3", controller);
            StringAssert.Contains("m_HasExitTime: 0", controller);
            StringAssert.Contains("m_BlendingMode: 1", controller);
            StringAssert.Contains("m_DefaultWeight: 1", controller);
            Assert.IsFalse(controller.Contains("m_WriteDefaultValues: 1"));

            Assert.IsTrue(controller.Contains(MetaGuid(Path.Combine(root, "Idle.anim"))));
            Assert.IsTrue(controller.Contains(MetaGuid(Path.Combine(root, "Talk.anim"))));
            Assert.IsTrue(controller.Contains(MetaGuid(Path.Combine(root, "Happy.anim"))));
            Assert.IsTrue(controller.Contains(MetaGuid(Path.Combine(root, "Sad.anim"))));
            Assert.IsTrue(controller.Contains(MetaGuid(Path.Combine(root, "Surprised.anim"))));
            Assert.IsTrue(controller.Contains(MetaGuid(Path.Combine(root, "Thinking.anim"))));

            var idle = File.ReadAllText(Path.Combine(root, "Idle.anim"));
            StringAssert.Contains("m_LoopTime: 1", idle);
            StringAssert.Contains("attribute: Left Arm Down-Up", idle);
            StringAssert.Contains("attribute: Spine Front-Back", idle);

            var license = File.ReadAllText(Path.Combine(root, "LICENSE.txt"));
            StringAssert.Contains("CC0", license);
            StringAssert.Contains("not Mixamo", license);
        }

        static string MetaGuid(string assetPath)
        {
            var meta = File.ReadAllText(assetPath + ".meta");
            const string marker = "guid: ";
            var start = meta.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            start += marker.Length;
            var end = meta.IndexOf('\n', start);
            return meta.Substring(start, end - start).Trim();
        }
    }
}
