using AICompanion.NewUI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonKeyboardTests
    {
        [Test]
        public void MainNavigationWrapsAndSkipsDisabledLookNow()
        {
            var keys = new RibbonKeyboard();
            keys.Open();

            keys.Move(1, 6, 0, false);
            Assert.AreEqual(2, keys.Row);
            keys.Move(-1, 6, 0, false);
            Assert.AreEqual(0, keys.Row);
            keys.Move(-1, 6, 0, false);
            Assert.AreEqual(5, keys.Row);
            keys.Move(1, 6, 0, true);
            keys.Move(1, 6, 0, true);
            Assert.AreEqual(1, keys.Row);
        }

        [Test]
        public void TakingOverFromHoverStartsAtTheHoveredRow()
        {
            var keys = new RibbonKeyboard();
            keys.Open(3);
            keys.Move(1, 6, 0, true);
            Assert.AreEqual(4, keys.Row);
            Assert.AreEqual(-1, keys.Destination);
        }

        [Test]
        public void FlyoutWrapsIndependentlyAndReturnsToItsParent()
        {
            var keys = new RibbonKeyboard();
            keys.Open();
            keys.Move(1, 6, 0, false);
            keys.EnterFlyout();

            keys.Move(-1, 6, 4, false);
            Assert.AreEqual(3, keys.Destination);
            Assert.AreEqual(2, keys.Row);
            keys.Move(1, 6, 4, false);
            Assert.AreEqual(0, keys.Destination);
            keys.LeaveFlyout();
            Assert.AreEqual(-1, keys.Destination);
            Assert.AreEqual(2, keys.Row);
            keys.Close();
            Assert.IsFalse(keys.Active);
        }
    }
}
