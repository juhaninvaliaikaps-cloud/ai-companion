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
        public void ReactionName_AWalkOrStopWithAnEmotion_AsksForTheEmotionsClip()
        {
            // Play returns after a walk or stop before the face is set (Bugbot on PR #56), so
            // the 2D emotion clip would never start from the face.
            AvatarReactionCatalog.Set(new[] { "happy", "concerned", "pout" });

            Assert.AreEqual("happy", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"happy\" gesture=\"walk\" direction=\"left\" />\nx").Cue));
            Assert.AreEqual("concerned", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"concerned\" gesture=\"stop\" />\nx").Cue));
            // Neutral, or an emotion without a clip: nothing.
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"neutral\" gesture=\"walk\" />\nx").Cue));
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"surprised\" gesture=\"walk\" />\nx").Cue));
            // A named reaction still wins, and a reaction word as the emotion still counts.
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"happy\" gesture=\"walk\" reaction=\"pout\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pout\" gesture=\"walk\" />\nx").Cue));
            // A reaction word as the emotion beats the clip of the emotion it aliases to.
            AvatarReactionCatalog.Set(new[] { "happy", "sad", "pout" });
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pout\" gesture=\"walk\" />\nx").Cue));
            Assert.AreEqual("sad", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"sad\" gesture=\"walk\" />\nx").Cue));
            // Without a walk, a plain emotion still goes through the face only.
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"happy\" />\nx").Cue));
        }

        [Test]
        public void ReactionName_PoutAndPouty_ResolveToPoutOnEveryPath()
        {
            AvatarReactionCatalog.Set(new[] { "happy", "pout", "wink_dance" });

            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation reaction=\"pout\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation reaction=\"pouty\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation gesture=\"pout\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation gesture=\"pouty\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pout\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pouty\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pout\" gesture=\"walk\" direction=\"left\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"pouty\" gesture=\"walk\" direction=\"left\" />\nx").Cue));
            Assert.AreEqual("pout", Director.ReactionName(AnimationCueParser.Parse("<animation reaction=\"pouty\" gesture=\"walk\" direction=\"left\" />\nx").Cue));
        }

        [Test]
        public void Play_PoutCue_RequestsThePoutClipOnEveryPath()
        {
            AvatarReactionCatalog.Set(new[] { "happy", "pout", "wink_dance" });
            var host = new GameObject("director-pout-play");
            try
            {
                var director = host.AddComponent<Director>();
                var reactions = host.AddComponent<RecordingReactions>();
                var tags = new[]
                {
                    "<animation reaction=\"pout\" />",
                    "<animation reaction=\"pouty\" />",
                    "<animation gesture=\"pout\" />",
                    "<animation gesture=\"pouty\" />",
                    "<animation emotion=\"pout\" intensity=\"0.6\" />",
                    "<animation emotion=\"pouty\" intensity=\"0.6\" face=\"pouty\" />",
                    "<animation emotion=\"pout\" gesture=\"walk\" direction=\"left\" />",
                    "<animation emotion=\"pouty\" gesture=\"walk\" direction=\"left\" />",
                };

                foreach (var tag in tags)
                {
                    reactions.Last = null;
                    director.Play(AnimationCueParser.Parse(tag + "\nFine.").Cue);
                    Assert.AreEqual("pout", reactions.Last, tag);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Catalog_Find_IsTheNameOrTheOnlyNameStartingWithTheWord()
        {
            AvatarReactionCatalog.Set(new[] { "pout", "turn_around_back", "turn_around_front", "wink_dance" });

            Assert.AreEqual("pout", AvatarReactionCatalog.Find("POUT"));
            Assert.AreEqual("pout", AvatarReactionCatalog.Find("pouty"));
            Assert.AreEqual("pout", AvatarReactionCatalog.Find("pouting"));
            Assert.AreEqual("wink_dance", AvatarReactionCatalog.Find("wink"));
            // Two fit: none.
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find("turn_around"));
            // Only whole words before an underscore.
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find("po"));
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find(""));
            Assert.AreEqual(string.Empty, AvatarReactionCatalog.Find(null));
        }

        [Test]
        public void DistressedAndTearyHappy_AreReadable_AndPickTheClosestClip()
        {
            // JUH-113: the 2026-11-12 log tag, written exactly as the model wrote it.
            AvatarReactionCatalog.Set(new[] { "happy", "concerned", "pout", "flustered" });
            var parsed = AnimationCueParser.Parse("<animation emotion=\"distressed\" intensity=\"0.7\" gesture=\"teary_happy\">\nOh. Thank you.");
            Assert.AreEqual(CueParseStatus.Parsed, parsed.Status);
            Assert.AreEqual(Emotion.Concerned, parsed.Cue.Emotion);
            Assert.AreEqual(0.7f, parsed.Cue.Intensity, 0.0001f);
            Assert.AreEqual("happy", Director.ReactionName(parsed.Cue));

            Assert.AreEqual("happy", AvatarReactionCatalog.Find("teary_happy"));
            Assert.AreEqual("happy", AvatarReactionCatalog.Alias("teary_happy"));
            Assert.AreEqual("concerned", AvatarReactionCatalog.Find("distressed"));
            Assert.AreEqual("concerned", Director.ReactionName(AnimationCueParser.Parse("<animation reaction=\"distressed\" />\nx").Cue));
            Assert.AreEqual("concerned", Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"distressed\" gesture=\"walk\" direction=\"left\" />\nx").Cue));
            // A plain emotion word still drives only the face, as emotion="concerned" does.
            Assert.AreEqual(string.Empty, Director.ReactionName(AnimationCueParser.Parse("<animation emotion=\"distressed\" />\nx").Cue));
            // Unknown words are untouched by the alias.
            Assert.AreEqual("wave", AvatarReactionCatalog.Alias("wave"));
        }

        [TestCase("distressed", Emotion.Concerned)]
        [TestCase("anxious", Emotion.Concerned)]
        [TestCase("nervous", Emotion.Concerned)]
        [TestCase("upset", Emotion.Concerned)]
        [TestCase("teary_happy", Emotion.Happy)]
        [TestCase("hurt", Emotion.Sad)]
        [TestCase("grumpy", Emotion.Sad)]
        [TestCase("playful", Emotion.Happy)]
        [TestCase("affectionate", Emotion.Happy)]
        public void MoodWords_ParseAsAFace(string word, Emotion expected)
        {
            Assert.IsTrue(EmotionNames.TryParse(word, out var emotion), word);
            Assert.AreEqual(expected, emotion, word);
            var parsed = AnimationCueParser.Parse("<animation emotion=\"" + word + "\" />\nx");
            Assert.AreEqual(CueParseStatus.Parsed, parsed.Status, word);
        }
    }

    /// <summary>Records the reaction name the director asks for in the Play tests.</summary>
    public sealed class RecordingReactions : MonoBehaviour, IAvatarReactions
    {
        public string Last;

        public AvatarReactionResult RequestReaction(string name)
        {
            Last = name;
            return AvatarReactionResult.Started;
        }
    }
}
