using System.Linq;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>Adult video reactions are offered and playable only while the 18+ gate is open.</summary>
    public sealed class AdultVideoReactionTests
    {
        const string Manifest =
            "{\"clips\":[" +
            "{\"name\":\"idle\",\"role\":\"idle\",\"color\":\"idle.mp4\"}," +
            "{\"name\":\"strip\",\"role\":\"reaction\",\"color\":\"strip.mp4\",\"adult\":true}," +
            "{\"name\":\"naked_idle\",\"role\":\"reaction\",\"color\":\"naked_idle.mp4\",\"playback\":\"loop\",\"hold\":12,\"adult\":true}," +
            "{\"name\":\"happy\",\"role\":\"reaction\",\"color\":\"happy.mp4\"}" +
            "]}";

        static readonly string[] Files = { "idle.mp4", "strip.mp4", "naked_idle.mp4", "happy.mp4" };

        const string NakedManifest =
            "{\"clips\":[" +
            "{\"name\":\"idle\",\"role\":\"idle\",\"color\":\"idle.mp4\"}," +
            "{\"name\":\"strip\",\"role\":\"reaction\",\"color\":\"strip.mp4\",\"adult\":true,\"after\":\"naked_idle\"}," +
            "{\"name\":\"naked_idle\",\"role\":\"reaction\",\"color\":\"naked_idle.mp4\",\"playback\":\"loop\",\"hold\":-1,\"adult\":true}" +
            "]}";

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
            AvatarReactionCatalog.Clear();
        }

        [Test]
        public void Resolve_ReadsTheAdultFlag()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(Manifest, Files);

            Assert.IsTrue(set.Clips[set.FindReaction("strip")].Adult);
            Assert.IsTrue(set.Clips[set.FindReaction("naked_idle")].Adult);
            Assert.IsFalse(set.Clips[set.FindReaction("happy")].Adult);
        }

        [Test]
        public void ClosedGate_HidesAndRefusesAdultClips()
        {
            IntimateExpressions.Reset();
            var set = VideoAvatarManifest.Resolve(Manifest, Files);
            AvatarReactionCatalog.Set(set.OfferedReactions(), set.OfferedAdultReactions());

            Assert.AreEqual(-1, set.FindReaction("strip"));
            Assert.IsFalse(AvatarReactionCatalog.Names.Contains("strip"));
            Assert.IsFalse(AvatarReactionCatalog.Contains("strip"));
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find("strip"));
            Assert.IsFalse(AvatarReactionCatalog.PromptLine().Contains("Adult-only"));

            Assert.IsTrue(AvatarReactionCatalog.Names.Contains("happy"));
            Assert.IsTrue(AvatarReactionCatalog.Contains("happy"));
        }

        [Test]
        public void OpenGate_OffersAndFindsAdultClips()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(Manifest, Files);
            AvatarReactionCatalog.Set(set.OfferedReactions(), set.OfferedAdultReactions());

            Assert.AreNotEqual(-1, set.FindReaction("strip"));
            Assert.AreNotEqual(-1, set.FindReaction("naked_idle"));
            Assert.IsTrue(AvatarReactionCatalog.Names.Contains("strip"));
            Assert.IsTrue(AvatarReactionCatalog.Contains("strip"));
            Assert.AreEqual("strip", AvatarReactionCatalog.Find("strip"));
            StringAssert.Contains("Adult-only video reactions", AvatarReactionCatalog.PromptLine());
            StringAssert.Contains("strip", AvatarReactionCatalog.PromptLine());
            StringAssert.Contains("she undresses", AvatarReactionCatalog.PromptLine());
        }

        [Test]
        public void Undressed_OffersOnlyNakedReactionsAndDress()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(Manifest, Files);
            AvatarReactionCatalog.Set(set.OfferedReactions(), set.OfferedAdultReactions());
            IntimateExpressions.Undress();

            Assert.AreEqual(-1, set.FindReaction("happy"));
            Assert.AreEqual(-1, set.FindReaction("strip"));
            Assert.AreNotEqual(-1, set.FindReaction("naked_idle"));
            Assert.IsFalse(AvatarReactionCatalog.Names.Contains("happy"));
            Assert.IsFalse(AvatarReactionCatalog.Names.Contains("strip"));
            Assert.IsTrue(AvatarReactionCatalog.Names.Contains("naked_idle"));
            Assert.IsTrue(AvatarReactionCatalog.Names.Contains("dress"));
            Assert.AreEqual("dress", AvatarReactionCatalog.Find("dress"));
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find("happy"));
            StringAssert.Contains("She is naked now", AvatarReactionCatalog.PromptLine());
            StringAssert.Contains("dress", AvatarReactionCatalog.PromptLine());

            IntimateExpressions.Dress();
            Assert.IsTrue(AvatarReactionCatalog.Names.Contains("happy"));
            Assert.IsTrue(AvatarReactionCatalog.Names.Contains("strip"));
            Assert.IsFalse(AvatarReactionCatalog.Names.Contains("dress"));
        }

        [Test]
        public void MinusOneHold_IsForever()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(
                "{\"clips\":[{\"name\":\"idle\",\"role\":\"idle\",\"color\":\"idle.mp4\"}," +
                "{\"name\":\"naked_idle\",\"role\":\"reaction\",\"color\":\"naked_idle.mp4\",\"playback\":\"loop\",\"hold\":-1,\"adult\":true}]}",
                Files);
            Assert.IsTrue(set.Clips[set.FindReaction("naked_idle")].HoldForever);
        }

        [Test]
        public void CancelQueuedReactions_DropsTheDressTail()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(Manifest, Files);
            var director = new VideoAvatarDirector(set, 1);

            director.Request("naked_idle", true);
            Assert.GreaterOrEqual(director.Queued, 0);
            director.CancelQueuedReactions();
            director.RequestFront();

            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(-1, director.PendingReaction);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void GateCloseFromNakedPose_ReturnsToClothedIdle()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(NakedManifest, Files);
            var director = new VideoAvatarDirector(set, 1);

            director.Request("strip", true);
            director.OnBoundary(true);
            director.OnEnded();

            Assert.AreEqual(VideoAvatarAct.Pose, director.Act);
            Assert.AreEqual("naked_idle", set.Clips[director.Clip].Name);

            director.ReturnToClothedIdle();

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual("idle", set.Clips[director.Clip].Name);
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(-1, director.PendingReaction);
        }

        [Test]
        public void GateCloseFromAdultReaction_ReturnsToClothedIdle()
        {
            IntimateExpressions.Configure(true, true, true);
            var set = VideoAvatarManifest.Resolve(NakedManifest, Files);
            var director = new VideoAvatarDirector(set, 1);

            director.Request("strip", true);
            director.OnBoundary(true);

            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);
            Assert.AreEqual("strip", set.Clips[director.Clip].Name);

            director.ReturnToClothedIdle();

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual("idle", set.Clips[director.Clip].Name);
        }
    }
}
