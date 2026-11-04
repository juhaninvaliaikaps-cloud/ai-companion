using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class HandPullTests
    {
        static readonly Vector3 Shoulder = new Vector3(0.18f, 1.4f, 0f);
        const float Reach = 0.55f;

        [Test]
        public void PullingSidewaysPastTheReachWalksThatWay()
        {
            var right = Shoulder + new Vector3(Reach * 1.2f, 0f, 0f);
            Assert.AreEqual(1, HandPull.WalkDirection(right, Shoulder, Reach));

            var left = Shoulder + new Vector3(-Reach * 1.2f, 0f, 0f);
            Assert.AreEqual(-1, HandPull.WalkDirection(left, Shoulder, Reach));
        }

        [Test]
        public void AShortPullDoesNotStartAWalk()
        {
            var near = Shoulder + new Vector3(Reach * 0.5f, 0f, 0f);
            Assert.AreEqual(0, HandPull.WalkDirection(near, Shoulder, Reach));
        }

        [Test]
        public void PullingMainlyUpOrForwardDoesNotCountAsLeading()
        {
            var up = Shoulder + new Vector3(0f, Reach * 1.4f, 0f);
            Assert.AreEqual(0, HandPull.WalkDirection(up, Shoulder, Reach));

            var forward = Shoulder + new Vector3(0f, 0f, Reach * 1.4f);
            Assert.AreEqual(0, HandPull.WalkDirection(forward, Shoulder, Reach));
        }

        [Test]
        public void ADiagonalPullNeedsALateralShare()
        {
            // Mostly lateral: walks.
            var lateral = Shoulder + new Vector3(Reach * 1.1f, Reach * 0.4f, 0f);
            Assert.AreEqual(1, HandPull.WalkDirection(lateral, Shoulder, Reach));

            // Mostly vertical: does not.
            var vertical = Shoulder + new Vector3(Reach * 0.4f, Reach * 1.1f, 0f);
            Assert.AreEqual(0, HandPull.WalkDirection(vertical, Shoulder, Reach));
        }

        [Test]
        public void AZeroReachNeverWalks()
        {
            Assert.AreEqual(0, HandPull.WalkDirection(Shoulder + Vector3.right, Shoulder, 0f));
        }
    }
}
