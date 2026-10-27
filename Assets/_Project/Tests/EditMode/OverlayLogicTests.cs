using System.Reflection;
using System.Text.RegularExpressions;
using AICompanion.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AICompanion.Tests.EditMode
{
    public class OverlayLogicTests
    {
        [Test]
        public void ShouldClickThrough_WhenNothingIsUnderTheCursor()
        {
            Assert.IsTrue(ClickThroughDecision.ShouldClickThrough(false, false, false));
        }

        [Test]
        public void ShouldClickThrough_IsFalseWhenAnySignalHits()
        {
            Assert.IsFalse(ClickThroughDecision.ShouldClickThrough(true, false, false));
            Assert.IsFalse(ClickThroughDecision.ShouldClickThrough(false, true, false));
            Assert.IsFalse(ClickThroughDecision.ShouldClickThrough(false, false, true));
            Assert.IsFalse(ClickThroughDecision.ShouldClickThrough(true, true, true));
        }

        [Test]
        public void ShouldClickThrough_NullQueryPassesClicksThrough()
        {
            Assert.IsTrue(ClickThroughDecision.ShouldClickThrough(null));
        }

        [Test]
        public void ShouldClickThrough_ReadsTheQuery()
        {
            Assert.IsFalse(ClickThroughDecision.ShouldClickThrough(new FakeHit(true, false, false)));
            Assert.IsTrue(ClickThroughDecision.ShouldClickThrough(new FakeHit(false, false, false)));
        }

        [Test]
        public void IsOpaque_UsesAnInclusiveThreshold()
        {
            Assert.IsFalse(ClickThroughDecision.IsOpaque(0f));
            Assert.IsFalse(ClickThroughDecision.IsOpaque(0.079f));
            Assert.IsTrue(ClickThroughDecision.IsOpaque(0.08f));
            Assert.IsTrue(ClickThroughDecision.IsOpaque(1f));
            Assert.IsFalse(ClickThroughDecision.IsOpaque(0.2f, 0.5f));
            Assert.IsTrue(ClickThroughDecision.IsOpaque(0.5f, 0.5f));
        }

        [Test]
        public void FrameRate_StaysAt60UntilIdleThenDrops()
        {
            Assert.AreEqual(60, OverlayFrameRate.Choose(0f));
            Assert.AreEqual(60, OverlayFrameRate.Choose(2.99f));
            Assert.AreEqual(15, OverlayFrameRate.Choose(3f));
            Assert.AreEqual(15, OverlayFrameRate.Choose(30f));
        }

        [Test]
        public void WindowDrag_AddsTheCursorDeltaToTheWindowOrigin()
        {
            var drag = new WindowDrag(new Vector2Int(100, 200), new Vector2Int(5, 5));

            Assert.AreEqual(new Vector2Int(110, 220), drag.PositionFor(new Vector2Int(15, 25)));
            Assert.AreEqual(new Vector2Int(100, 200), drag.PositionFor(new Vector2Int(5, 5)));
        }

        [Test]
        public void ToUnityScreen_FlipsYAndScalesToTheUnityBuffer()
        {
            var same = OverlayPointerMath.ToUnityScreen(10, 20, 100, 200, 100, 200);
            Assert.AreEqual(10f, same.x);
            Assert.AreEqual(180f, same.y);

            var scaled = OverlayPointerMath.ToUnityScreen(10, 20, 100, 200, 200, 400);
            Assert.AreEqual(20f, scaled.x);
            Assert.AreEqual(360f, scaled.y);
            Assert.AreEqual(Vector2.zero, OverlayPointerMath.ToUnityScreen(10, 20, 0, 200, 100, 200));
        }

        [Test]
        public void IsInsideClient_RejectsTheFarEdges()
        {
            Assert.IsTrue(OverlayPointerMath.IsInsideClient(0, 0, 100, 50));
            Assert.IsTrue(OverlayPointerMath.IsInsideClient(99, 49, 100, 50));
            Assert.IsFalse(OverlayPointerMath.IsInsideClient(100, 0, 100, 50));
            Assert.IsFalse(OverlayPointerMath.IsInsideClient(0, 50, 100, 50));
            Assert.IsFalse(OverlayPointerMath.IsInsideClient(-1, 0, 100, 50));
        }

        [Test]
        public void IsInsideTopLeftRect_MapsGuiSpaceOntoUnityScreenSpace()
        {
            var panel = new Rect(8f, 8f, 10f, 10f);

            Assert.IsTrue(OverlayPointerMath.IsInsideTopLeftRect(panel, new Vector2(8f, 82f), 100f));
            Assert.IsTrue(OverlayPointerMath.IsInsideTopLeftRect(panel, new Vector2(17.9f, 91.9f), 100f));
            Assert.IsFalse(OverlayPointerMath.IsInsideTopLeftRect(panel, new Vector2(8f, 92f), 100f));
            Assert.IsFalse(OverlayPointerMath.IsInsideTopLeftRect(panel, new Vector2(18f, 85f), 100f));
            Assert.IsFalse(OverlayPointerMath.IsInsideTopLeftRect(panel, new Vector2(8f, 81.9f), 100f));
        }

        [Test]
        public void NextOpaque_EngagesAtTheThresholdAndReleasesLower()
        {
            Assert.IsFalse(SilhouetteHit.NextOpaque(false, 0.079f));
            Assert.IsTrue(SilhouetteHit.NextOpaque(false, 0.08f));
            Assert.IsTrue(SilhouetteHit.NextOpaque(true, 0.04f));
            Assert.IsTrue(SilhouetteHit.NextOpaque(true, 0.05f));
            Assert.IsFalse(SilhouetteHit.NextOpaque(true, 0.039f));
            Assert.IsFalse(SilhouetteHit.NextOpaque(true, 0f));
        }

        [Test]
        public void NextOpaque_ReleaseCannotSitAboveEngage()
        {
            Assert.IsFalse(SilhouetteHit.NextOpaque(true, 0.1f, 0.2f, 0.5f));
            Assert.IsTrue(SilhouetteHit.NextOpaque(true, 0.2f, 0.2f, 0.5f));
        }

        [Test]
        public void WithinSlack_KeepsAShortPixelCache()
        {
            var origin = new Vector2Int(40, 50);

            Assert.IsTrue(SilhouetteHit.WithinSlack(origin, new Vector2Int(40, 50), 3));
            Assert.IsTrue(SilhouetteHit.WithinSlack(origin, new Vector2Int(43, 50), 3));
            Assert.IsFalse(SilhouetteHit.WithinSlack(origin, new Vector2Int(44, 50), 3));
            Assert.IsFalse(SilhouetteHit.WithinSlack(origin, new Vector2Int(0, 0), 3));
        }

        [Test]
        public void ShouldSample_OnlyWhenInsideMovedAndIdle()
        {
            Assert.IsTrue(SilhouetteHit.ShouldSample(true, true, false));
            Assert.IsFalse(SilhouetteHit.ShouldSample(false, true, false));
            Assert.IsFalse(SilhouetteHit.ShouldSample(true, false, false));
            Assert.IsFalse(SilhouetteHit.ShouldSample(true, true, true));
        }

        [Test]
        public void SilhouetteClickThrough_UsesThePixelAndKeepsADrag()
        {
            Assert.IsTrue(SilhouetteHit.ShouldClickThrough(false, false, false));
            Assert.IsFalse(SilhouetteHit.ShouldClickThrough(false, true, false));
            Assert.IsFalse(SilhouetteHit.ShouldClickThrough(true, false, false));
            Assert.IsFalse(SilhouetteHit.ShouldClickThrough(false, false, true));
        }

        [Test]
        public void WindowDrag_DoesNotStartOnAnotherUiPanel()
        {
            Assert.IsFalse(WindowDragGate.ShouldStart(false, true, true, false));
            Assert.IsFalse(WindowDragGate.ShouldStart(true, true, true, true));
            Assert.IsTrue(WindowDragGate.ShouldStart(true, true, false, false));
            Assert.IsTrue(WindowDragGate.ShouldStart(true, false, true, false));
            Assert.IsFalse(WindowDragGate.ShouldStart(true, false, false, false));
        }

        [Test]
        public void AvatarDoubleClick_RequiresAShortGap()
        {
            Assert.IsFalse(AvatarDoubleClick.IsSecond(-1f, 1f));
            Assert.IsFalse(AvatarDoubleClick.IsSecond(0f, 0.02f));
            Assert.IsTrue(AvatarDoubleClick.IsSecond(0f, 0.2f));
            Assert.IsTrue(AvatarDoubleClick.IsSecond(0f, AvatarDoubleClick.MaxGapSeconds));
            Assert.IsFalse(AvatarDoubleClick.IsSecond(0f, AvatarDoubleClick.MaxGapSeconds + 0.05f));
        }

        [Test]
        public void ApplyTransparentCamera_ClearsToTransparentBlackWithoutHdrOrMsaa()
        {
            var host = new GameObject("overlay camera");
            var camera = host.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Color.blue;
            camera.allowHDR = true;
            camera.allowMSAA = true;

            try
            {
                OverlayWindow.ApplyTransparentCamera(camera);

                Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
                Assert.AreEqual(0f, camera.backgroundColor.r);
                Assert.AreEqual(0f, camera.backgroundColor.g);
                Assert.AreEqual(0f, camera.backgroundColor.b);
                Assert.AreEqual(0f, camera.backgroundColor.a);
                Assert.IsFalse(camera.allowHDR);
                Assert.IsFalse(camera.allowMSAA);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LogCamera_StaysOffUntilEnabledAndIgnoresShownZoom()
        {
            var host = new GameObject("frame");
            var cameraObject = new GameObject("frame camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var frame = host.AddComponent<AvatarCameraFrame>();
                frame.Attach(camera);
                var log = typeof(AvatarCameraFrame).GetMethod("LogCamera", BindingFlags.Instance | BindingFlags.NonPublic);
                var shown = typeof(AvatarCameraFrame).GetField("_shownZoom", BindingFlags.Instance | BindingFlags.NonPublic);
                var toggle = typeof(AvatarCameraFrame).GetField("logCamera", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsFalse((bool)toggle.GetValue(frame));
                log.Invoke(frame, new object[] { 4 });
                shown.SetValue(frame, 0.85f);
                log.Invoke(frame, new object[] { 4 });

                toggle.SetValue(frame, true);
                LogAssert.Expect(LogType.Log, new Regex("^Camera: zoom="));
                log.Invoke(frame, new object[] { 4 });
                shown.SetValue(frame, 0.15f);
                log.Invoke(frame, new object[] { 4 });

                LogAssert.Expect(LogType.Log, new Regex("^Camera: zoom="));
                frame.SetZoom(0.4f);
                log.Invoke(frame, new object[] { 4 });
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
            }
        }

        sealed class FakeHit : IOverlayHitQuery
        {
            public FakeHit(bool overCollider, bool overUi, bool opaquePixel)
            {
                OverCollider = overCollider;
                OverUi = overUi;
                OpaquePixel = opaquePixel;
            }

            public bool OverCollider { get; }
            public bool OverUi { get; }
            public bool OpaquePixel { get; }
        }
    }
}
