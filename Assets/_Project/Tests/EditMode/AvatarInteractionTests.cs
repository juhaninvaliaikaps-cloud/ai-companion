using System;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-101: the interaction menu's fixed actions and their model event lines.</summary>
    public sealed class AvatarInteractionTests
    {
        [Test]
        public void Catalog_HasAllTwentyThreeActions()
        {
            Assert.AreEqual(23, AvatarInteractionCatalog.All.Count);
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
            Assert.IsTrue(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.TouchVagina).Adult);
            Assert.IsTrue(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.TouchButt).Adult);
            Assert.IsFalse(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.TouchButt).BackOnly);
            Assert.IsTrue(AvatarInteractionCatalog.Find(AvatarInteractionCatalog.FingerMouth).Adult);
        }

        [Test]
        public void AdultLine_IsDroppedWhenTheGateClosesBeforeItIsSent()
        {
            Assert.AreEqual(InteractionSignalDelivery.Choice.Drop, InteractionSignalDelivery.Decide(true, false, true));
            Assert.AreEqual(InteractionSignalDelivery.Choice.Drop, InteractionSignalDelivery.Decide(true, false, false), "a busy chat must not keep the adult line");
            Assert.AreEqual(InteractionSignalDelivery.Choice.Hold, InteractionSignalDelivery.Decide(true, true, false));
            Assert.AreEqual(InteractionSignalDelivery.Choice.Send, InteractionSignalDelivery.Decide(true, true, true));
            Assert.AreEqual(InteractionSignalDelivery.Choice.Send, InteractionSignalDelivery.Decide(false, false, true), "an ordinary line still sends after 18+ is off");
            Assert.AreEqual(InteractionSignalDelivery.Choice.Hold, InteractionSignalDelivery.Decide(false, false, false));

            for (var i = 0; i < AvatarInteractionCatalog.All.Count; i++)
            {
                var item = AvatarInteractionCatalog.All[i];
                var choice = InteractionSignalDelivery.Decide(item.Adult, false, true);
                Assert.AreEqual(item.Adult ? InteractionSignalDelivery.Choice.Drop : InteractionSignalDelivery.Choice.Send, choice, item.Id);
            }
        }

        [Test]
        public void InteractionPromptCap_AllowsTwoPerMinute()
        {
            var limiter = new InteractionPromptLimiter();
            var now = DateTime.UtcNow;
            Assert.IsTrue(limiter.TryTake(now));
            Assert.IsTrue(limiter.TryTake(now.AddSeconds(5)));
            Assert.IsFalse(limiter.TryTake(now.AddSeconds(10)));
            Assert.IsTrue(limiter.TryTake(now.AddSeconds(61)));
            Assert.AreEqual(2, limiter.Count);
            limiter.Reset();
            Assert.IsTrue(limiter.TryTake(now.AddSeconds(62)));
        }

        [Test]
        public void AdultScene_ContinuesTouchText()
        {
            AdultSceneNow.Reset();
            AdultSceneNow.Compose(AvatarInteractionCatalog.BlowjobKneeling, "*User guides you down.*");
            Assert.AreEqual("*User grabs your butt, while you are on your knees, giving him a blowjob.*",
                AdultSceneNow.Compose(AvatarInteractionCatalog.TouchButt, "*User touches your butt.*"));
            Assert.AreEqual("*User grabs your breasts, and butt while you are on your knees, giving him a blowjob.*",
                AdultSceneNow.Compose(AvatarInteractionCatalog.TouchBreast, "*User cups your breast.*"));
            AdultSceneNow.Reset();
            Assert.IsFalse(AdultSceneNow.Active);
        }

        [Test]
        public void TypedScene_DetectsBlowjobAndLyingKiss()
        {
            IntimateExpressions.Configure(true, true, true);
            try
            {
                Assert.AreEqual(AvatarInteractionCatalog.BlowjobKneeling,
                    AdultSceneIntent.Detect("She kneels down and gives him a blowjob."));
                Assert.AreEqual(AvatarInteractionCatalog.BlowjobLying,
                    AdultSceneIntent.Detect("She lies down and takes him in her mouth."));
                Assert.AreEqual(AvatarInteractionCatalog.BlowjobOnTop,
                    AdultSceneIntent.Detect("She is on top, giving him a blowjob."));
                Assert.AreEqual(AvatarInteractionCatalog.LyingKiss,
                    AdultSceneIntent.Detect("They lie together and kiss."));
                Assert.AreEqual(AvatarInteractionCatalog.ButtSlap,
                    AdultSceneIntent.Detect("He slaps your butt."));
            }
            finally
            {
                IntimateExpressions.Reset();
            }
        }

        [Test]
        public void InteractionLine_IsNotScoredAsTheUsersMood()
        {
            // "kisses" is an affectionate word. Scoring the kiss event would play the shy
            // mood face over the kiss expression. The send path uses ScoreAsUserMood.
            var kiss = AvatarInteractionCatalog.Find(AvatarInteractionCatalog.Kiss).Signal;
            Assert.AreEqual(UserEmotion.Affectionate, UserMoodDetector.Detect(kiss).Emotion);
            Assert.AreEqual(Emotion.Shy, UserMoodReaction.FaceFor(UserEmotion.Affectionate));
            Assert.IsFalse(InteractionSignalDelivery.ScoreAsUserMood(true));
            Assert.IsTrue(InteractionSignalDelivery.ScoreAsUserMood(false));
        }
    }
}
