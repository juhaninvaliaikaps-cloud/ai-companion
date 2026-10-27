using AICompanion.Avatar;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class LipSyncTests
    {
        const float Hz = 50f;

        [Test]
        public void Rms_IsZeroForSilenceAndMatchesAConstantSignal()
        {
            Assert.AreEqual(0f, LipSyncMath.Rms(null), 0.0001f);
            Assert.AreEqual(0f, LipSyncMath.Rms(new[] { 0f, 0f, 0f }), 0.0001f);
            Assert.AreEqual(0.5f, LipSyncMath.Rms(new[] { 0.5f, -0.5f }), 0.0001f);
            Assert.AreEqual(1f, LipSyncMath.Rms(new[] { 1f, -1f, 1f, -1f }), 0.0001f);
            Assert.AreEqual(0.5f, LipSyncMath.Rms(new[] { 0.5f, float.NaN }), 0.0001f);
        }

        [Test]
        public void Estimate_MapsFormantBandsAndFallsBackToA()
        {
            var tuning = LipSyncTuning.Default;
            Assert.AreEqual(MouthViseme.Rest, LipSyncMath.Estimate(Band(LipSyncMath.IStartHz, LipSyncMath.IEndHz), Hz, 0.001f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(null, Hz, 0.2f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(new float[0], Hz, 0.2f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(new float[100], Hz, 0.2f, tuning.SilenceThreshold));

            Assert.AreEqual(MouthViseme.U, LipSyncMath.Estimate(Band(LipSyncMath.UStartHz, LipSyncMath.UEndHz), Hz, 0.2f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.O, LipSyncMath.Estimate(Band(LipSyncMath.OStartHz, LipSyncMath.OEndHz), Hz, 0.2f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(Band(LipSyncMath.AStartHz, LipSyncMath.AEndHz), Hz, 0.2f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.E, LipSyncMath.Estimate(Band(LipSyncMath.EStartHz, LipSyncMath.EEndHz), Hz, 0.2f, tuning.SilenceThreshold));
            Assert.AreEqual(MouthViseme.I, LipSyncMath.Estimate(Band(LipSyncMath.IStartHz, LipSyncMath.IEndHz), Hz, 0.2f, tuning.SilenceThreshold));

            var flat = new float[100];
            for (var i = 0; i < flat.Length; i++)
                flat[i] = 0.5f;
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(flat, Hz, 0.2f, tuning.SilenceThreshold));

            var tied = Band(LipSyncMath.UStartHz, LipSyncMath.UEndHz);
            Add(tied, LipSyncMath.OStartHz, LipSyncMath.OEndHz, 1f);
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(tied, Hz, 0.2f, tuning.SilenceThreshold));

            var close = Band(LipSyncMath.IStartHz, LipSyncMath.IEndHz);
            Add(close, LipSyncMath.AStartHz, LipSyncMath.AEndHz, 0.9f);
            Assert.AreEqual(MouthViseme.A, LipSyncMath.Estimate(close, Hz, 0.2f, tuning.SilenceThreshold));
        }

        [Test]
        public void Advance_AttacksFastReleasesToRestAndHoldsTheViseme()
        {
            var tuning = LipSyncTuning.Default;
            var frame = new LipSyncFrame();

            frame = LipSyncMath.Advance(frame, 0.2f, MouthViseme.I, 0.01f, tuning);
            Assert.AreEqual(0.5f, frame.Level, 0.0001f);
            Assert.AreEqual(0.5f, frame.MouthOverride, 0.0001f);
            Assert.AreEqual(0.5f, frame.Weights.I, 0.0001f);
            Assert.AreEqual(0f, frame.Weights.A, 0.0001f);
            Assert.AreEqual(MouthViseme.I, frame.Viseme);

            frame = LipSyncMath.Advance(frame, 0.2f, MouthViseme.I, 0.02f, tuning);
            Assert.AreEqual(1f, frame.Level, 0.0001f);
            Assert.AreEqual(1f, frame.Weights.I, 0.0001f);

            frame = LipSyncMath.Advance(frame, 0f, MouthViseme.Rest, 0.05f, tuning);
            Assert.AreEqual(0.5f, frame.Level, 0.0001f);
            Assert.AreEqual(MouthViseme.I, frame.Viseme);
            Assert.AreEqual(0.5f, frame.Weights.I, 0.0001f);
            Assert.AreEqual(0f, frame.Weights.A, 0.0001f);
            Assert.Greater(frame.MouthOverride, 0.4f);

            for (var i = 0; i < 12; i++)
                frame = LipSyncMath.Advance(frame, 0f, MouthViseme.Rest, 0.1f, tuning);

            Assert.Less(frame.Level, 0.001f);
            Assert.Less(frame.MouthOverride, 0.001f);
            Assert.Less(frame.Weights.Opening, 0.001f);
            Assert.AreEqual(0f, frame.Weights.A, 0.0001f);
            Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
        }

        [Test]
        public void Advance_ZeroDeltaDoesNotMoveAndStrengthZeroStaysShut()
        {
            var tuning = LipSyncTuning.Default;
            var held = LipSyncMath.Advance(new LipSyncFrame(), 0.2f, MouthViseme.A, 0f, tuning);
            Assert.AreEqual(0f, held.Level, 0.0001f);

            tuning.Strength = 0f;
            var shut = LipSyncMath.Advance(new LipSyncFrame(), 0.2f, MouthViseme.E, 1f, tuning);
            Assert.AreEqual(1f, shut.Level, 0.0001f);
            Assert.AreEqual(0f, shut.MouthOverride, 0.0001f);
            Assert.AreEqual(0f, shut.Weights.Opening, 0.0001f);
        }

        [Test]
        public void Sense_UsesSensitivityAndTheSilenceFloor()
        {
            var tuning = LipSyncTuning.Default;
            Assert.AreEqual(0f, LipSyncMath.Sense(0.011f, tuning), 0.0001f);
            Assert.AreEqual(0f, LipSyncMath.Sense(float.NaN, tuning), 0.0001f);
            Assert.AreEqual(0.4f, LipSyncMath.Sense(0.05f, tuning), 0.0001f);
            Assert.AreEqual(1f, LipSyncMath.Sense(0.5f, tuning), 0.0001f);

            var open = LipSyncMath.Advance(new LipSyncFrame(), 0.05f, MouthViseme.O, 1f, tuning);
            Assert.AreEqual(0.4f, open.Weights.O, 0.0001f);
            Assert.AreEqual(0f, open.Weights.A, 0.0001f);
            Assert.AreEqual(0.4f, open.MouthOverride, 0.0001f);
        }

        [Test]
        public void Choose_PrefersVrmThenBlendShapesThenJaw()
        {
            Assert.AreEqual(LipSyncTargetKind.Vrm, LipSyncTargetSelection.Choose(true, 4, true));
            Assert.AreEqual(LipSyncTargetKind.BlendShapes, LipSyncTargetSelection.Choose(false, 2, true));
            Assert.AreEqual(LipSyncTargetKind.JawBone, LipSyncTargetSelection.Choose(false, 0, true));
            Assert.AreEqual(LipSyncTargetKind.None, LipSyncTargetSelection.Choose(false, 0, false));

            Assert.IsTrue(LipSyncTargetSelection.DrivesSharedMouth(LipSyncTargetKind.Vrm));
            Assert.IsTrue(LipSyncTargetSelection.DrivesSharedMouth(LipSyncTargetKind.BlendShapes));
            Assert.IsTrue(LipSyncTargetSelection.DrivesSharedMouth(LipSyncTargetKind.Custom));
            Assert.IsFalse(LipSyncTargetSelection.DrivesSharedMouth(LipSyncTargetKind.JawBone));
            Assert.IsFalse(LipSyncTargetSelection.DrivesSharedMouth(LipSyncTargetKind.None));
        }

        [Test]
        public void MouthNames_MatchAsunaVisemesAndLeaveEmotionShapesAlone()
        {
            var names = new[]
            {
                "jawOpen",
                "mouthClose",
                "mouthFunnel",
                "mouthPucker",
                "mouthStretchLeft",
                "mouthStretchRight",
                "mouthSmileLeft",
                "mouthFrownRight",
                "jawLeft",
                "jawRight",
                "jawForward",
                "eyeBlinkLeft",
                "Jaw"
            };

            var hits = LipSyncTargetSelection.Collect(names);

            Assert.AreEqual(7, hits.Length);
            Assert.AreEqual(ExpressionPreset.Aa, hits[0].Preset);
            Assert.AreEqual("jawOpen", hits[0].Name);
            Assert.AreEqual(ExpressionPreset.Ih, hits[1].Preset);
            Assert.AreEqual(ExpressionPreset.Oh, hits[2].Preset);
            Assert.AreEqual(ExpressionPreset.Ou, hits[3].Preset);
            Assert.AreEqual(ExpressionPreset.Ee, hits[4].Preset);
            Assert.AreEqual(ExpressionPreset.Ee, hits[5].Preset);
            Assert.AreEqual("Jaw", hits[6].Name);
            Assert.AreEqual(ExpressionPreset.Aa, hits[6].Preset);

            Assert.IsFalse(LipSyncTargetSelection.TryClassifyMouth("mouthSmileLeft", out _));
            Assert.IsFalse(LipSyncTargetSelection.TryClassifyMouth("browDownLeft", out _));
            Assert.IsTrue(LipSyncTargetSelection.TryClassifyMouth("viseme_U", out var viseme));
            Assert.AreEqual(ExpressionPreset.Ou, viseme);
        }

        [Test]
        public void JawBoneNames_AcceptTheHumanoidJawAndRejectSideJoints()
        {
            Assert.IsTrue(LipSyncTargetSelection.IsJawBoneName("Jaw"));
            Assert.IsTrue(LipSyncTargetSelection.IsJawBoneName("mixamorig:Jaw"));
            Assert.IsFalse(LipSyncTargetSelection.IsJawBoneName("jawLeft"));
            Assert.IsFalse(LipSyncTargetSelection.IsJawBoneName("jawRight"));
            Assert.IsFalse(LipSyncTargetSelection.IsJawBoneName("jawForward"));
            Assert.IsFalse(LipSyncTargetSelection.IsJawBoneName("Head"));
        }

        [Test]
        public void Diagnostic_NamesTheModeAndDoesNotIncludeSpeech()
        {
            Assert.AreEqual("LipSync: none", LipSyncTargetSelection.FormatDiagnostic(LipSyncTargetKind.None, null));
            Assert.AreEqual("LipSync: VRM expressions Aa, Ih", LipSyncTargetSelection.FormatDiagnostic(LipSyncTargetKind.Vrm, "expressions Aa, Ih"));
            Assert.AreEqual("LipSync: blendshapes jawOpen, mouthFunnel", LipSyncTargetSelection.FormatDiagnostic(LipSyncTargetKind.BlendShapes, "jawOpen, mouthFunnel"));
            Assert.AreEqual("LipSync: jaw bone Jaw", LipSyncTargetSelection.FormatDiagnostic(LipSyncTargetKind.JawBone, "Jaw"));
            Assert.AreEqual("LipSync: Daz face", LipSyncTargetSelection.FormatDiagnostic(LipSyncTargetKind.Custom, "Daz face"));
            Assert.AreEqual("Aa, Oh", LipSyncTargetSelection.VrmList(true, false, false, false, true));

            var line = LipSyncTargetSelection.FormatDiagnostic(LipSyncTargetKind.BlendShapes, "jawOpen, mouthClose");
            StringAssert.StartsWith("LipSync:", line);
            StringAssert.DoesNotContain("hello", line);
        }

        [Test]
        public void OverrideMouth_ReplacesOnlyMouthShapesAndProceduralTalkYields()
        {
            var surprised = EmotionFace.EmotionPose(Emotion.Surprised, 1f);
            var speech = MouthWeights.For(MouthViseme.I, 0.8f);
            var mixed = EmotionFace.OverrideMouth(surprised, speech, 0.8f);

            Assert.AreEqual(1f, mixed.Surprised, 0.0001f);
            Assert.AreEqual(0f, mixed.Joy, 0.0001f);
            Assert.AreEqual(0.8f, mixed.Ih, 0.0001f);
            Assert.AreEqual(surprised.Aa * 0.2f, mixed.Aa, 0.0001f);
            Assert.AreEqual(surprised.Oh * 0.2f, mixed.Oh, 0.0001f);

            var unchanged = EmotionFace.OverrideMouth(surprised, speech, 0f);
            Assert.AreEqual(surprised.Aa, unchanged.Aa, 0.0001f);
            Assert.AreEqual(0f, unchanged.Ih, 0.0001f);

            var happy = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            var talking = EmotionFace.OverrideMouth(happy, MouthWeights.For(MouthViseme.A, 1f), 1f);
            Assert.AreEqual(1f, talking.Joy, 0.0001f);
            Assert.AreEqual(happy.Fun, talking.Fun, 0.0001f);
            Assert.AreEqual(1f, talking.Aa, 0.0001f);
            Assert.AreEqual(0f, talking.Ih, 0.0001f);

            Assert.AreEqual(1f, EmotionFace.ProceduralTalkScale(1f, 0f), 0.0001f);
            Assert.AreEqual(0.75f, EmotionFace.ProceduralTalkScale(1f, 0.25f), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.ProceduralTalkScale(1f, 1f), 0.0001f);
            Assert.AreEqual(0f, EmotionFace.ProceduralTalkScale(0f, 0.5f), 0.0001f);
        }

        static float[] Band(float startHz, float endHz)
        {
            var spectrum = new float[100];
            Add(spectrum, startHz, endHz, 1f);
            return spectrum;
        }

        static void Add(float[] spectrum, float startHz, float endHz, float magnitude)
        {
            for (var i = 0; i < spectrum.Length; i++)
            {
                var hz = i * Hz;
                if (hz >= startHz && hz < endHz)
                    spectrum[i] = magnitude;
            }
        }
    }
}
