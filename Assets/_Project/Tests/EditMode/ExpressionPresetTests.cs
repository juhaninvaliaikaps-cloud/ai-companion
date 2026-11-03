using System.Collections.Generic;
using System.IO;
using AICompanion.Avatar;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class ExpressionPresetTests
    {
        [SetUp]
        public void ResetLibrary()
        {
            ExpressionPresetLibrary.ResetActive();
        }

        [TearDown]
        public void ReleaseLibrary()
        {
            ExpressionPresetLibrary.ResetActive();
        }

        [Test]
        public void ShippedPresets_MatchTheExpressionSheet()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/ExpressionPresets.json");
            var library = ExpressionPresetLibrary.Parse(File.ReadAllText(path));
            var happy = library.Get(Emotion.Happy);
            var concerned = library.Get(Emotion.Concerned);
            var embarrassed = library.Get(Emotion.Embarrassed);
            var shy = library.Get(Emotion.Shy);
            var flustered = library.Get(Emotion.Flustered);

            var sad = library.Get(Emotion.Sad);
            var surprised = library.Get(Emotion.Surprised);
            var thinking = library.Get(Emotion.Thinking);

            Assert.Greater(happy.Joy, 0.8f);
            Assert.Greater(happy.EyeSquint, 0.5f);
            Assert.Greater(happy.Aa, 0.3f);
            Assert.Greater(happy.Fun, 0.2f);
            Assert.AreEqual(0f, sad.Angry, 0.0001f);
            Assert.Greater(sad.Sorrow, 0.7f);
            Assert.Greater(sad.BrowInnerUp, 0.6f);
            Assert.Greater(sad.EyeLookDown, 0.4f);
            Assert.Greater(sad.EyeSquint, 0.1f);
            Assert.Greater(surprised.EyeWide, 0.9f);
            Assert.Greater(surprised.BrowInnerUp, 0.7f);
            Assert.Greater(surprised.BrowOuterUp, 0.7f);
            Assert.Greater(surprised.BrowSide, 0.7f);
            Assert.Greater(surprised.Aa, 0.2f);
            Assert.Less(surprised.Aa, 0.45f);
            Assert.Greater(surprised.Surprised, 0.8f);
            Assert.Greater(thinking.BrowSide, 0.6f);
            Assert.AreEqual(0f, thinking.BrowOuterUp, 0.0001f);
            Assert.AreEqual(0f, thinking.BrowInnerUp, 0.0001f);
            Assert.Greater(thinking.EyeLookUp, 0.3f);
            Assert.Greater(thinking.EyeLookSide, 0.2f);
            Assert.Greater(thinking.Ou, 0.3f);
            Assert.Greater(thinking.MouthPress, 0.3f);
            Assert.Greater(concerned.BrowInnerUp, concerned.EyeWide);
            Assert.Greater(concerned.BrowInnerUp, 0.7f);
            Assert.Greater(concerned.BrowInnerUp, sad.BrowInnerUp);
            Assert.Greater(sad.Sorrow, concerned.Sorrow);
            Assert.Greater(sad.EyeLookDown, concerned.EyeLookDown);
            Assert.AreEqual(0f, concerned.Blush, 0.0001f);
            Assert.AreEqual(0f, concerned.Surprised, 0.0001f);
            Assert.Greater(embarrassed.EyeLookDown, 0.5f);
            Assert.Greater(embarrassed.Blush, 0.8f);
            Assert.Greater(shy.EyeLookDown, 0.3f);
            Assert.Greater(flustered.Aa, 0.4f);
            Assert.Greater(flustered.Blush, 0.8f);
            Assert.AreEqual(0f, library.Get(Emotion.Neutral).Joy, 0.0001f);

            var fromFace = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            Assert.AreEqual(happy.Joy, fromFace.Joy, 0.0001f);
            Assert.AreEqual(happy.EyeSquint, fromFace.EyeSquint, 0.0001f);
            Assert.AreEqual(happy.Aa * 0.45f, EmotionFace.EmotionPose(Emotion.Happy, 0.45f).Aa, 0.0001f);
        }

        [Test]
        public void Parse_SkipsUnknownEmotionsAndChannels()
        {
            var library = ExpressionPresetLibrary.Parse(
                "{\"poses\":[{\"emotion\":\"shy\",\"weights\":[{\"channel\":\"blush\",\"weight\":0.2},{\"channel\":\"nope\",\"weight\":1}]}," +
                "{\"emotion\":\"not-an-emotion\",\"weights\":[{\"channel\":\"joy\",\"weight\":1}]}]}");

            Assert.AreEqual(0.2f, library.Get(Emotion.Shy).Blush, 0.0001f);
            Assert.AreEqual(0f, library.Get(Emotion.Shy).Joy, 0.0001f);
            Assert.AreEqual(0f, library.Get(Emotion.Happy).Joy, 0.0001f);
            Assert.AreEqual(default(FacePose).Aa, library.Get(Emotion.Happy).Aa, 0.0001f);

            ExpressionPresetLibrary.SetActiveForTests(library);
            Assert.AreEqual(0.2f, EmotionFace.EmotionPose(Emotion.Shy, 1f).Blush, 0.0001f);
            Assert.AreEqual(0f, EmotionFace.EmotionPose(Emotion.Happy, 1f).Joy, 0.0001f);
        }

        [Test]
        public void Classify_ResolvesVrmArkitAndDazNamesAndSkipsUnknown()
        {
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("Joy"));
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("Fcl_ALL_Joy"));
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("mouthSmileLeft"));
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("eCTRLMouthSmile"));
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("eCTRLSmile"));
            Assert.AreEqual(ExpressionPreset.EyeSquint, ExpressionPresetMap.Classify("eyeSquintLeft"));
            Assert.AreEqual(ExpressionPreset.EyeSquint, ExpressionPresetMap.Classify("eCTRLEyeSquintL"));
            Assert.AreEqual(ExpressionPreset.BrowInnerUp, ExpressionPresetMap.Classify("browInnerUp"));
            Assert.AreEqual(ExpressionPreset.BrowInnerUp, ExpressionPresetMap.Classify("eCTRLBrowsInnerUp"));
            Assert.AreEqual(ExpressionPreset.EyeLookDown, ExpressionPresetMap.Classify("eyeLookDown_L"));
            Assert.AreEqual(ExpressionPreset.EyeLookDown, ExpressionPresetMap.Classify("eCTRLEyeLookDown_L"));
            Assert.AreEqual(ExpressionPreset.Blink, ExpressionPresetMap.Classify("eCTRLEyesClosedL"));
            Assert.AreEqual(ExpressionPreset.Blink, ExpressionPresetMap.Classify("Blink"));
            Assert.AreEqual(ExpressionPreset.Aa, ExpressionPresetMap.Classify("eCTRLMouthOpen"));
            Assert.AreEqual(ExpressionPreset.Blush, ExpressionPresetMap.Classify("eCTRLBlush"));
            Assert.AreEqual(ExpressionPreset.Blush, ExpressionPresetMap.Classify("cheekBlush"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify("tongueOut"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify(""));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify(null));
            Assert.IsFalse(BlinkShapeSelection.IsBlinkName("eCTRLEyeLookDown_L"));
        }

        [Test]
        public void BlushAmount_IsStrongForEmbarrassedAndFlusteredAndLightForShy()
        {
            var embarrassed = EmotionFace.BlushAmount(Emotion.Embarrassed, 1f, true);
            var flustered = EmotionFace.BlushAmount(Emotion.Flustered, 1f, true);
            var shy = EmotionFace.BlushAmount(Emotion.Shy, 1f, true);
            var concerned = EmotionFace.BlushAmount(Emotion.Concerned, 1f, true);

            Assert.Greater(embarrassed, 0.8f);
            Assert.Greater(flustered, 0.8f);
            Assert.Greater(shy, 0.2f);
            Assert.Less(shy, 0.5f);
            Assert.Less(shy, embarrassed);
            Assert.AreEqual(0f, concerned, 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Sad, 1f, true), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Surprised, 1f, true), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Thinking, 1f, true), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Neutral, 1f, true), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Happy, 1f, true), 0.0001f);
            Assert.AreEqual(embarrassed * 0.5f, EmotionFace.BlushAmount(Emotion.Embarrassed, 0.5f, true), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Embarrassed, 1f, false), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.BlushAmount(Emotion.Flustered, 1f, false), 0.0001f);
        }

        [Test]
        public void LipSync_ReplacesMouthWeightsAndKeepsBlushBrowsAndBlink()
        {
            var flustered = EmotionFace.EmotionPose(Emotion.Flustered, 1f);
            Assert.Greater(flustered.Aa, 0.4f);
            var spoken = EmotionFace.OverrideMouth(flustered, new MouthWeights { A = 0.2f, O = 0.05f }, 1f);

            Assert.AreEqual(0.2f, spoken.Aa, 0.0001f);
            Assert.AreEqual(0.05f, spoken.Oh, 0.0001f);
            Assert.AreEqual(flustered.Blush, spoken.Blush, 0.0001f);
            Assert.AreEqual(flustered.Joy, spoken.Joy, 0.0001f);
            Assert.AreEqual(flustered.BrowInnerUp, spoken.BrowInnerUp, 0.0001f);
            Assert.AreEqual(flustered.EyeSquint, spoken.EyeSquint, 0.0001f);
            Assert.AreEqual(flustered.EyeLookDown, spoken.EyeLookDown, 0.0001f);
            Assert.AreEqual(flustered.EyeLookUp, spoken.EyeLookUp, 0.0001f);
            Assert.AreEqual(flustered.BrowSide, spoken.BrowSide, 0.0001f);
            Assert.AreEqual(flustered.MouthPress, spoken.MouthPress, 0.0001f);

            var thinking = EmotionFace.EmotionPose(Emotion.Thinking, 1f);
            var thinkingSpoken = EmotionFace.OverrideMouth(thinking, new MouthWeights { A = 0.2f, U = 0f }, 1f);
            Assert.AreEqual(0f, thinkingSpoken.Ou, 0.0001f);
            Assert.AreEqual(thinking.MouthPress, thinkingSpoken.MouthPress, 0.0001f);
            Assert.AreEqual(thinking.BrowSide, thinkingSpoken.BrowSide, 0.0001f);
            Assert.AreEqual(thinking.EyeLookUp, thinkingSpoken.EyeLookUp, 0.0001f);
            Assert.AreEqual(thinking.EyeLookSide, thinkingSpoken.EyeLookSide, 0.0001f);

            var held = EmotionFace.OverrideMouth(flustered, new MouthWeights { A = 0.2f }, 0f);
            Assert.AreEqual(flustered.Aa, held.Aa, 0.0001f);
            Assert.AreEqual(flustered.Blush, held.Blush, 0.0001f);

            var embarrassed = EmotionFace.EmotionPose(Emotion.Embarrassed, 1f);
            var blinked = EmotionFace.ApplyProceduralBlink(embarrassed, 1f);
            Assert.AreEqual(1f, blinked.Blink, 0.0001f);
            Assert.Greater(embarrassed.Blink, 0f);
            Assert.AreEqual(embarrassed.Blush, blinked.Blush, 0.0001f);
            Assert.AreEqual(embarrassed.Aa, blinked.Aa, 0.0001f);
            Assert.AreEqual(embarrassed.EyeLookDown, blinked.EyeLookDown, 0.0001f);
            Assert.AreEqual(embarrassed.BrowInnerUp, blinked.BrowInnerUp, 0.0001f);
        }

        [Test]
        public void CueParser_AcceptsTheNewEmotionsAndWorriedAlias()
        {
            var embarrassed = AnimationCueParser.Parse("Oh.\n<animation emotion=\"embarrassed\" intensity=\"0.8\" />");
            Assert.AreEqual(CueParseStatus.Parsed, embarrassed.Status);
            Assert.AreEqual(Emotion.Embarrassed, embarrassed.Cue.Emotion);
            Assert.AreEqual(0.8f, embarrassed.Cue.Intensity, 0.0001f);

            var worried = AnimationCueParser.Parse("Hmm.\n<animation emotion=\"worried\" intensity=\"0.4\" />");
            Assert.AreEqual(Emotion.Concerned, worried.Cue.Emotion);

            var fluster = AnimationCueParser.Parse("Wait.\n<animation emotion=\"flustered\" />");
            Assert.AreEqual(Emotion.Flustered, fluster.Cue.Emotion);

            var shy = AnimationCueParser.Parse("Um.\n<animation emotion=\"shy\" intensity=\"0.3\" />");
            Assert.AreEqual(Emotion.Shy, shy.Cue.Emotion);

            var smile = AnimationCueParser.Parse("Hi.\n<animation emotion=\"smile\" intensity=\"0.4\" />");
            Assert.AreEqual(Emotion.Happy, smile.Cue.Emotion);
        }

        [Test]
        public void BlushTexture_IsSoftPinkWithFaintHatchAndClearCorners()
        {
            var center = BlushTexture.Sample(BlushTexture.Size / 2, BlushTexture.Size / 2);
            var corner = BlushTexture.Sample(0, 0);

            Assert.Greater(center.r, center.b);
            Assert.Greater(center.r, center.g);
            Assert.Greater(center.a, 180);
            Assert.AreEqual(0, corner.a);
            Assert.Less(center.a, 250);

            var foundHatch = false;
            for (var y = 0; y < BlushTexture.Size && !foundHatch; y++)
            {
                for (var x = 0; x < BlushTexture.Size; x++)
                {
                    if ((x + y) % 6 != 0)
                        continue;
                    var hatch = BlushTexture.Sample(x, y);
                    if (hatch.a < 40)
                        continue;
                    var neighbor = BlushTexture.Sample(x, y + 1 < BlushTexture.Size ? y + 1 : y - 1);
                    if (neighbor.a < 40)
                        continue;
                    Assert.Less(hatch.g, neighbor.g);
                    Assert.Less(hatch.a, center.a);
                    foundHatch = true;
                    break;
                }
            }

            Assert.IsTrue(foundHatch);
        }

        [Test]
        public void BlushLog_NamesShapeOverlayOrSkip()
        {
            Assert.AreEqual("Blush: off", BlushOverlay.FormatLog(false, true, true, true));
            Assert.AreEqual("Blush: blendshape", BlushOverlay.FormatLog(true, true, true, false));
            Assert.AreEqual("Blush: no head bone, overlay skipped", BlushOverlay.FormatLog(true, false, false, false));
            Assert.AreEqual("Blush: no material, overlay skipped", BlushOverlay.FormatLog(true, false, true, false));
            Assert.AreEqual("Blush: procedural overlay", BlushOverlay.FormatLog(true, false, true, true));
        }

        [Test]
        public void Classify_ResolvesMmdVroidAndPrefixedNames()
        {
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("にこり"));
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("にっこり"));
            Assert.AreEqual(ExpressionPreset.EyeSquint, ExpressionPresetMap.Classify("笑い"));
            Assert.AreEqual(ExpressionPreset.BrowInnerUp, ExpressionPresetMap.Classify("眉困り"));
            Assert.AreEqual(ExpressionPreset.Sorrow, ExpressionPresetMap.Classify("困る"));
            Assert.AreEqual(ExpressionPreset.Angry, ExpressionPresetMap.Classify("怒り"));
            Assert.AreEqual(ExpressionPreset.Surprised, ExpressionPresetMap.Classify("びっくり"));
            Assert.AreEqual(ExpressionPreset.Blink, ExpressionPresetMap.Classify("まばたき左"));
            Assert.AreEqual(ExpressionPreset.Blush, ExpressionPresetMap.Classify("照れ"));
            Assert.AreEqual(ExpressionPreset.Aa, ExpressionPresetMap.Classify("あ"));
            Assert.AreEqual(ExpressionPreset.EyeLookDown, ExpressionPresetMap.Classify("下"));
            Assert.AreEqual(ExpressionPreset.Sorrow, ExpressionPresetMap.Classify("Fcl_BRW_Sorrow"));
            Assert.AreEqual(ExpressionPreset.Angry, ExpressionPresetMap.Classify("Fcl_MTH_Angry"));
            Assert.AreEqual(ExpressionPreset.EyeSquint, ExpressionPresetMap.Classify("Fcl_EYE_Joy"));
            Assert.AreEqual(ExpressionPreset.Surprised, ExpressionPresetMap.Classify("Fcl_EYE_Surprised"));
            Assert.AreEqual(ExpressionPreset.Joy, ExpressionPresetMap.Classify("M_Joy"));
            Assert.AreEqual(ExpressionPreset.Sorrow, ExpressionPresetMap.Classify("M_Sorrow"));
            Assert.AreEqual(ExpressionPreset.Sorrow, ExpressionPresetMap.Classify("mouthFrownLeft"));
            Assert.AreEqual(ExpressionPreset.BrowInnerUp, ExpressionPresetMap.Classify("browInnerUp"));
            Assert.AreEqual(ExpressionPreset.EyeLookDown, ExpressionPresetMap.Classify("eyesLookDown"));
            Assert.AreEqual(ExpressionPreset.Sorrow, ExpressionPresetMap.Classify("eCTRLFrown"));
            Assert.AreEqual(ExpressionPreset.Oh, ExpressionPresetMap.Classify("eCTRLMouthFunnel"));
            Assert.AreEqual(ExpressionPreset.BrowInnerUp, ExpressionPresetMap.Classify("eCTRLBrowsUp"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify("Fcl_HA_Fung1"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify("tongueOut"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify("dangerous"));
            Assert.AreEqual(ExpressionPreset.Fun, ExpressionPresetMap.Classify("cheekSquintLeft"));
            Assert.AreEqual(ExpressionPreset.Fun, ExpressionPresetMap.Classify("eCTRLCheekSquintL"));
            Assert.AreEqual(ExpressionPreset.EyeSquint, ExpressionPresetMap.Classify("eyeSquintLeft"));
            Assert.AreEqual(ExpressionPreset.BrowSide, ExpressionPresetMap.Classify("browOuterUpLeft"));
            Assert.AreEqual(ExpressionPreset.BrowOuterUp, ExpressionPresetMap.Classify("browOuterUpRight"));
            Assert.AreEqual(ExpressionPreset.BrowOuterUp, ExpressionPresetMap.Classify("browOuterUp"));
            Assert.AreEqual(ExpressionPreset.EyeLookUp, ExpressionPresetMap.Classify("eyeLookUpLeft"));
            Assert.AreEqual(ExpressionPreset.EyeLookSide, ExpressionPresetMap.Classify("eyeLookOutLeft"));
            Assert.AreEqual(ExpressionPreset.EyeLookSide, ExpressionPresetMap.Classify("eyeLookInRight"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify("eyeLookOutRight"));
            Assert.AreEqual(ExpressionPreset.None, ExpressionPresetMap.Classify("eyeLookInLeft"));
            Assert.AreEqual(ExpressionPreset.MouthPress, ExpressionPresetMap.Classify("mouthPressLeft"));
            Assert.AreEqual(ExpressionPreset.Ou, ExpressionPresetMap.Classify("mouthPucker"));
            Assert.IsFalse(BlinkShapeSelection.IsBlinkName("下"));
            Assert.IsFalse(BlinkShapeSelection.IsBlinkName("eyeLookDownLeft"));
        }

        [Test]
        public void FaceEmotion_PortraitNameWinsOverANeutralBodyEmotion()
        {
            Assert.AreEqual(Emotion.Embarrassed, FaceEmotion.Resolve("embarrassed", Emotion.Neutral));
            Assert.AreEqual(Emotion.Shy, FaceEmotion.Resolve("shy", Emotion.Neutral));
            Assert.AreEqual(Emotion.Flustered, FaceEmotion.Resolve("flustered", Emotion.Neutral));
            Assert.AreEqual(Emotion.Concerned, FaceEmotion.Resolve("worried", Emotion.Neutral));
            Assert.AreEqual(Emotion.Embarrassed, FaceEmotion.Resolve("embarrassed_worried", Emotion.Sad));
            Assert.AreEqual(Emotion.Embarrassed, FaceEmotion.Resolve("embarrassed-worried", Emotion.Sad));
            Assert.AreEqual(Emotion.Sad, FaceEmotion.Resolve("angry", Emotion.Neutral));
            Assert.AreEqual(Emotion.Sad, FaceEmotion.Resolve(string.Empty, Emotion.Sad));
            Assert.AreEqual(Emotion.Happy, FaceEmotion.Resolve("teasing", Emotion.Happy));
            Assert.AreEqual(Emotion.Surprised, FaceEmotion.Resolve(null, Emotion.Surprised));
            Assert.AreEqual("Expression: embarrassed", FaceEmotion.FormatLog(Emotion.Embarrassed));
            Assert.AreEqual("Expression: sad", FaceEmotion.FormatLog(Emotion.Sad));
        }

        [Test]
        public void Coverage_ReportsFoundAndMissingChannels()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/ExpressionPresets.json");
            var library = ExpressionPresetLibrary.Parse(File.ReadAllText(path));
            var bound = new HashSet<ExpressionPreset>
            {
                ExpressionPreset.Sorrow
            };

            Assert.AreEqual(
                "Expressions: sad -> 1/5 shapes found, missing blink, eyeSquint, browInnerUp, eyeLookDown",
                ExpressionCoverage.Format(Emotion.Sad, library.Get(Emotion.Sad), bound));

            bound.Add(ExpressionPreset.Blink);
            bound.Add(ExpressionPreset.EyeSquint);
            bound.Add(ExpressionPreset.BrowInnerUp);
            bound.Add(ExpressionPreset.EyeLookDown);
            Assert.AreEqual(
                "Expressions: sad -> 5/5 shapes found",
                ExpressionCoverage.Format(Emotion.Sad, library.Get(Emotion.Sad), bound));

            var names = new List<KeyValuePair<ExpressionPreset, string>>
            {
                new KeyValuePair<ExpressionPreset, string>(ExpressionPreset.Joy, "mouthSmileLeft"),
                new KeyValuePair<ExpressionPreset, string>(ExpressionPreset.Joy, "にこり"),
                new KeyValuePair<ExpressionPreset, string>(ExpressionPreset.Blush, "照れ")
            };
            Assert.AreEqual(
                "Expressions: bound joy=mouthSmileLeft, にこり; blush=照れ",
                ExpressionCoverage.FormatBound(names));
            Assert.AreEqual("Expressions: bound none", ExpressionCoverage.FormatBound(null));
            Assert.AreEqual(
                "Expressions: unclassified 4, sample tongueOut, hair",
                ExpressionCoverage.FormatUnclassified(4, new[] { "tongueOut", "hair" }));
        }

        [Test]
        public void ArkitNames_CoverEveryShippedExpression()
        {
            var names = new[]
            {
                "browDownLeft", "browDownRight", "browInnerUp", "browOuterUpLeft", "browOuterUpRight",
                "cheekSquintLeft", "cheekSquintRight",
                "eyeBlinkLeft", "eyeBlinkRight",
                "eyeLookDownLeft", "eyeLookDownRight",
                "eyeLookInLeft", "eyeLookInRight",
                "eyeLookOutLeft", "eyeLookOutRight",
                "eyeLookUpLeft", "eyeLookUpRight",
                "eyeSquintLeft", "eyeSquintRight",
                "eyeWideLeft", "eyeWideRight",
                "jawOpen", "mouthClose", "mouthFunnel", "mouthPucker",
                "mouthSmileLeft", "mouthSmileRight",
                "mouthFrownLeft", "mouthFrownRight",
                "mouthStretchLeft", "mouthPressLeft", "mouthPressRight",
                "tongueOut"
            };
            var bound = new HashSet<ExpressionPreset>();
            for (var i = 0; i < names.Length; i++)
            {
                var preset = ExpressionPresetMap.Classify(names[i]);
                if (preset != ExpressionPreset.None)
                    bound.Add(preset);
            }

            Assert.IsFalse(bound.Contains(ExpressionPreset.Blush));
            Assert.IsFalse(bound.Contains(ExpressionPreset.Surprised));
            Assert.IsTrue(bound.Contains(ExpressionPreset.Fun));
            Assert.IsTrue(bound.Contains(ExpressionPreset.BrowSide));
            Assert.IsTrue(bound.Contains(ExpressionPreset.EyeLookUp));
            Assert.IsTrue(bound.Contains(ExpressionPreset.EyeLookSide));
            Assert.IsTrue(bound.Contains(ExpressionPreset.MouthPress));

            var path = Path.Combine(Application.dataPath, "_Project/Resources/ExpressionPresets.json");
            var library = ExpressionPresetLibrary.Parse(File.ReadAllText(path));
            var emotions = new[]
            {
                Emotion.Happy, Emotion.Sad, Emotion.Surprised, Emotion.Thinking,
                Emotion.Embarrassed, Emotion.Shy, Emotion.Flustered, Emotion.Concerned
            };
            for (var i = 0; i < emotions.Length; i++)
            {
                var line = ExpressionCoverage.Format(emotions[i], library.Get(emotions[i]), bound);
                StringAssert.DoesNotContain("missing", line, line);
            }
        }

        [Test]
        public void ProjectOnto_FoldsMissingSurprisedAndFunOntoArkitShapes()
        {
            var surprised = EmotionFace.EmotionPose(Emotion.Surprised, 1f);
            var arkit = new HashSet<ExpressionPreset>
            {
                ExpressionPreset.EyeWide,
                ExpressionPreset.BrowInnerUp,
                ExpressionPreset.BrowOuterUp,
                ExpressionPreset.BrowSide,
                ExpressionPreset.Aa
            };
            var folded = EmotionFace.ProjectOnto(surprised, arkit);
            Assert.AreEqual(0f, folded.Surprised, 0.0001f);
            Assert.Greater(folded.EyeWide, 0.9f);
            Assert.Greater(folded.BrowInnerUp, 0.8f);
            Assert.Greater(folded.BrowOuterUp, 0.8f);
            Assert.Greater(folded.BrowSide, 0.8f);
            Assert.Greater(folded.Aa, 0.2f);

            var kept = EmotionFace.ProjectOnto(surprised, new HashSet<ExpressionPreset> { ExpressionPreset.Surprised });
            Assert.Greater(kept.Surprised, 0.8f);

            var happy = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            var noFun = EmotionFace.ProjectOnto(happy, new HashSet<ExpressionPreset>
            {
                ExpressionPreset.EyeSquint,
                ExpressionPreset.Joy
            });
            Assert.AreEqual(0f, noFun.Fun, 0.0001f);
            Assert.Greater(noFun.EyeSquint, happy.EyeSquint);

            var withFun = EmotionFace.ProjectOnto(happy, new HashSet<ExpressionPreset>
            {
                ExpressionPreset.Fun,
                ExpressionPreset.EyeSquint
            });
            Assert.AreEqual(happy.Fun, withFun.Fun, 0.0001f);
            Assert.AreEqual(happy.EyeSquint, withFun.EyeSquint, 0.0001f);
        }
    }
}
