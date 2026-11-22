using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-169: a secret's lean sets the sign of a landing, and Player.log keeps no secret text.</summary>
    public sealed class SecretLeanTests
    {
        [Test]
        public void AKinkFoundDuringAFlirtWithANearStranger_IsACriticalGain()
        {
            // Jussi's log, 2026-11-22: affection -4.1, familiarity 5.6, a flirt that found k1.
            var before = RelationshipMath.Land(LandingEvent.Flirted, 0.6f, -4.1f, 5.6f, true, 1f);
            Assert.Less(before.AffectionDelta, 0f, "without a lean the flirt's small loss is doubled");

            var now = RelationshipMath.Land(LandingEvent.Flirted, 0.6f, -4.1f, 5.6f, true, 1f, SecretLean.Likes);
            Assert.IsTrue(now.Critical);
            Assert.Greater(now.AffectionDelta, 0f, "doing her kink is always a gain");

            var compliment = RelationshipMath.Land(LandingEvent.Complimented, 0.6f, -4.1f, 5.6f, true, 1f);
            Assert.GreaterOrEqual(now.AffectionDelta, compliment.AffectionDelta - 0.001f, "at least a compliment's size");
        }

        [Test]
        public void AStingAlwaysLoses_EvenWhenTheMessageWasKind()
        {
            var change = RelationshipMath.Land(LandingEvent.Complimented, 0.5f, 20f, 50f, true, 1f, SecretLean.Stings);
            Assert.Less(change.AffectionDelta, 0f);
            Assert.IsTrue(change.Critical, "the first find is still a critical hit");
        }

        [Test]
        public void ALikedSecret_NeverShrinksABiggerGain()
        {
            // Close: comfort is worth more than a compliment, and the lean keeps it.
            var plain = RelationshipMath.Land(LandingEvent.Comforted, 0.5f, 20f, 100f, false, 1f);
            var liked = RelationshipMath.Land(LandingEvent.Comforted, 0.5f, 20f, 100f, false, 1f, SecretLean.Likes);
            Assert.AreEqual(plain.AffectionDelta, liked.AffectionDelta, 0.001f);
        }

        [Test]
        public void NoLean_AndANeutralLanding_ChangeNothing()
        {
            var old = RelationshipMath.Land(LandingEvent.Teased, 0.5f, 10f, 30f, true, 1f);
            var none = RelationshipMath.Land(LandingEvent.Teased, 0.5f, 10f, 30f, true, 1f, SecretLean.None);
            Assert.AreEqual(old.AffectionDelta, none.AffectionDelta, 0.0001f);

            var neutral = RelationshipMath.Land(LandingEvent.Neutral, 0.5f, 10f, 30f, true, 1f, SecretLean.Likes);
            Assert.AreEqual(0f, neutral.AffectionDelta, 0.0001f, "the model's neutral stays neutral");
            Assert.IsFalse(neutral.Critical);
        }

        [Test]
        public void TheLogTextOfAFoundSecret_HasNoSecretText()
        {
            var feedback = new RelationshipFeedback();
            Assert.IsTrue(feedback.AddSecret("Exhibitionism: showing off", 0f, true));
            var piece = feedback.Piece(feedback.Count - 1);
            Assert.AreEqual(FeedbackKind.Secret, piece.Kind);
            StringAssert.Contains("Exhibitionism", piece.Label, "the floating piece still shows it");
            Assert.AreEqual("Secret secret", piece.LogText);

            var flirt = new FeedbackPiece { Kind = FeedbackKind.Down, Amount = "-0.9", Label = "Flirt" };
            Assert.AreEqual("Down -0.9 Flirt", flirt.LogText);
        }
    }
}
