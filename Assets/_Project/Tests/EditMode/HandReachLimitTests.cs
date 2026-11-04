using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class HandReachLimitTests
    {
        static HandReachLimit.Bounds Probe(out Vector3 shoulder)
        {
            shoulder = new Vector3(0.2f, 1.4f, 0f);
            var lower = new Vector3(0.2f, 1.15f, 0f);
            var hand = new Vector3(0.2f, 0.9f, 0f);
            var head = new Vector3(0f, 1.65f, 0f);
            var hips = new Vector3(0f, 1f, 0f);
            return HandReachLimit.ForShoulder(shoulder, lower, hand, head, hips);
        }

        [Test]
        public void APointInsideTheReachIsUnchanged()
        {
            Vector3 shoulder;
            var bounds = Probe(out shoulder);

            var point = shoulder + new Vector3(0.1f, -0.2f, 0.1f);
            var clamped = HandReachLimit.Clamp(point, shoulder, bounds);

            Assert.AreEqual(point.x, clamped.x, 0.0001f);
            Assert.AreEqual(point.y, clamped.y, 0.0001f);
            Assert.AreEqual(point.z, clamped.z, 0.0001f);
        }

        [Test]
        public void TheArmCannotStretchPastItsLength()
        {
            Vector3 shoulder;
            var bounds = Probe(out shoulder);

            var far = shoulder + new Vector3(3f, 0f, 0f);
            var clamped = HandReachLimit.Clamp(far, shoulder, bounds);

            Assert.LessOrEqual(Vector3.Distance(clamped, shoulder), bounds.MaxReach + 0.0001f);
        }

        [Test]
        public void TheHandCannotGoBehindTheShoulder()
        {
            Vector3 shoulder;
            var bounds = Probe(out shoulder);

            var behind = shoulder + new Vector3(0f, 0f, -0.8f);
            var clamped = HandReachLimit.Clamp(behind, shoulder, bounds);

            Assert.GreaterOrEqual(clamped.z, bounds.MinZ - 0.0001f);
        }

        [Test]
        public void TheHandCannotGoAboveTheHeadOrBelowTheHips()
        {
            Vector3 shoulder;
            var bounds = Probe(out shoulder);

            var above = HandReachLimit.Clamp(shoulder + new Vector3(0f, 2f, 0f), shoulder, bounds);
            Assert.LessOrEqual(above.y, bounds.MaxY + 0.0001f);

            var below = HandReachLimit.Clamp(shoulder + new Vector3(0f, -2f, 0f), shoulder, bounds);
            Assert.GreaterOrEqual(below.y, bounds.MinY - 0.0001f);
        }

        [Test]
        public void TheHandCannotCrossFarPastTheShoulderSideways()
        {
            Vector3 shoulder;
            var bounds = Probe(out shoulder);

            var far = HandReachLimit.Clamp(shoulder + new Vector3(2f, 0f, 0f), shoulder, bounds);
            Assert.LessOrEqual(far.x, shoulder.x + bounds.MaxSide + 0.0001f);

            var cross = HandReachLimit.Clamp(shoulder + new Vector3(-2f, 0f, 0f), shoulder, bounds);
            Assert.GreaterOrEqual(cross.x, shoulder.x - bounds.MaxSide - 0.0001f);
        }
    }
}
