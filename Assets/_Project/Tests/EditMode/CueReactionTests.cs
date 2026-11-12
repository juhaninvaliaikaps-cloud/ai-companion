using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-99: cue tags as models really write them, the reaction attribute, the raw tag for
    /// the log, and the reaction list in the system prompt.
    /// </summary>
    public class CueReactionTests
    {
        [TearDown]
        public void ClearCatalog()
        {
            AvatarReactionCatalog.Clear();
        }

        // ---- what models write -----------------------------------------------------------

        [TestCase("<animation emotion=\"sad\" reaction=\"pout\" />\nFine.")]
        [TestCase("<ANIMATION REACTION=\"POUT\" EMOTION=\"SAD\"/>\nFine.")]
        [TestCase("<animation   reaction = 'pout'   emotion = 'sad'  >\nFine.")]
        [TestCase("<animation emotion=sad reaction=pout />\nFine.")]
        [TestCase("<animation emotion=sad reaction=pout>\nFine.")]
        [TestCase("Fine.\n<animation emotion=\"sad\" reaction=\"pout\" />")]
        [TestCase("Fine.\n\n<animation emotion=\"sad\" reaction=\"pout\" />\n")]
        public void Parse_AcceptsCaseSpacingOrderQuotesAndAMissingSlash_AtTheStartOrTheEnd(string reply)
        {
            var result = AnimationCueParser.Parse(reply);

            Assert.AreEqual(CueParseStatus.Parsed, result.Status, reply);
            Assert.AreEqual(Emotion.Sad, result.Cue.Emotion, reply);
            Assert.AreEqual("pout", result.Cue.Reaction, reply);
            Assert.AreEqual("Fine.", result.Text, reply);
        }

        [Test]
        public void Parse_ATagInTheMiddleOfTheText_IsReadAndCutOutCleanly()
        {
            var result = AnimationCueParser.Parse("*She huffs.* <animation emotion=\"sad\" reaction=\"pout\" /> Fine, then.");

            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual("pout", result.Cue.Reaction);
            Assert.AreEqual("*She huffs.* Fine, then.", result.Text);
        }

        [Test]
        public void Parse_ALooseTagWithOnlyAReaction_IsRead()
        {
            var result = AnimationCueParser.Parse("Hmph.\n<reaction=\"pout\" />");

            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual("pout", result.Cue.Reaction);
            Assert.AreEqual(Emotion.Neutral, result.Cue.Emotion);
            Assert.AreEqual("Hmph.", result.Text);
        }

        [Test]
        public void Parse_AReactionWithAnUnknownEmotion_StillCarriesTheReaction()
        {
            var result = AnimationCueParser.Parse("Hmph.\n<animation emotion=\"furious\" reaction=\"pout\" />");

            Assert.AreEqual(CueParseStatus.Invalid, result.Status);
            Assert.AreEqual(Emotion.Neutral, result.Cue.Emotion);
            Assert.AreEqual("pout", result.Cue.Reaction);
        }

        [Test]
        public void Parse_AReactionNameIsCleanedToACueToken()
        {
            var result = AnimationCueParser.Parse("<animation reaction=\"Wink_Dance!\" />\nYay.");

            Assert.AreEqual("wink_dance", result.Cue.Reaction);
        }

        [Test]
        public void Parse_ABrokenSecondTag_DoesNotUndoAGoodFirstOne()
        {
            var result = AnimationCueParser.Parse("<animation emotion=\"happy\" />\nYay.\n<animation emotion=\"happy\" intensity=\"NaN\" />");

            Assert.AreEqual(CueParseStatus.Parsed, result.Status);
            Assert.AreEqual(Emotion.Happy, result.Cue.Emotion);
            Assert.AreEqual("<animation emotion=\"happy\" />", result.RawTag);
        }

        [Test]
        public void Parse_RawTag_IsTheTagAsWritten_OnOneLine_AndCut()
        {
            Assert.AreEqual(string.Empty, AnimationCueParser.Parse("No tag here.").RawTag);

            var broken = AnimationCueParser.Parse("Hi.\n<animation emotion=\"furious\" />");
            Assert.AreEqual("<animation emotion=\"furious\" />", broken.RawTag);
            Assert.AreEqual("Cue tag (not readable, used neutral): <animation emotion=\"furious\" />",
                AnimationCueParser.DescribeTag(broken.RawTag, broken.Status));

            var unclosed = AnimationCueParser.Parse("Hi.\n<animation emotion=\"ha");
            Assert.AreEqual("<animation emotion=\"ha", unclosed.RawTag);

            var longTag = AnimationCueParser.Parse("<animation emotion=\"happy\" gesture=\"" + new string('x', 300) + "\" />\nHi.");
            Assert.AreEqual(AnimationCueParser.MaxRawTagLength + 3, longTag.RawTag.Length);
            StringAssert.DoesNotContain("\n", longTag.RawTag);
        }

        [Test]
        public void StreamFilter_HoldsBackATagAtTheStart_AndReleasesTheReplyAfterIt()
        {
            var filter = new CueStreamFilter();
            var shown = filter.Push("<animation emotion=\"sad\" rea");
            Assert.AreEqual(string.Empty, shown);
            shown += filter.Push("ction=\"pout\" />\n*She huffs.*");
            shown += filter.Flush();

            Assert.AreEqual("*She huffs.*", shown);
            Assert.AreEqual("pout", filter.Cue.Reaction);
            Assert.AreEqual("<animation emotion=\"sad\" reaction=\"pout\" />", filter.RawTag);
        }

        // ---- the reaction list -----------------------------------------------------------

        [Test]
        public void Catalog_CleansNames_DropsDuplicates_AndBuildsThePromptLine()
        {
            AvatarReactionCatalog.Set(new[] { "happy", "Pout", "pout", "", "wink_dance" });

            CollectionAssert.AreEqual(new[] { "happy", "pout", "wink_dance" }, AvatarReactionCatalog.Names);
            Assert.IsTrue(AvatarReactionCatalog.Contains("POUT"));
            Assert.IsFalse(AvatarReactionCatalog.Contains("walk"));

            var line = AvatarReactionCatalog.PromptLine();
            StringAssert.Contains("happy, pout, wink_dance", line);
            // The example is not "happy", so the model sees a reaction that is not an emotion.
            StringAssert.Contains("<animation emotion=\"neutral\" reaction=\"pout\" />", line);

            AvatarReactionCatalog.Clear();
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.PromptLine());
        }

        [Test]
        public void Prompt_AlwaysCarriesTheCueInstruction_EvenWithoutAGestureCatalog()
        {
            var host = new GameObject("director-no-catalog");
            try
            {
                var director = host.AddComponent<Director>();

                var prompt = director.SystemPromptSuffix;

                StringAssert.Contains(AnimationCueParser.Instruction, prompt);
                StringAssert.Contains(EmotionNames.CatalogLine, prompt);
                StringAssert.DoesNotContain("Body reactions", prompt);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prompt_ListsTheLoadedReactions_BeforeTheInstruction()
        {
            var host = new GameObject("director-reactions");
            try
            {
                var director = host.AddComponent<Director>();
                AvatarReactionCatalog.Set(new[] { "happy", "pout", "wink_dance" });

                var prompt = director.SystemPromptSuffix;

                StringAssert.Contains("Body reactions you can play once, by name: happy, pout, wink_dance.", prompt);
                Assert.Less(prompt.IndexOf("Body reactions", System.StringComparison.Ordinal),
                    prompt.IndexOf(AnimationCueParser.Instruction, System.StringComparison.Ordinal));
                Assert.IsTrue(prompt.TrimEnd().EndsWith(EmotionNames.CatalogLine, System.StringComparison.Ordinal));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Instruction_AsksForTheTagFirst_InRoleplayToo()
        {
            StringAssert.Contains("Start every reply with one cue tag on its own first line", AnimationCueParser.Instruction);
            StringAssert.Contains("also in roleplay and narration", AnimationCueParser.Instruction);
            StringAssert.Contains("<animation", AnimationCueParser.TurnReminder);
        }

        // ---- which reaction a cue asks for ------------------------------------------------

        [Test]
        public void ReactionName_IsTheAttribute_OrAnEmotionWordThatNamesAnOfferedReaction()
        {
            AvatarReactionCatalog.Set(new[] { "happy", "pout", "wink_dance" });

            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation reaction=\"pout\" />\nx").Cue));
            // A model that writes the reaction as the emotion still gets it.
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pout\" />\nx").Cue));
            // A plain emotion reaches the 2D avatar through the face, not twice.
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"happy\" />\nx").Cue));
            // Not offered: nothing.
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"sleepy\" />\nx").Cue));
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.NeutralCue));
        }

        [Test]
        public void ReactionName_AGestureThatNamesAnOfferedReaction_PlaysIt()
        {
            // The tags Rocinante-X-12B wrote in the JUH-99 player test, 2026-11-12.
            AvatarReactionCatalog.Set(new[] { "happy", "concerned", "turn360", "embarrassed", "flustered", "pout", "wink_dance", "turn_around_back" });

            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"sad\" intensity=\"0.6\" gesture=\"pout\" />\nx").Cue));
            Assert.AreEqual("wink_dance", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"neutral\" gesture=\"wink_dance\" intensity=\"0.7\" />\nx").Cue));
            // One offered name starts with the word: that one.
            Assert.AreEqual("turn_around_back", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"neutral\" gesture=\"turn_around\" />\nx").Cue));
            Assert.AreEqual("wink_dance", Director.ReactionName(AnimationCueParser.Parse("<animation gesture=\"wink\" />\nx").Cue));
            // The reaction attribute still wins over the gesture.
            Assert.AreEqual("flustered", Director.ReactionName(AnimationCueParser.Parse("<animation reaction=\"flustered\" gesture=\"pout\" />\nx").Cue));
            // An emotion clip named as the gesture plays, unless the emotion already asks for it.
            Assert.AreEqual("flustered", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"neutral\" gesture=\"flustered\" />\nx").Cue));
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"happy\" gesture=\"happy\" />\nx").Cue));
            // Not reactions: walk, stop, a 3D gesture.
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"neutral\" gesture=\"walk\" direction=\"left\" />\nx").Cue));
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation gesture=\"stop\" />\nx").Cue));
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"happy\" gesture=\"laugh\" />\nx").Cue));
        }

        [Test]
        public void Catalog_Find_IsTheNameOrTheOnlyNameStartingWithTheWord()
        {
            AvatarReactionCatalog.Set(new[] { "pout", "turn_around_back", "turn_around_front", "wink_dance" });

            Assert.AreEqual("pout", AvatarReactionCatalog.Find("POUT"));
            Assert.AreEqual("wink_dance", AvatarReactionCatalog.Find("wink"));
            // Two fit: none.
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find("turn_around"));
            // Only whole words before an underscore.
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find("po"));
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find(""));
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find(null));
        }
    }
}
