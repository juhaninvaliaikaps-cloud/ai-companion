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
        public void Compress_IsLinearBelowTheKneeAndSofterAbove()
        {
            var tuning = LipSyncTuning.Default;
            Assert.AreEqual(0f, LipSyncMath.Sense(0.011f, tuning), 0.0001f);
            Assert.AreEqual(0f, LipSyncMath.Sense(float.NaN, tuning), 0.0001f);
            Assert.AreEqual(0.15f, LipSyncMath.Sense(0.05f, tuning), 0.0001f);

            var linear = LipSyncTuning.Default;
            linear.KneeRatio = 1f;
            Assert.AreEqual(0.8f, LipSyncMath.Compress(0.8f, linear), 0.0001f);

            var loud = LipSyncMath.Sense(0.5f, tuning);
            Assert.AreEqual(0.675f, loud, 0.0001f);
            Assert.Less(loud, 0.5f * tuning.Sensitivity);
            Assert.Less(loud, 1f);
            Assert.AreEqual(1f, LipSyncMath.Sense(10f, tuning), 0.0001f);
            Assert.AreEqual(0f, LipSyncMath.Compress(float.NaN, tuning), 0.0001f);
        }

        [Test]
        public void Advance_AttacksThenReleasesToRestAndHoldsTheViseme()
        {
            var tuning = LipSyncTuning.Default;
            var frame = new LipSyncFrame();

            frame = LipSyncMath.Advance(frame, 0.2f, MouthViseme.I, 0.01f, tuning);
            Assert.AreEqual(0.1125f, frame.Level, 0.0001f);
            Assert.Greater(frame.MouthOverride, 0.9f);
            Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
            Assert.Greater(frame.Weights.A, 0f);
            Assert.Less(frame.Weights.A, 0.1f);
            Assert.AreEqual(MouthViseme.I, frame.Viseme);

            frame = LipSyncMath.Advance(frame, 0.2f, MouthViseme.I, 1f, tuning);
            Assert.AreEqual(0.45f, frame.Level, 0.0001f);
            Assert.AreEqual(1f, frame.MouthOverride, 0.0001f);
            Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
            Assert.LessOrEqual(frame.Weights.A, tuning.MaxJawOpen);
            Assert.Greater(frame.Weights.A, 0.15f);

            frame = LipSyncMath.Advance(frame, 0f, MouthViseme.Rest, 0.08f, tuning);
            Assert.AreEqual(0.225f, frame.Level, 0.001f);
            Assert.AreEqual(MouthViseme.I, frame.Viseme);
            Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
            Assert.Greater(frame.Weights.A, 0.05f);

            for (var i = 0; i < 20; i++)
                frame = LipSyncMath.Advance(frame, 0f, MouthViseme.Rest, 0.1f, tuning);

            Assert.Less(frame.Level, 0.001f);
            Assert.Less(frame.MouthOverride, 0.001f);
            Assert.Less(frame.Weights.Opening, 0.001f);
            Assert.AreEqual(0f, frame.Weights.A, 0.0001f);
            Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
        }

        [Test]
        public void Advance_LoudSpeechStaysAModestJawAndDoesNotSealTheLips()
        {
            var tuning = LipSyncTuning.Default;
            var visemes = new[] { MouthViseme.A, MouthViseme.I, MouthViseme.U, MouthViseme.E, MouthViseme.O };
            for (var i = 0; i < visemes.Length; i++)
            {
                var frame = LipSyncMath.Advance(new LipSyncFrame(), 1f, visemes[i], 1f, tuning);
                Assert.LessOrEqual(frame.Weights.A, tuning.MaxJawOpen + 0.0001f);
                Assert.GreaterOrEqual(frame.Weights.A, 0.15f);
                Assert.Less(frame.Weights.A, 0.5f);
                Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
                Assert.LessOrEqual(frame.Weights.U, tuning.VowelAmount + 0.0001f);
                Assert.LessOrEqual(frame.Weights.O, tuning.VowelAmount + 0.0001f);
                Assert.LessOrEqual(frame.Weights.E, tuning.VowelAmount * LipSyncMath.StretchShare + 0.0001f);
                if (frame.Weights.U > 0f)
                    Assert.Less(frame.Weights.U, frame.Weights.A);
                if (frame.Weights.O > 0f)
                    Assert.Less(frame.Weights.O, frame.Weights.A);
                if (frame.Weights.E > 0f)
                    Assert.Less(frame.Weights.E, frame.Weights.A);
            }

            var open = LipSyncMath.Advance(new LipSyncFrame(), 0.5f, MouthViseme.A, 1f, tuning);
            var narrow = LipSyncMath.Advance(new LipSyncFrame(), 0.5f, MouthViseme.I, 1f, tuning);
            Assert.AreEqual(open.Weights.A * LipSyncMath.IJawScale, narrow.Weights.A, 0.0001f);
            Assert.AreEqual(0f, narrow.Weights.I, 0.0001f);

            var funnel = LipSyncMath.Advance(new LipSyncFrame(), 0.5f, MouthViseme.O, 1f, tuning);
            var stretch = LipSyncMath.Advance(new LipSyncFrame(), 0.5f, MouthViseme.E, 1f, tuning);
            Assert.AreEqual(tuning.VowelAmount, funnel.Weights.O, 0.0001f);
            Assert.Less(funnel.Weights.O, funnel.Weights.A);
            Assert.Less(stretch.Weights.E, funnel.Weights.O);
            Assert.AreEqual(0f, funnel.Weights.E, 0.0001f);
            Assert.AreEqual(0f, stretch.Weights.O, 0.0001f);

            tuning.VowelAmount = 0f;
            var jawOnly = LipSyncMath.Advance(new LipSyncFrame(), 0.5f, MouthViseme.O, 1f, tuning);
            Assert.Greater(jawOnly.Weights.A, 0.15f);
            Assert.AreEqual(0f, jawOnly.Weights.O, 0.0001f);
            Assert.AreEqual(0f, jawOnly.Weights.U, 0.0001f);
            Assert.AreEqual(0f, jawOnly.Weights.E, 0.0001f);
        }

        [Test]
        public void Advance_OneFrameVisemeBlipDoesNotSwitchTheShape()
        {
            var tuning = LipSyncTuning.Default;
            var frame = LipSyncMath.Advance(new LipSyncFrame(), 0.2f, MouthViseme.O, 1f, tuning);
            var opened = frame.Weights.O;
            Assert.Greater(opened, 0.1f);
            Assert.AreEqual(MouthViseme.O, frame.Viseme);

            frame = LipSyncMath.Advance(frame, 0.2f, MouthViseme.E, 0.016f, tuning);
            Assert.AreEqual(MouthViseme.O, frame.Viseme);
            Assert.AreEqual(0f, frame.Weights.E, 0.001f);
            Assert.Greater(frame.Weights.O, opened * 0.5f);

            var switched = false;
            for (var i = 0; i < 8; i++)
            {
                frame = LipSyncMath.Advance(frame, 0.2f, MouthViseme.I, 0.016f, tuning);
                if (frame.Viseme != MouthViseme.I)
                    continue;
                switched = true;
                break;
            }

            Assert.IsTrue(switched);
            Assert.AreEqual(0f, frame.Weights.I, 0.0001f);
            Assert.Greater(frame.Weights.O, 0f);
            Assert.Less(frame.Weights.O, opened);
        }

        [Test]
        public void Advance_ZeroDeltaDoesNotMoveAndStrengthZeroStaysShut()
        {
            var tuning = LipSyncTuning.Default;
            var held = LipSyncMath.Advance(new LipSyncFrame(), 0.2f, MouthViseme.A, 0f, tuning);
            Assert.AreEqual(0f, held.Level, 0.0001f);
            Assert.AreEqual(0f, held.Weights.Opening, 0.0001f);

            tuning.Strength = 0f;
            var shut = LipSyncMath.Advance(new LipSyncFrame(), 0.2f, MouthViseme.E, 1f, tuning);
            Assert.AreEqual(0.45f, shut.Level, 0.0001f);
            Assert.AreEqual(0f, shut.MouthOverride, 0.0001f);
            Assert.AreEqual(0f, shut.Weights.Opening, 0.0001f);
        }

        [Test]
        public void JawPitch_UsesTheModestOpenAsFullTravel()
        {
            var tuning = LipSyncTuning.Default;
            var full = new MouthWeights { A = tuning.MaxJawOpen };
            Assert.AreEqual(-12f, LipSyncMath.JawPitchDegrees(full, tuning, -12f), 0.0001f);

            var half = new MouthWeights { A = tuning.MaxJawOpen * 0.5f };
            Assert.AreEqual(-6f, LipSyncMath.JawPitchDegrees(half, tuning, -12f), 0.0001f);

            var hot = new MouthWeights { A = 1f };
            Assert.AreEqual(-12f, LipSyncMath.JawPitchDegrees(hot, tuning, -12f), 0.0001f);
            Assert.AreEqual(0f, LipSyncMath.JawPitchDegrees(default, tuning, -12f), 0.0001f);
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

            var tuned = LipSyncTuning.Default.AppendDiagnostic(line);
            StringAssert.StartsWith("LipSync:", tuned);
            StringAssert.Contains("strength 0.55", tuned);
            StringAssert.Contains("sensitivity 3.00", tuned);
            StringAssert.Contains("jaw 0.40", tuned);
            StringAssert.Contains("vowel 0.20", tuned);
            StringAssert.DoesNotContain("hello", tuned);
        }

        [Test]
        public void OverrideMouth_ReplacesOnlyMouthShapesAndProceduralTalkYields()
        {
            var surprised = EmotionFace.EmotionPose(Emotion.Surprised, 1f);
            var speech = new MouthWeights { A = 0.3f, O = 0.12f, I = 0.8f };
            var mixed = EmotionFace.OverrideMouth(surprised, speech, 0.8f);

            Assert.AreEqual(1f, mixed.Surprised, 0.0001f);
            Assert.AreEqual(0f, mixed.Joy, 0.0001f);
            Assert.AreEqual(0.33f, mixed.Aa, 0.0001f);
            Assert.AreEqual(0f, mixed.Ih, 0.0001f);
            Assert.AreEqual(0.156f, mixed.Oh, 0.0001f);
            Assert.LessOrEqual(mixed.Aa, surprised.Aa);
            Assert.LessOrEqual(mixed.Oh, surprised.Oh);

            var unchanged = EmotionFace.OverrideMouth(surprised, speech, 0f);
            Assert.AreEqual(surprised.Aa, unchanged.Aa, 0.0001f);
            Assert.AreEqual(0f, unchanged.Ih, 0.0001f);

            var thinking = EmotionFace.EmotionPose(Emotion.Thinking, 1f);
            var open = EmotionFace.OverrideMouth(thinking, new MouthWeights { A = 0.3f }, 0.5f);
            Assert.AreEqual(0.15f, open.Aa, 0.0001f);
            Assert.AreEqual(0f, open.Ih, 0.0001f);
            Assert.AreEqual(thinking.Blink, open.Blink, 0.0001f);

            var early = EmotionFace.OverrideMouth(thinking, new MouthWeights { A = 0.02f }, 0.2f);
            Assert.AreEqual(0.004f, early.Aa, 0.0001f);
            Assert.AreEqual(0.28f, early.Ih, 0.0001f);

            var happy = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            var talking = EmotionFace.OverrideMouth(happy, new MouthWeights { A = 0.35f }, 1f);
            Assert.AreEqual(1f, talking.Joy, 0.0001f);
            Assert.AreEqual(happy.Fun, talking.Fun, 0.0001f);
            Assert.AreEqual(0.35f, talking.Aa, 0.0001f);
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
