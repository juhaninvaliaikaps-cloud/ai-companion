using AICompanion.Avatar;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class FallStateTests
    {
        [Test]
        public void FallingParameterUsesTheControllerName()
        {
            Assert.AreEqual("Falling", AvatarAnimatorParameters.Falling);
        }

        [Test]
        public void FallStateNameIsDeclared()
        {
            Assert.AreEqual("Fall", AvatarAnimatorStates.Fall);
        }

        [Test]
        public void FallCountsAsGestureActivitySoTheSwayFades()
        {
            Assert.IsTrue(GestureActivity.IsGestureState(AvatarAnimatorStates.Fall));
        }

        [Test]
        public void EmptyIsNotGestureActivity()
        {
            Assert.IsFalse(GestureActivity.IsGestureState(AvatarAnimatorStates.Empty));
        }
    }
}
