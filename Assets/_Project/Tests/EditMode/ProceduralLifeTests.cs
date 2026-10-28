using System.IO;
using AICompanion.Avatar;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class ProceduralLifeTests
    {
        [Test]
        public void BlinkGaps_StayInsideTheRandomBounds()
        {
            Assert.AreEqual(BlinkTiming.MinGapSeconds, BlinkTiming.GapSeconds(0f, 1f), 0.0001f);
            Assert.AreEqual(BlinkTiming.MaxGapSeconds, BlinkTiming.GapSeconds(1f, 1f), 0.0001f);
            Assert.AreEqual(BlinkTiming.MaxGapSeconds, BlinkTiming.GapSeconds(0f, 0f), 0.0001f);
            Assert.AreEqual(BlinkTiming.LongMaxSeconds, BlinkTiming.GapSeconds(1f, 0f), 0.0001f);
            Assert.IsFalse(BlinkTiming.IsDouble(float.NaN));
            Assert.IsTrue(BlinkTiming.IsDouble(0f));
            Assert.IsFalse(BlinkTiming.IsDouble(BlinkTiming.DoubleChance));

            for (var gate = 0; gate <= 10; gate++)
            {
                for (var unit = 0; unit <= 10; unit++)
                {
                    var gap = BlinkTiming.GapSeconds(unit / 10f, gate / 10f);
                    Assert.GreaterOrEqual(gap, BlinkTiming.MinGapSeconds - 0.0001f);
                    Assert.LessOrEqual(gap, BlinkTiming.LongMaxSeconds + 0.0001f);
                }
            }
        }

        [Test]
        public void BlinkCurve_ClosesQuicklyOpensAndCanDouble()
        {
            Assert.AreEqual(0f, BlinkTiming.Sample(0f, false), 0.0001f);
            Assert.AreEqual(0f, BlinkTiming.Sample(BlinkTiming.SingleDuration, false), 0.0001f);
            Assert.Greater(BlinkTiming.Sample(BlinkTiming.CloseSeconds * 0.5f, false), 0.8f);
            Assert.AreEqual(1f, BlinkTiming.Sample(BlinkTiming.CloseSeconds + BlinkTiming.HoldSeconds * 0.5f, false), 0.0001f);

            var peak = 0f;
            var steps = 40;
            for (var i = 0; i <= steps; i++)
            {
                var weight = BlinkTiming.Sample(BlinkTiming.SingleDuration * i / steps, false);
                Assert.GreaterOrEqual(weight, 0f);
                Assert.LessOrEqual(weight, 1f);
                if (weight > peak)
                    peak = weight;
            }

            Assert.Greater(peak, 0.95f);

            var one = BlinkTiming.SingleDuration;
            var gapTime = one + BlinkTiming.DoubleGapSeconds * 0.5f;
            Assert.AreEqual(0f, BlinkTiming.Sample(gapTime, true), 0.0001f);
            Assert.Greater(BlinkTiming.Sample(BlinkTiming.CloseSeconds + 0.01f, true), 0.9f);
            Assert.Greater(BlinkTiming.Sample(one + BlinkTiming.DoubleGapSeconds + BlinkTiming.CloseSeconds + 0.01f, true), 0.9f);

            var scheduler = BlinkScheduler.Start(0f, 1f);
            Assert.AreEqual(BlinkTiming.MinGapSeconds, scheduler.Wait, 0.0001f);
            var sawBlink = false;
            for (var i = 0; i < 80; i++)
            {
                scheduler = scheduler.Tick(0.1f, 0f, 1f, 0.9f);
                Assert.GreaterOrEqual(scheduler.Weight, 0f);
                Assert.LessOrEqual(scheduler.Weight, 1f);
                if (scheduler.Weight > 0.5f)
                    sawBlink = true;
            }

            Assert.IsTrue(sawBlink);

            var doubled = BlinkScheduler.Start(0f, 1f);
            var guard = 0;
            while (!doubled.Active && guard < 40)
            {
                doubled = doubled.Tick(0.1f, 0.2f, 1f, 0f);
                guard++;
            }

            var peaks = 0;
            var wasUp = false;
            for (var i = 0; i < 40; i++)
            {
                doubled = doubled.Tick(0.02f, 0.2f, 1f, 0f);
                var up = doubled.Weight > 0.8f;
                if (up && !wasUp)
                    peaks++;
                wasUp = up;
            }

            Assert.GreaterOrEqual(peaks, 2);
        }

        [Test]
        public void BlinkNames_FindCommonShapesAndSkipAMissingOne()
        {
            var names = BlinkShapeSelection.Collect(
                new[] { "JawOpen", "Blink", "eyeBlink_L", "eyeBlink_R", "EyeClose", "Joy" },
                new[] { "customBlink", "Blink" });

            CollectionAssert.AreEqual(
                new[] { "Blink", "eyeBlink_L", "eyeBlink_R", "EyeClose", "customBlink" },
                names);
            Assert.AreEqual("ProceduralLife: blink blendshapes Blink, eyeBlink_L, eyeBlink_R, EyeClose, customBlink", BlinkShapeSelection.FormatLog(names));
            Assert.AreEqual(ExpressionPreset.Blink, ExpressionPresetMap.Classify("EyeClose"));
            Assert.AreEqual(ExpressionPreset.Blink, ExpressionPresetMap.Classify("Fcl_EYE_Close"));
            Assert.AreEqual(ExpressionPreset.Blink, ExpressionPresetMap.Classify("eyeBlinkLeft"));
            Assert.AreEqual(ExpressionPreset.EyeLookDown, ExpressionPresetMap.Classify("eyeLookDownLeft"));
            Assert.IsFalse(BlinkShapeSelection.IsBlinkName("eyeLookDownLeft"));

            var none = BlinkShapeSelection.Collect(new[] { "jawOpen", "mouthSmile" }, null);
            Assert.AreEqual(0, none.Length);
            Assert.AreEqual("ProceduralLife: no blink blendshape, blinking skipped", BlinkShapeSelection.FormatLog(none));
            Assert.AreEqual("ProceduralLife: blink blendshape Blink", BlinkShapeSelection.FormatLog(new[] { "Blink" }));
        }

        [Test]
        public void ProceduralBlink_DoesNotReplaceEmotionOrMouth()
        {
            var thinking = EmotionFace.EmotionPose(Emotion.Thinking, 1f);
            var held = EmotionFace.ApplyProceduralBlink(thinking, 0f);
            Assert.AreEqual(0.15f, held.Blink, 0.0001f);
            Assert.AreEqual(thinking.Ih, held.Ih, 0.0001f);

            var closed = EmotionFace.ApplyProceduralBlink(thinking, 1f);
            Assert.AreEqual(1f, closed.Blink, 0.0001f);
            Assert.AreEqual(thinking.Ih, closed.Ih, 0.0001f);

            var happy = EmotionFace.EmotionPose(Emotion.Happy, 1f);
            happy.Aa = 0.4f;
            var blinked = EmotionFace.ApplyProceduralBlink(happy, 0.8f);
            Assert.AreEqual(1f, blinked.Joy, 0.0001f);
            Assert.AreEqual(0.4f, blinked.Aa, 0.0001f);
            Assert.AreEqual(0.8f, blinked.Blink, 0.0001f);
            Assert.AreEqual(happy.Fun, blinked.Fun, 0.0001f);
        }

        [Test]
        public void LookAt_ClampsLimitsAndReturnsToNeutral()
        {
            var mild = CursorLookMath.Desired(CursorLookMath.FromPixels(-100f, 40f));
            Assert.AreEqual(8f, mild.Yaw, 0.0001f);
            Assert.AreEqual(2.8f, mild.Pitch, 0.0001f);

            var clamped = CursorLookMath.Desired(new LookAngles { Yaw = 32f, Pitch = -10f });
            Assert.AreEqual(CursorLookMath.MaxHeadYaw, clamped.Yaw, 0.0001f);
            Assert.AreEqual(-10f, clamped.Pitch, 0.0001f);

            var edge = CursorLookMath.Desired(new LookAngles { Yaw = -CursorLookMath.FarYaw, Pitch = 0f });
            Assert.AreEqual(-CursorLookMath.MaxHeadYaw, edge.Yaw, 0.0001f);

            var far = CursorLookMath.Desired(CursorLookMath.FromPixels(800f, 10f));
            Assert.AreEqual(0f, far.Yaw, 0.0001f);
            Assert.AreEqual(0f, far.Pitch, 0.0001f);
            Assert.AreEqual(0f, CursorLookMath.Desired(new LookAngles { Yaw = float.NaN, Pitch = 1f }).Yaw, 0.0001f);

            var eyes = CursorLookMath.Eyes(new LookAngles { Yaw = CursorLookMath.MaxHeadYaw, Pitch = CursorLookMath.MaxHeadPitch });
            Assert.AreEqual(CursorLookMath.MaxEyeYaw, eyes.Yaw, 0.0001f);
            Assert.LessOrEqual(eyes.Pitch, CursorLookMath.MaxEyePitch);
            Assert.Greater(eyes.Yaw, CursorLookMath.MaxHeadYaw);

            var look = new CursorLookModel();
            for (var i = 0; i < 30; i++)
                look = look.Advance(true, true, -150f, 0f, 150f + i, 400f, 0.05f);

            Assert.Greater(look.Head.Yaw, 10f);
            Assert.LessOrEqual(look.Head.Yaw, CursorLookMath.MaxHeadYaw + 0.001f);
            Assert.LessOrEqual(MathAbs(look.Eyes.Yaw), CursorLookMath.MaxEyeYaw + 0.001f);

            var parkedX = look.LastX;
            var parkedY = look.LastY;
            for (var i = 0; i < 60; i++)
                look = look.Advance(true, true, -150f, 0f, parkedX, parkedY, 0.05f);

            Assert.Less(MathAbs(look.Head.Yaw), 1f);
            Assert.Less(MathAbs(look.Eyes.Yaw), 1f);

            look = new CursorLookModel();
            for (var i = 0; i < 20; i++)
                look = look.Advance(true, true, 2000f, 0f, 2000f + i, 10f, 0.05f);
            Assert.Less(MathAbs(look.Head.Yaw), 0.5f);

            look = new CursorLookModel { Head = new LookAngles { Yaw = 14f } };
            for (var i = 0; i < 20; i++)
                look = look.Advance(false, true, -150f, 0f, 20f + i, 10f, 0.05f);
            Assert.Less(MathAbs(look.Head.Yaw), 0.5f);

            var smooth = 0f;
            for (var i = 0; i < 40; i++)
                smooth = CursorLookMath.Smooth(smooth, 20f, 0.05f, CursorLookMath.HeadSmoothSeconds);
            Assert.Greater(smooth, 19f);
            Assert.LessOrEqual(smooth, 20f);
        }

        [Test]
        public void IdleVariations_CrossfadeWithoutRepeatsOrPops()
        {
            Assert.AreEqual(0f, IdleVariationPlayer.SmoothStep(0f), 0.0001f);
            Assert.AreEqual(1f, IdleVariationPlayer.SmoothStep(1f), 0.0001f);
            Assert.AreEqual(IdleVariationPlayer.MinHoldSeconds, IdleVariationPlayer.HoldSeconds(0f), 0.0001f);
            Assert.AreEqual(IdleVariationPlayer.MaxHoldSeconds, IdleVariationPlayer.HoldSeconds(1f), 0.0001f);

            for (var current = 0; current < IdlePoses.Count; current++)
            {
                Assert.LessOrEqual(IdlePoses.For(current).MaxAbs(), 6f);
                for (var step = 0; step <= 20; step++)
                {
                    var pick = IdleVariationPlayer.PickNext(current, step / 20f);
                    Assert.AreNotEqual(current, pick);
                    Assert.GreaterOrEqual(pick, 0);
                    Assert.Less(pick, IdlePoses.Count);
                }
            }

            var player = IdleVariationPlayer.Begin(0, 0f);
            var previous = player.Current;
            for (var cycle = 0; cycle < 8; cycle++)
            {
                while (!player.Fading)
                    player = player.Advance(0.1f, cycle / 8f, 0.25f);

                Assert.AreNotEqual(previous, player.Upcoming);
                var upcoming = player.Upcoming;
                while (player.Fading)
                {
                    var before = player.Sample();
                    player = player.Advance(0.05f, cycle / 8f, 0.25f);
                    Assert.IsTrue(IdlePose.Near(before, player.Sample(), 0.85f));
                }

                Assert.AreEqual(upcoming, player.Current);
                Assert.AreEqual(0f, player.Blend, 0.0001f);
                Assert.IsTrue(IdlePose.Near(player.Sample(), IdlePoses.For(player.Current), 0.0001f));
                previous = player.Current;
            }

            var hitch = IdleVariationPlayer.Begin(0, 0f);
            while (!hitch.Fading)
                hitch = hitch.Advance(0.1f, 0.4f, 0.2f);
            var atFadeStart = hitch.Sample();
            hitch = hitch.Advance(10f, 0.4f, 0.2f);
            Assert.IsTrue(hitch.Fading);
            Assert.Less(hitch.Blend, 0.2f);
            Assert.IsTrue(IdlePose.Near(atFadeStart, hitch.Sample(), 0.5f));
        }

        [Test]
        public void Pose_FadesIdleDuringGesturesAndKeepsLookSeparate()
        {
            Assert.IsTrue(GestureActivity.Suppress(true, "Wave", false, null, false));
            Assert.IsTrue(GestureActivity.Suppress(true, "Empty", true, "Nod", false));
            Assert.IsTrue(GestureActivity.Suppress(false, "Idle", false, null, true));
            Assert.IsFalse(GestureActivity.Suppress(false, "Idle", false, null, false));
            Assert.IsFalse(GestureActivity.Suppress(true, "Empty", false, "Idle", false));
            Assert.IsTrue(GestureActivity.IsGestureState("Turn180"));
            Assert.IsFalse(GestureActivity.IsGestureState("Idle"));

            var weight = 1f;
            for (var i = 0; i < 4; i++)
            {
                var next = GestureActivity.FadeWeight(weight, true, 0.1f, GestureActivity.FadeSeconds);
                Assert.Less(next, weight);
                weight = next;
            }

            Assert.Greater(weight, 0f);
            weight = GestureActivity.FadeWeight(weight, true, 1f, GestureActivity.FadeSeconds);
            Assert.AreEqual(0f, weight, 0.0001f);
            weight = GestureActivity.FadeWeight(0f, false, GestureActivity.FadeSeconds, GestureActivity.FadeSeconds);
            Assert.AreEqual(1f, weight, 0.0001f);

            var idle = IdlePoses.For(1);
            var head = new LookAngles { Yaw = 10f, Pitch = 4f };
            var eyes = CursorLookMath.Eyes(head);
            var full = ProceduralPose.Compose(idle, BreathingMotion.ChestDegrees, head, eyes, 1f, 1f);
            Assert.AreEqual(idle.SpineRoll, full.SpineRoll, 0.0001f);
            Assert.AreEqual(3.5f, full.NeckYaw, 0.0001f);
            Assert.AreEqual(idle.HeadRoll, full.HeadRoll, 0.0001f);
            Assert.Greater(full.EyeYaw, 0f);

            var hidden = ProceduralPose.Compose(idle, BreathingMotion.ChestDegrees, head, eyes, 0f, 0f);
            Assert.AreEqual(0f, hidden.SpineRoll, 0.0001f);
            Assert.AreEqual(0f, hidden.ChestPitch, 0.0001f);
            Assert.AreEqual(0f, hidden.HeadYaw, 0.0001f);
            Assert.AreEqual(0f, hidden.EyeYaw, 0.0001f);

            var folded = ProceduralPose.FoldMissingNeck(full, false);
            Assert.AreEqual(0f, folded.NeckYaw, 0.0001f);
            Assert.AreEqual(full.HeadYaw + full.NeckYaw, folded.HeadYaw, 0.0001f);

            Assert.AreEqual(0f, BreathingMotion.ChestPitch(0f), 0.0001f);
            for (var i = 0; i <= 40; i++)
            {
                var breath = BreathingMotion.ChestPitch(i / 8f);
                Assert.LessOrEqual(MathAbs(breath), BreathingMotion.ChestDegrees + 0.0001f);
            }
        }

        [Test]
        public void CursorLookSetting_RoundTripsAndDefaultsOn()
        {
            var created = CompanionSettings.CreateDefault();
            Assert.IsTrue(created.CursorLookAt);

            created.CursorLookAt = false;
            var json = CompanionSettingsJson.Write(created);
            Assert.IsTrue(json.Contains("\"cursorLookAt\": false"));
            var loaded = CompanionSettingsJson.Parse(json, SettingsLegacyPrefs.None);
            Assert.IsFalse(loaded.CursorLookAt);

            var missing = CompanionSettingsJson.Parse("{\"version\":1,\"muted\":true}", SettingsLegacyPrefs.None);
            Assert.IsTrue(missing.CursorLookAt);
            Assert.IsTrue(missing.Muted);

            var directory = Path.Combine(Path.GetTempPath(), "leia-look-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "settings.json");
                CompanionSettingsStore.Save(path, created);
                var fromDisk = CompanionSettingsStore.Load(path, SettingsLegacyPrefs.None);
                Assert.IsFalse(fromDisk.CursorLookAt);
                var text = File.ReadAllText(path);
                Assert.IsFalse(CompanionSettingsSecrets.ContainsForbiddenSecretField(text));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        static float MathAbs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}
