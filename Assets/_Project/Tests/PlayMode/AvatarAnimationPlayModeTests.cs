using System.Collections;
using AICompanion.Avatar;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AICompanion.Tests.PlayMode
{
    public class AvatarAnimationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SetEmotionAndTalking_LastAcrossAFrameWithoutAModel()
        {
            var host = new GameObject("animator test");
            var animator = host.AddComponent<AvatarAnimationController>();
            animator.SetEmotion(Emotion.Surprised, 0.4f);
            animator.SetTalking(true);

            yield return null;

            Assert.AreEqual(Emotion.Surprised, animator.Emotion);
            Assert.AreEqual(0.4f, animator.EmotionIntensity, 0.0001f);
            Assert.IsTrue(animator.IsTalking);
            Assert.AreEqual("Surprised 40%  Talk on", AvatarAnimatorModel.FormatLabel(animator.Emotion, animator.EmotionIntensity, animator.IsTalking));

            Object.Destroy(host);
        }
    }
}
