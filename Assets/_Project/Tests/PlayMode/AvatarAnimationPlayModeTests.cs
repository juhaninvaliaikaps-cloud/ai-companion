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

        [UnityTest]
        public IEnumerator YawTowardCamera_StillFacesTheCameraAfterAFewFrames()
        {
            var cameraObject = new GameObject("facing camera");
            cameraObject.transform.position = new Vector3(0f, 1f, -3.2f);
            var root = new GameObject("facing root");
            var yaw = AvatarFacing.YawDegreesToward(Vector3.forward, cameraObject.transform.position - root.transform.position);
            root.transform.Rotate(0f, yaw, 0f, Space.World);

            yield return null;
            yield return null;
            yield return null;

            var toCamera = cameraObject.transform.position - root.transform.position;
            toCamera.y = 0f;
            var forward = root.transform.forward;
            forward.y = 0f;
            var dot = Vector3.Dot(forward.normalized, toCamera.normalized);
            Object.Destroy(cameraObject);
            Object.Destroy(root);
            Assert.Greater(dot, 0.9f);
        }
    }
}
