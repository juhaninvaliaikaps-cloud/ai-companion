using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-180: a claimed first find is checked with one yes/no question before it counts.</summary>
    public sealed class SecretCheckTests
    {
        static readonly SecretClaim Sting = new SecretClaim { Id = "t4", Text = "being called meathead.", Likes = false };
        static readonly SecretClaim Win = new SecretClaim { Id = "w2", Text = "honest compliments", Likes = true };
        static readonly SecretClaim Kink = new SecretClaim { Id = "k1", Text = "being praised", Intimate = true };

        [TearDown]
        public void TearDown()
        {
            SecretClaimNow.Reset();
        }

        [Test]
        public void Question_ForAnEverydaySecret_HasOnlyTheUsersMessage()
        {
            var q = SecretCheck.Question(Sting, "you are lovely", "<animation emotion=\"angry\" secret=\"t4\"/> You meathead!");
            StringAssert.Contains("She is touchy about: \"being called meathead\"", q);
            StringAssert.Contains("The user's message: \"you are lovely\"", q);
            StringAssert.DoesNotContain("You meathead", q, "her own words must not be in the question");
            StringAssert.Contains("Only what the user wrote counts", q);
            StringAssert.Contains("yes or no", q);

            StringAssert.Contains("She warms to: \"honest compliments\"", SecretCheck.Question(Win, "hi", null));
        }

        [Test]
        public void Question_ForAnIntimateSecret_AlsoHasHerReplyWithoutTheTag()
        {
            var q = SecretCheck.Question(Kink, "good girl", "<animation emotion=\"loving\" secret=\"k1\"/>She melts at the praise.");
            StringAssert.Contains("she has this secret: \"being praised\"", q);
            StringAssert.Contains("Her reply: \"She melts at the praise.\"", q);
            StringAssert.DoesNotContain("<animation", q);
        }

        [Test]
        public void Question_QuotesAreMadeSingle_AndLongTextIsCut()
        {
            var q = SecretCheck.Question(Sting, "she said \"hi\"", null);
            StringAssert.Contains("she said 'hi'", q);

            var longText = new string('a', SecretCheck.MaxQuoted + 200);
            var cut = SecretCheck.Question(Sting, longText, null);
            StringAssert.Contains(new string('a', SecretCheck.MaxQuoted) + "...", cut);
            StringAssert.DoesNotContain(new string('a', SecretCheck.MaxQuoted + 1), cut);
        }

        [TestCase("yes", SecretVerdict.Yes)]
        [TestCase("Yes.", SecretVerdict.Yes)]
        [TestCase("  **YES**, clearly", SecretVerdict.Yes)]
        [TestCase("\"yes\"", SecretVerdict.Yes)]
        [TestCase("Kyllä", SecretVerdict.Yes)]
        [TestCase("no", SecretVerdict.No)]
        [TestCase("No, the user never said that.", SecretVerdict.No)]
        [TestCase("Ei", SecretVerdict.No)]
        [TestCase("not really", SecretVerdict.Unclear)]
        [TestCase("nope", SecretVerdict.Unclear)]
        [TestCase("Maybe", SecretVerdict.Unclear)]
        [TestCase("", SecretVerdict.Unclear)]
        [TestCase(null, SecretVerdict.Unclear)]
        [TestCase("<animation emotion=\"neutral\"/>No", SecretVerdict.No)]
        public void Parse_ReadsTheFirstWord(string reply, SecretVerdict expected)
        {
            Assert.AreEqual(expected, SecretCheck.Parse(reply));
        }

        [Test]
        public void WithoutSecret_KeepsEverythingElse()
        {
            var cue = new AnimationCue(Emotion.Flustered, 0.8f, "stop", 1f, "flustered", "pout", LandingEvent.Flirted, "t4", DesireHeat.Warm);
            var plain = SecretCheck.WithoutSecret(cue);
            Assert.AreEqual(string.Empty, plain.Secret);
            Assert.AreEqual(cue.Emotion, plain.Emotion);
            Assert.AreEqual(cue.Intensity, plain.Intensity);
            Assert.AreEqual(cue.Gesture, plain.Gesture);
            Assert.AreEqual(cue.Direction, plain.Direction);
            Assert.AreEqual(cue.EmotionField, plain.EmotionField);
            Assert.AreEqual(cue.Reaction, plain.Reaction);
            Assert.AreEqual(cue.Landed, plain.Landed);
            Assert.AreEqual(cue.Heat, plain.Heat);
        }

        [Test]
        public void NeedsCheck_AsksTheLookup_OnlyWhenTheCueNamesASecret()
        {
            var asked = 0;
            SecretClaimNow.Lookup = (string id, out SecretClaim claim) =>
            {
                asked++;
                claim = Sting;
                return id == "t4";
            };

            SecretClaim found;
            Assert.IsTrue(SecretClaimNow.NeedsCheck(new AnimationCue(Emotion.Neutral, 0.5f, null, 0f, null, null, LandingEvent.Flirted, "t4"), out found));
            Assert.AreEqual("t4", found.Id);
            Assert.IsFalse(SecretClaimNow.NeedsCheck(new AnimationCue(Emotion.Neutral, 0.5f, null, 0f, null, null, LandingEvent.Flirted, "t1"), out found));
            Assert.IsFalse(SecretClaimNow.NeedsCheck(new AnimationCue(Emotion.Neutral, 0.5f, null), out found));
            Assert.AreEqual(2, asked, "no lookup for a cue without a secret");
        }

        [Test]
        public void NeedsCheck_IsFalse_WithoutALookupOrWhenItThrows()
        {
            SecretClaim found;
            var cue = new AnimationCue(Emotion.Neutral, 0.5f, null, 0f, null, null, LandingEvent.Neutral, "t4");
            Assert.IsFalse(SecretClaimNow.NeedsCheck(cue, out found));

            SecretClaimNow.Lookup = (string id, out SecretClaim claim) => { throw new System.InvalidOperationException(); };
            Assert.IsFalse(SecretClaimNow.NeedsCheck(cue, out found));
        }

        [Test]
        public void Instructions_SayOnlyTheUsersOwnMessageCounts()
        {
            StringAssert.Contains("Only what the user wrote counts", HerSecrets.Instruction);
            StringAssert.Contains("your own words don't count", LandingEvents.TurnReminder);
            StringAssert.Contains("secret=", HerSecrets.Instruction);
        }
    }
}
