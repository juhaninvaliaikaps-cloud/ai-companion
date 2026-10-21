using AICompanion.Avatar;
using AICompanion.Core;
using AICompanion.Overlay;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class AvatarFacingTests
    {
        [Test]
        public void YawDegreesToward_TurnsPlusZOntoTheOverlayCamera()
        {
            var toCamera = new Vector3(0f, 1f, -3.2f);
            Assert.AreEqual(180f, Mathf.Abs(AvatarFacing.YawDegreesToward(Vector3.forward, toCamera)), 0.01f);
            Assert.AreEqual(0f, AvatarFacing.YawDegreesToward(Vector3.back, toCamera), 0.01f);
            Assert.AreEqual(90f, AvatarFacing.YawDegreesToward(Vector3.forward, Vector3.right), 0.01f);
        }

        [Test]
        public void YawDegreesToward_LeavesADegenerateDirectionAlone()
        {
            Assert.AreEqual(0f, AvatarFacing.YawDegreesToward(Vector3.up, Vector3.back));
            Assert.AreEqual(0f, AvatarFacing.YawDegreesToward(Vector3.forward, Vector3.up));
            Assert.AreEqual(0f, AvatarFacing.YawDegreesToward(Vector3.zero, Vector3.back));
        }

        [Test]
        public void TryBodyForward_UsesHipsSpineAndUpperLegs()
        {
            Assert.IsTrue(AvatarFacing.TryBodyForward(
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1.2f, 0.02f),
                new Vector3(-0.12f, 0.9f, 0f),
                new Vector3(0.12f, 0.9f, 0f),
                out var ahead));
            Assert.Greater(Vector3.Dot(ahead, Vector3.forward), 0.99f);

            Assert.IsTrue(AvatarFacing.TryBodyForward(
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1.2f, 0f),
                new Vector3(0.12f, 0.9f, 0f),
                new Vector3(-0.12f, 0.9f, 0f),
                out var towardCamera));
            Assert.Greater(Vector3.Dot(towardCamera, Vector3.back), 0.99f);
            Assert.AreEqual(0f, AvatarFacing.YawDegreesToward(towardCamera, new Vector3(0f, 1f, -3.2f)), 0.01f);

            Assert.IsFalse(AvatarFacing.TryBodyForward(Vector3.zero, Vector3.up, Vector3.zero, Vector3.zero, out _));
        }

        [Test]
        public void ApplyDrag_AddsHorizontalPixelsAtAFixedRate()
        {
            Assert.AreEqual(0.5f, AvatarFacing.DegreesPerPixel);
            Assert.AreEqual(5f, AvatarFacing.DragDelta(10f));
            Assert.AreEqual(17f, AvatarFacing.ApplyDrag(12f, 10f));
            Assert.AreEqual(10f, AvatarFacing.ApplyDrag(12f, -4f));
            Assert.AreEqual(-170f, AvatarFacing.ApplyDrag(170f, 40f), 0.001f);
            Assert.AreEqual(170f, AvatarFacing.ApplyDrag(-170f, -40f), 0.001f);
            Assert.AreEqual(0f, AvatarFacing.ApplyDrag(0f, 720f), 0.001f);
        }

        [Test]
        public void YawKeys_AreDistinctPerAvatarSource()
        {
            Assert.IsTrue(AvatarYawKeys.TryKey(AvatarPreference.LocalPrefab, out var local));
            Assert.IsTrue(AvatarYawKeys.TryKey(AvatarPreference.VrmSample, out var vrm));
            Assert.IsTrue(AvatarYawKeys.TryKey(AvatarPreference.FbxSample, out var fbx));

            Assert.AreEqual("ai-companion.avatar-yaw.LocalPrefab", local);
            Assert.AreEqual("ai-companion.avatar-yaw.VrmSample", vrm);
            Assert.AreEqual("ai-companion.avatar-yaw.FbxSample", fbx);
            Assert.AreNotEqual(local, vrm);
            Assert.AreNotEqual(vrm, fbx);
            Assert.AreNotEqual(local, fbx);

            Assert.IsFalse(AvatarYawKeys.TryKey(AvatarPreference.Auto, out var ignored));
            Assert.IsNull(ignored);
        }

        [Test]
        public void RotateMode_TogglesOnlyOnASilhouetteRightClickAndEscapeExits()
        {
            Assert.IsTrue(AvatarRotateMode.ShouldToggle(true, true, false));
            Assert.IsFalse(AvatarRotateMode.ShouldToggle(true, false, false));
            Assert.IsFalse(AvatarRotateMode.ShouldToggle(false, true, false));
            Assert.IsFalse(AvatarRotateMode.ShouldToggle(true, true, true));

            Assert.IsTrue(AvatarRotateMode.Next(false, true, false));
            Assert.IsFalse(AvatarRotateMode.Next(true, true, false));
            Assert.IsTrue(AvatarRotateMode.Next(true, false, false));
            Assert.IsFalse(AvatarRotateMode.Next(true, false, true));
            Assert.IsFalse(AvatarRotateMode.Next(false, true, true));

            Assert.AreEqual("Rotate: on (right-click to finish)", AvatarRotateMode.Format(true));
            Assert.AreEqual("Rotate: off", AvatarRotateMode.Format(false));
        }

        [Test]
        public void RotateMode_ResetsFromMiddleClickOrRWhileActive()
        {
            Assert.IsTrue(AvatarRotateMode.ShouldReset(true, true, false, false, false));
            Assert.IsFalse(AvatarRotateMode.ShouldReset(false, true, false, true, false));
            Assert.IsTrue(AvatarRotateMode.ShouldReset(false, false, true, true, false));
            Assert.IsTrue(AvatarRotateMode.ShouldReset(true, false, true, true, false));
            Assert.IsFalse(AvatarRotateMode.ShouldReset(true, false, true, false, false));
            Assert.IsFalse(AvatarRotateMode.ShouldReset(true, false, true, true, true));
            Assert.IsFalse(AvatarRotateMode.ShouldReset(false, false, false, true, false));
        }

        [Test]
        public void RotateMode_LeavesWindowDragAloneUntilItIsActiveOnTheSilhouette()
        {
            Assert.IsFalse(AvatarRotateMode.ShouldRotateInsteadOfMovingWindow(false, true, false));
            Assert.IsFalse(AvatarRotateMode.ShouldRotateInsteadOfMovingWindow(true, false, false));
            Assert.IsFalse(AvatarRotateMode.ShouldRotateInsteadOfMovingWindow(true, true, true));
            Assert.IsTrue(AvatarRotateMode.ShouldRotateInsteadOfMovingWindow(true, true, false));
        }
    }
}
