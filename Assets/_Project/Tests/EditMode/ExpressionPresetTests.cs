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

            Assert.Greater(happy.Joy, 0.8f);
            Assert.Greater(happy.EyeSquint, 0.5f);
            Assert.Greater(happy.Aa, 0.3f);
            Assert.Greater(concerned.BrowInnerUp, concerned.EyeWide);
            Assert.Greater(concerned.BrowInnerUp, 0.7f);
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
            Assert.Less(concerned, embarrassed);
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
    }
}
