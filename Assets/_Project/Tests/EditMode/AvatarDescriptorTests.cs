using System.Collections.Generic;
using AICompanion.Avatar;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class AvatarDescriptorTests
    {
        [Test]
        public void Map_MatchesVrmPresetsAndVroidNames()
        {
            var map = ExpressionPresetMap.Map(new[]
            {
                "aa", "ih", "ou", "ee", "oh", "blink",
                "happy", "angry", "sad", "relaxed",
                "Fcl_MTH_A", "Fcl_EYE_Close", "Fcl_ALL_Joy"
            });

            CollectionAssert.AreEqual(new[] { "aa", "Fcl_MTH_A" }, map[ExpressionPreset.Aa]);
            CollectionAssert.AreEqual(new[] { "ih" }, map[ExpressionPreset.Ih]);
            CollectionAssert.AreEqual(new[] { "ou" }, map[ExpressionPreset.Ou]);
            CollectionAssert.AreEqual(new[] { "ee" }, map[ExpressionPreset.Ee]);
            CollectionAssert.AreEqual(new[] { "oh" }, map[ExpressionPreset.Oh]);
            CollectionAssert.AreEqual(new[] { "blink", "Fcl_EYE_Close" }, map[ExpressionPreset.Blink]);
            CollectionAssert.AreEqual(new[] { "happy", "Fcl_ALL_Joy" }, map[ExpressionPreset.Joy]);
            CollectionAssert.AreEqual(new[] { "angry" }, map[ExpressionPreset.Angry]);
            CollectionAssert.AreEqual(new[] { "sad" }, map[ExpressionPreset.Sorrow]);
            CollectionAssert.AreEqual(new[] { "relaxed" }, map[ExpressionPreset.Fun]);
        }

        [Test]
        public void Map_MatchesArkitVisemesBlinkAndEmotions()
        {
            var names = new[]
            {
                "jawOpen", "mouthClose", "mouthPucker", "mouthStretchLeft", "mouthFunnel",
                "eyeBlinkLeft", "eyeBlinkRight",
                "mouthSmileRight", "browDownLeft", "mouthFrownLeft",
                "tongueOut", "browInnerUp", "eyeLookDownLeft"
            };

            var map = ExpressionPresetMap.Map(names);

            CollectionAssert.AreEqual(new[] { "jawOpen" }, map[ExpressionPreset.Aa]);
            CollectionAssert.AreEqual(new[] { "mouthClose" }, map[ExpressionPreset.Ih]);
            CollectionAssert.AreEqual(new[] { "mouthPucker" }, map[ExpressionPreset.Ou]);
            CollectionAssert.AreEqual(new[] { "mouthStretchLeft" }, map[ExpressionPreset.Ee]);
            CollectionAssert.AreEqual(new[] { "mouthFunnel" }, map[ExpressionPreset.Oh]);
            CollectionAssert.AreEqual(new[] { "eyeBlinkLeft", "eyeBlinkRight" }, map[ExpressionPreset.Blink]);
            CollectionAssert.AreEqual(new[] { "mouthSmileRight" }, map[ExpressionPreset.Joy]);
            CollectionAssert.AreEqual(new[] { "browDownLeft" }, map[ExpressionPreset.Angry]);
            CollectionAssert.AreEqual(new[] { "mouthFrownLeft" }, map[ExpressionPreset.Sorrow]);
            Assert.IsFalse(map.ContainsKey(ExpressionPreset.Fun));

            var descriptor = AvatarDescriptor.Create("Asuna", HumanoidReport.Valid(), names);
            CollectionAssert.Contains(descriptor.BlendShapeNames, "tongueOut");
            Assert.IsFalse(descriptor.TryGetExpression(ExpressionPreset.Fun, out _));
            StringAssert.Contains("jawOpen", descriptor.Summary());
        }

        [Test]
        public void WithExpressions_AddsVrmClipShapeWithoutDroppingNameMatches()
        {
            var descriptor = AvatarDescriptor.Create("model", HumanoidReport.Valid(), new[] { "aa" });
            var merged = descriptor.WithExpressions(new[]
            {
                new KeyValuePair<ExpressionPreset, string>(ExpressionPreset.Aa, "customMouth")
            });

            CollectionAssert.AreEqual(new[] { "aa", "customMouth" }, merged.Expressions[ExpressionPreset.Aa]);
        }

        [Test]
        public void ValidateBoneNames_AcceptsRequiredSetAndMixamoAliases()
        {
            var bones = new List<string>(HumanoidValidation.RequiredBoneNames);
            bones.Remove("LeftUpperArm");
            bones.Add("mixamorig:LeftArm");
            bones.Add("Chest");

            var report = HumanoidValidation.ValidateBoneNames(bones);

            Assert.IsTrue(report.IsValid);
            StringAssert.Contains("ready for shared animations", report.Message);
        }

        [Test]
        public void ValidateBoneNames_ReportsMissingRequiredBones()
        {
            var bones = new List<string>(HumanoidValidation.RequiredBoneNames);
            bones.Remove("LeftHand");
            bones.Remove("RightFoot");

            var report = HumanoidValidation.ValidateBoneNames(bones);

            Assert.IsFalse(report.IsValid);
            CollectionAssert.AreEqual(new[] { "LeftHand", "RightFoot" }, report.MissingRequired);
            StringAssert.Contains("Missing required bones: LeftHand, RightFoot", report.Message);
            StringAssert.Contains("Mixamo", report.Message);
        }
    }
}
