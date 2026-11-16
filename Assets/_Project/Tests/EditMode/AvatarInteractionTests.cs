using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-101: the interaction menu's fixed actions and their model event lines.</summary>
    public sealed class AvatarInteractionTests
    {
        [Test]
        public void Catalog_HasAllNineActions()
        {
            Assert.AreEqual(9, AvatarInteractionCatalog.All.Count);
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.PatHead));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.StrokeHair));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.CupCheek));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.TouchFace));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.Kiss));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.HoldHand));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.LetGo));
            Assert.IsNotNull(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.TouchShoulder));
        }

        [Test]
        public void EveryAction_HasAVisibleLabelAndAModelEvent()
        {
            for (var i = 0; i < AvatarInteractionCatalog.All.Count; i++)
            {
                var item = AvatarInteractionCatalog.All[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Label), item.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Signal), item.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Expression), item.Id);
            }
        }

        [Test]
        public void KissAndHold_AreTheStatefulOnes()
        {
            Assert.AreEqual("kiss", AvatarInteractionCatalog.Find(AvatarInteractionCatalog.Kiss).Clip);
            Assert.AreEqual("kiss", AvatarInteractionCatalog.Find(AvatarInteractionCatalog.Kiss).Expression);
            Assert.IsTrue(AvatarInteractionCatalog.IsHold(AvatarInteractionCatalog.HoldHand));
            Assert.IsTrue(AvatarInteractionCatalog.IsRelease(AvatarInteractionCatalog.LetGo));
            Assert.IsFalse(AvatarInteractionCatalog.IsHold(AvatarInteractionCatalog.Kiss));
            var breast = AvatarInteractionCatalog.Find(AvatarInteractionCatalog.TouchBreast);
            Assert.IsNotNull(breast);
            Assert.IsTrue(breast.Adult);
            Assert.IsFalse(string.IsNullOrWhiteSpace(breast.Signal));
        }
    }
}
