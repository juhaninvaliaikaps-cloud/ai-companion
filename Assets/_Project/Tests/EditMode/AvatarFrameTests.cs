using AICompanion.Overlay;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class AvatarFrameTests
    {
        const float Fov = 60f;
        const float Aspect = 480f / 720f;
        const float Near = 0.3f;

        [Test]
        public void Fit_EmptyPoints_UsesTheScenePose()
        {
            var pose = AvatarFrameMath.Fit(null, 0, Fov, Aspect, Near);

            Assert.AreEqual(0f, pose.X, 0.0001f);
            Assert.AreEqual(1f, pose.Y, 0.0001f);
            Assert.AreEqual(-3.2f, pose.Z, 0.0001f);
        }

        [Test]
        public void Fit_PortraitWindow_KeepsHeadHandsAndToesInsideWithRoomUnderTheFeet()
        {
            var body = ForwardBody(0.45f);
            var pose = AvatarFrameMath.Fit(body, body.Length, Fov, Aspect, Near);

            AssertInside(pose, body, 0.93f);
            var leftToe = Project(pose, body[2]);
            var rightToe = Project(pose, body[3]);
            Assert.Greater(leftToe.y, -0.86f);
            Assert.Greater(rightToe.y, -0.86f);
            Assert.Less(leftToe.y, -0.5f);
            Assert.Less(pose.Z, -1f);
            Assert.Greater(body[2].z - pose.Z, Near + 0.05f);
        }

        [Test]
        public void Fit_BottomPadding_LiftsTheToesOffTheFrameEdge()
        {
            var body = ForwardBody(0.45f);
            var padded = AvatarFrameMath.Fit(body, body.Length, Fov, Aspect, Near, bottomPadding: 0.08f);
            var tight = AvatarFrameMath.Fit(body, body.Length, Fov, Aspect, Near, bottomPadding: 0f);
            var toe = body[2];

            Assert.Greater(Project(padded, toe).y, Project(tight, toe).y + 0.05f);
            Assert.Less(Mathf.Abs(Project(tight, toe).y), 0.93f);
        }

        [Test]
        public void Fit_WideArms_PullTheCameraBackAndStayInside()
        {
            var narrow = ForwardBody(0.2f);
            var wide = ForwardBody(0.8f);
            var narrowPose = AvatarFrameMath.Fit(narrow, narrow.Length, Fov, Aspect, Near);
            var widePose = AvatarFrameMath.Fit(wide, wide.Length, Fov, Aspect, Near);

            Assert.Less(widePose.Z, narrowPose.Z);
            AssertInside(widePose, wide, 0.93f);
        }

        [Test]
        public void Fit_Yaw_KeepsEveryPointInsideThePortraitFrame()
        {
            var body = ForwardBody(0.45f);
            for (var degrees = 0; degrees < 360; degrees += 15)
            {
                var yawed = Yaw(body, degrees);
                var pose = AvatarFrameMath.Fit(yawed, yawed.Length, Fov, Aspect, Near);
                AssertInside(pose, yawed, 0.93f);
            }
        }

        [Test]
        public void ZoomToward_ZeroMatchesFullBody_AndCannotPullBackPastIt()
        {
            var full = AvatarFrameMath.Fit(ForwardBody(0.45f), 6, Fov, Aspect, Near);
            var pivot = new Vector3(0.2f, 1.1f, -0.05f);

            var atZero = AvatarFrameMath.ZoomToward(full, pivot, 0f);
            var negative = AvatarFrameMath.ZoomToward(full, pivot, -1f);
            AssertPose(full, atZero);
            AssertPose(full, negative);

            var past = AvatarFrameMath.ZoomToward(full, pivot, 2f);
            var atOne = AvatarFrameMath.ZoomToward(full, pivot, 1f);
            AssertPose(atOne, past);

            float fullDistance = Vector3.Distance(full.Position, pivot);
            float zoomedDistance = Vector3.Distance(atOne.Position, pivot);
            float expected = Mathf.Max(AvatarFrameMath.DefaultMinZoomFraction * fullDistance, AvatarFrameMath.ZoomNearFloor);
            Assert.AreEqual(expected, zoomedDistance, 0.001f);
            Assert.Less(zoomedDistance, fullDistance);
        }

        [Test]
        public void ZoomToward_KeepsThePivotNdcStable()
        {
            var full = AvatarFrameMath.Fit(ForwardBody(0.45f), 6, Fov, Aspect, Near);
            var pivot = new Vector3(0.2f, 1.1f, -0.05f);
            var start = AvatarFrameMath.ProjectNdc(full, pivot, Fov, Aspect);

            foreach (var zoom in new[] { 0.25f, 0.7f, 1f })
            {
                var pose = AvatarFrameMath.ZoomToward(full, pivot, zoom);
                var ndc = AvatarFrameMath.ProjectNdc(pose, pivot, Fov, Aspect);
                Assert.AreEqual(start.x, ndc.x, 0.001f);
                Assert.AreEqual(start.y, ndc.y, 0.001f);
                Assert.Greater(pivot.z - pose.Z, 0.2f);
            }
        }

        [Test]
        public void ZoomToward_NearFloorDoesNotPushPastTheFullBodyPose()
        {
            var pivot = new Vector3(0f, 1f, 0f);
            var close = new CameraPose(0f, 1f, -0.4f);
            var zoomed = AvatarFrameMath.ZoomToward(close, pivot, 1f);

            Assert.AreEqual(AvatarFrameMath.ZoomNearFloor, Vector3.Distance(zoomed.Position, pivot), 0.001f);
            Assert.AreEqual(-AvatarFrameMath.ZoomNearFloor, zoomed.Z, 0.001f);

            var alreadyClose = new CameraPose(0f, 1f, -0.2f);
            var held = AvatarFrameMath.ZoomToward(alreadyClose, pivot, 1f);
            AssertPose(alreadyClose, held);
        }

        [Test]
        public void PlaceAnchored_DefaultBottomRightOn1080p()
        {
            OverlayWindowSize.SizeFor(1f, out var width, out var height);
            Assert.AreEqual(OverlayWindowSize.BaseWidth, width);
            Assert.AreEqual(OverlayWindowSize.BaseHeight, height);

            int right = 1920 - OverlayWindowSize.EdgeMargin;
            int bottom = 1080 - OverlayWindowSize.EdgeMargin;
            OverlayWindowSize.PlaceAnchored(right, bottom, 0, 0, 1920, 1080, width, height, out var x, out var y);

            Assert.AreEqual(1424, x);
            Assert.AreEqual(344, y);
            Assert.AreEqual(1904, x + width);
            Assert.AreEqual(1064, y + height);
        }

        [Test]
        public void PlaceAnchored_StaysAboveTheTaskbar()
        {
            OverlayWindowSize.SizeFor(1f, out var width, out var height);
            int right = 1920 - OverlayWindowSize.EdgeMargin;
            int bottom = 1032 - OverlayWindowSize.EdgeMargin;
            OverlayWindowSize.PlaceAnchored(right, bottom, 0, 0, 1920, 1032, width, height, out var x, out var y);

            Assert.AreEqual(296, y);
            Assert.AreEqual(1016, y + height);
            Assert.LessOrEqual(y + height, 1032);
            Assert.AreEqual(1424, x);
        }

        [Test]
        public void ClampScale_RespectsMinMaxAndTheWorkArea()
        {
            Assert.AreEqual(1f, OverlayWindowSize.ClampScale(1f, 1920, 1080), 0.0001f);
            Assert.AreEqual(OverlayWindowSize.MinScale, OverlayWindowSize.ClampScale(0.1f, 1920, 1080), 0.0001f);

            float fit = (1080 - OverlayWindowSize.EdgeMargin * 2) / (float)OverlayWindowSize.BaseHeight;
            Assert.AreEqual(fit, OverlayWindowSize.ClampScale(3f, 1920, 1080), 0.0001f);
            Assert.Less(fit, OverlayWindowSize.MaxScale);

            OverlayWindowSize.SizeFor(fit, out var width, out var height);
            Assert.LessOrEqual(width, 1920 - OverlayWindowSize.EdgeMargin * 2);
            Assert.LessOrEqual(height, 1080 - OverlayWindowSize.EdgeMargin * 2);
            Assert.Greater(height, OverlayWindowSize.BaseHeight);
        }

        [Test]
        public void ClampScale_ShrinksBelowTheMinimumWhenTheWorkAreaIsSmaller()
        {
            float scale = OverlayWindowSize.ClampScale(1f, 200, 300);
            Assert.Less(scale, OverlayWindowSize.MinScale);

            OverlayWindowSize.SizeFor(scale, out var width, out var height);
            Assert.LessOrEqual(width, 200 - OverlayWindowSize.EdgeMargin * 2);
            Assert.LessOrEqual(height, 300 - OverlayWindowSize.EdgeMargin * 2);
            Assert.Greater(width, 1);
            Assert.Greater(height, 1);
        }

        [Test]
        public void NextZoomAndNextScale_ClampTheWheel()
        {
            Assert.AreEqual(0.15f, OverlayWindowSize.NextZoom(0f, 1f), 0.0001f);
            Assert.AreEqual(0f, OverlayWindowSize.NextZoom(0f, -1f), 0.0001f);
            Assert.AreEqual(1f, OverlayWindowSize.NextZoom(1f, 4f), 0.0001f);
            Assert.AreEqual(Mathf.Exp(0.12f), OverlayWindowSize.NextScale(1f, 1f, 1920, 1080), 0.0001f);

            float ceiling = OverlayWindowSize.ClampScale(3f, 1920, 1080);
            Assert.AreEqual(ceiling, OverlayWindowSize.NextScale(1.4f, 5f, 1920, 1080), 0.0001f);
            Assert.AreEqual(OverlayWindowSize.MinScale, OverlayWindowSize.NextScale(0.6f, -5f, 1920, 1080), 0.0001f);
        }

        [Test]
        public void CursorCanAdjust_MatchesTheSilhouetteHitAndIgnoresUi()
        {
            Assert.IsTrue(OverlayWindowSize.CursorCanAdjust(true, false));
            Assert.IsFalse(OverlayWindowSize.CursorCanAdjust(false, false));
            Assert.IsFalse(OverlayWindowSize.CursorCanAdjust(true, true));
            Assert.IsFalse(OverlayWindowSize.CursorCanAdjust(false, true));
            Assert.AreEqual(
                !SilhouetteHit.ShouldClickThrough(false, true, false),
                OverlayWindowSize.CursorCanAdjust(true, false));
        }

        static Vector3[] ForwardBody(float handX)
        {
            return new[]
            {
                new Vector3(0f, 1.7f, 0f),
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0.1f, 0.02f, -0.25f),
                new Vector3(-0.1f, 0.02f, -0.25f),
                new Vector3(handX, 1f, 0f),
                new Vector3(-handX, 1f, 0f),
            };
        }

        static Vector3[] Yaw(Vector3[] points, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            var yawed = new Vector3[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var p = points[i];
                yawed[i] = new Vector3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
            }

            return yawed;
        }

        static Vector2 Project(CameraPose pose, Vector3 point)
        {
            return AvatarFrameMath.ProjectNdc(pose, point, Fov, Aspect);
        }

        static void AssertInside(CameraPose pose, Vector3[] points, float limit)
        {
            Assert.IsTrue(AvatarFrameMath.Contains(pose, points, points.Length, Fov, Aspect, Near, limit));
            for (var i = 0; i < points.Length; i++)
            {
                var ndc = Project(pose, points[i]);
                Assert.LessOrEqual(Mathf.Abs(ndc.x), limit);
                Assert.LessOrEqual(Mathf.Abs(ndc.y), limit);
                Assert.Greater(points[i].z - pose.Z, Near);
            }
        }

        static void AssertPose(CameraPose expected, CameraPose actual)
        {
            Assert.AreEqual(expected.X, actual.X, 0.0001f);
            Assert.AreEqual(expected.Y, actual.Y, 0.0001f);
            Assert.AreEqual(expected.Z, actual.Z, 0.0001f);
        }
    }
}
