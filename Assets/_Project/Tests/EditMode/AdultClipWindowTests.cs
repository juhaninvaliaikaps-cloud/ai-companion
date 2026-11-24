using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-189: the adult clip window stays on the overlay camera and ignores a stale prepare.</summary>
    public sealed class AdultClipWindowTests
    {
        GameObject _cameraObject;
        GameObject _canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);
        }

        [Test]
        public void NormalOverlay_ShowsTheClipAt480()
        {
            Assert.AreEqual(480f, AdultClipWindow.PanelEdge(480f, 720f));
        }

        [Test]
        public void LargeOverlay_ShowsTheClipAtItsNative544()
        {
            Assert.AreEqual(544f, AdultClipWindow.PanelEdge(960f, 1440f));
            Assert.AreEqual(544f, AdultClipWindow.PanelEdge(1200f, 800f));
        }

        [Test]
        public void StalePrepare_DoesNotCountAsTheCurrentClip()
        {
            Assert.IsTrue(AdultClipWindow.AcceptsPrepared(2, 2));
            Assert.IsFalse(AdultClipWindow.AcceptsPrepared(1, 2));
            Assert.IsFalse(AdultClipWindow.AcceptsPrepared(0, 0));
        }

        [Test]
        public void Canvas_UsesTheOverlayCamera()
        {
            _cameraObject = new GameObject("adult-clip-camera");
            var camera = _cameraObject.AddComponent<Camera>();
            _canvasObject = new GameObject("adult-clip-canvas");
            var canvas = _canvasObject.AddComponent<Canvas>();

            AdultClipWindow.PlaceOnOverlay(canvas, camera);

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.AreSame(camera, canvas.worldCamera);
            Assert.Greater(canvas.planeDistance, 0.55f);
            Assert.Less(canvas.planeDistance, 0.6f);
        }

        [Test]
        public void Canvas_StaysAnOverlayWhenThereIsNoCamera()
        {
            _canvasObject = new GameObject("adult-clip-canvas");
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            AdultClipWindow.PlaceOnOverlay(canvas, null);

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        }
    }
}
