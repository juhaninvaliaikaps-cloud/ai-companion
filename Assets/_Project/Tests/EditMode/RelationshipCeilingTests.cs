using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public sealed class RelationshipCeilingTests
    {
        [Test]
        public void Affection_Ceiling_UsesEveryDiscovery()
        {
            Assert.AreEqual(80f, RelationshipCeiling.Affection(0, 4, 0, 2), 0.001f);
            Assert.AreEqual(90f, RelationshipCeiling.Affection(2, 4, 1, 2), 0.001f);
            Assert.AreEqual(100f, RelationshipCeiling.Affection(4, 4, 2, 2), 0.001f);
            Assert.AreEqual(100f, RelationshipCeiling.Affection(0, 0, 0, 0), 0.001f, "no secrets must not lock a persona forever");
        }

        [Test]
        public void Innocence_Ceiling_UsesKinksAndSensitiveSpots()
        {
            Assert.AreEqual(70f, RelationshipCeiling.Innocence(0, 6), 0.001f);
            Assert.AreEqual(85f, RelationshipCeiling.Innocence(3, 6), 0.001f);
            Assert.AreEqual(100f, RelationshipCeiling.Innocence(6, 6), 0.001f);
            Assert.AreEqual(100f, RelationshipCeiling.Innocence(0, 0), 0.001f);
        }

        [Test]
        public void Clamp_DoesNotClawBackAnExistingHigherValue()
        {
            Assert.AreEqual(80f, RelationshipCeiling.ClampAffection(70f, 90f, 80f), 0.001f);
            Assert.AreEqual(100f, RelationshipCeiling.ClampAffection(100f, 100f, 80f), 0.001f);
            Assert.AreEqual(45f, RelationshipCeiling.ClampAffection(60f, 45f, 80f), 0.001f, "negative changes still pass");
            Assert.AreEqual(70f, RelationshipCeiling.ClampInnocence(69f, 75f, 70f), 0.001f);
        }

        [Test]
        public void Prompt_HasAnInLoveTierAndAHoldingBackTier()
        {
            StringAssert.Contains("holding something back", RelationshipPrompt.AffectionPhrase(85f, false));
            StringAssert.Contains("in love", RelationshipPrompt.AffectionPhrase(85f, true));
            StringAssert.Contains("deeply in love", RelationshipPrompt.AffectionPhrase(96f, true));
            StringAssert.Contains("hold", RelationshipPrompt.Line(null, null, 85f, 60f, false));
            StringAssert.Contains("in love", RelationshipPrompt.Line(null, null, 85f, 60f, true));
        }

        [Test]
        public void HighAffection_GainsAreHarder_ButNegativeGainsAreNot()
        {
            Assert.AreEqual(1f, RelationshipMath.TierMultiplier(10f), 0.001f);
            Assert.AreEqual(0.5f, RelationshipMath.TierMultiplier(65f), 0.001f);
            Assert.AreEqual(0.25f, RelationshipMath.TierMultiplier(82f), 0.001f);
            Assert.AreEqual(0.15f, RelationshipMath.TierMultiplier(95f), 0.001f);
            Assert.Less(RelationshipMath.Land(LandingEvent.Teased, 0.5f, 85f, 60f).AffectionDelta,
                RelationshipMath.Land(LandingEvent.Teased, 0.5f, 10f, 60f).AffectionDelta);
        }
    }
}
