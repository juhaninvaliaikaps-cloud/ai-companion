using System.Text;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-217: characterization of the nine displayed temperament steps. The UI shows 1..10,
    /// while storage and the mood engine keep 0..100. These tests pin the exact step mapping,
    /// the deterministic mood consumer, the prompt-only manner and voice consumers, and the
    /// thresholds that make some adjacent steps invisible without the others.
    /// </summary>
    public sealed class TemperamentControlTraceTests
    {
        const int Step1 = 0;
        const int Step5 = 44;
        const int Step6 = 56;
        const int Step7 = 67;
        const int Step8 = 78;
        const int Step10 = 100;

        static readonly int[] MoodAxes =
        {
            TemperamentAxes.Warmth,
            TemperamentAxes.Volatility,
            TemperamentAxes.Assertiveness,
            TemperamentAxes.Playfulness,
            TemperamentAxes.Openness,
            TemperamentAxes.Energy,
            TemperamentAxes.Directness,
            TemperamentAxes.Confidence,
        };

        [Test]
        public void DisplayedSteps_KeepTheHundredScale_AndStepSixIsTheStoredMiddle()
        {
            Assert.AreEqual(0, TemperamentSteps.ValueForStep(1));
            Assert.AreEqual(44, TemperamentSteps.ValueForStep(5));
            Assert.AreEqual(56, TemperamentSteps.ValueForStep(6));
            Assert.AreEqual(100, TemperamentSteps.ValueForStep(10));

            Assert.AreEqual(1, TemperamentSteps.DisplayStep(0f));
            Assert.AreEqual(5, TemperamentSteps.DisplayStep(44f));
            Assert.AreEqual(6, TemperamentSteps.DisplayStep(50f));
            Assert.AreEqual(6, TemperamentSteps.DisplayStep(56f));
            Assert.AreEqual(10, TemperamentSteps.DisplayStep(100f));

            // A stored 50 is shown as step 6, and reselecting step 6 must not rewrite it.
            Assert.IsFalse(TemperamentSteps.IsNewStep(50f, 6));
            Assert.IsTrue(TemperamentSteps.IsNewStep(50f, 5));
        }

        [Test]
        public void MoodAxes_ChangeAtOneFiveAndTen_ButCuriosityDoesNot()
        {
            foreach (var axis in MoodAxes)
            {
                var one = One(axis, Step1);
                var five = One(axis, Step5);
                var ten = One(axis, Step10);

                Assert.IsFalse(Fingerprint(TemperamentMood.From(one)) == Fingerprint(TemperamentMood.From(five)),
                    TemperamentAxes.Names[axis] + " must differ from step 1 to 5");
                Assert.IsFalse(Fingerprint(TemperamentMood.From(five)) == Fingerprint(TemperamentMood.From(ten)),
                    TemperamentAxes.Names[axis] + " must differ from step 5 to 10");
            }

            var curiousOne = One(TemperamentAxes.Curiosity, Step1);
            var curiousFive = One(TemperamentAxes.Curiosity, Step5);
            var curiousTen = One(TemperamentAxes.Curiosity, Step10);
            Assert.IsTrue(TemperamentMood.From(curiousOne).SameTuning(TemperamentMood.From(curiousFive)));
            Assert.IsTrue(TemperamentMood.From(curiousFive).SameTuning(TemperamentMood.From(curiousTen)));
        }

        [Test]
        public void AdjacentMiddleSteps_MoveMoodSlightly_ButDoNotChangeManner()
        {
            foreach (var axis in MoodAxes)
            {
                var five = TemperamentMood.From(One(axis, Step5));
                var six = TemperamentMood.From(One(axis, Step6));
                Assert.IsFalse(five.SameTuning(six), TemperamentAxes.Names[axis] + " must move the mood at 5 -> 6");
                Assert.Less(MaxGainDelta(five, six), 0.13f,
                    TemperamentAxes.Names[axis] + " should move only slightly at the adjacent middle pair");
                Assert.AreEqual(string.Empty, PersonaManner.Line(One(axis, Step5)));
                Assert.AreEqual(string.Empty, PersonaManner.Line(One(axis, Step6)));
            }
        }

        [Test]
        public void Manner_IsSilentAtFiveAndCrossesItsThresholdAtSevenToEight()
        {
            foreach (var axis in MoodAxes)
            {
                Assert.IsNotEmpty(PersonaManner.Line(One(axis, Step1)), TemperamentAxes.Names[axis] + " at step 1");
                Assert.IsEmpty(PersonaManner.Line(One(axis, Step5)), TemperamentAxes.Names[axis] + " at step 5");
                Assert.IsNotEmpty(PersonaManner.Line(One(axis, Step10)), TemperamentAxes.Names[axis] + " at step 10");

                // 67 is below PersonaManner.High (70), 78 is above it: this adjacent pair is the
                // first one that turns a slider into a manner instruction.
                Assert.IsEmpty(PersonaManner.Line(One(axis, Step7)), TemperamentAxes.Names[axis] + " at step 7");
                Assert.IsNotEmpty(PersonaManner.Line(One(axis, Step8)), TemperamentAxes.Names[axis] + " at step 8");
            }

            Assert.IsEmpty(PersonaManner.Line(One(TemperamentAxes.Curiosity, Step10)),
                "curiosity has no manner word");
        }

        [Test]
        public void VoiceInput_ShowsEveryExactValue_AndCuriosityOnlyReachesThisPrompt()
        {
            foreach (var axis in MoodAxes)
            {
                var name = TemperamentAxes.Names[axis].ToLowerInvariant();
                StringAssert.Contains(name + " 0", VoiceInput.Everyday("A persona.", One(axis, Step1), null));
                StringAssert.Contains(name + " 44", VoiceInput.Everyday("A persona.", One(axis, Step5), null));
                StringAssert.Contains(name + " 100", VoiceInput.Everyday("A persona.", One(axis, Step10), null));
            }

            var curious = One(TemperamentAxes.Curiosity, Step10);
            StringAssert.Contains("curiosity 100", VoiceInput.Everyday("A persona.", curious, null));
            Assert.IsEmpty(PersonaManner.Line(curious));
            Assert.AreEqual(GameVoices.For(One(TemperamentAxes.Curiosity, Step1), "friend"),
                GameVoices.For(curious, "friend"));
            Assert.IsTrue(TemperamentMood.From(One(TemperamentAxes.Curiosity, Step1))
                .SameTuning(TemperamentMood.From(curious)));
        }

        [Test]
        public void GameVoice_IsAThresholdClassifier_AndOpennessAndCuriosityDoNotMoveIt()
        {
            Assert.AreEqual(GameVoice.Sweet, GameVoices.For(One(TemperamentAxes.Warmth, Step10), "friend"));
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(One(TemperamentAxes.Warmth, Step1), "friend"));
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(One(TemperamentAxes.Warmth, Step5), "friend"));
            Assert.AreEqual(GameVoice.Cool, GameVoices.For(One(TemperamentAxes.Warmth, Step6), "friend"));

            foreach (var axis in new[] { TemperamentAxes.Openness, TemperamentAxes.Curiosity })
            {
                var one = GameVoices.For(One(axis, Step1), "friend");
                var five = GameVoices.For(One(axis, Step5), "friend");
                var ten = GameVoices.For(One(axis, Step10), "friend");
                Assert.AreEqual(GameVoice.Cool, one, TemperamentAxes.Names[axis]);
                Assert.AreEqual(one, five, TemperamentAxes.Names[axis]);
                Assert.AreEqual(one, ten, TemperamentAxes.Names[axis]);
            }
        }

        [Test]
        public void StoredAxes_RoundTripAtStepPrecision_AndOldFilesStillLoadTheirPresetPoint()
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(TemperamentAxes.Warmth, Step1);
            axes.Set(TemperamentAxes.Curiosity, Step10);
            var persona = new PersonaDefinition { Id = "trace", DisplayName = "Trace", TemperamentMode = TemperamentModes.Manual };
            persona.SetAxes(axes);
            var back = PersonaJson.Parse(PersonaJson.Write(persona));
            Assert.AreEqual(0, back.Axes.Value(TemperamentAxes.Warmth));
            Assert.AreEqual(100, back.Axes.Value(TemperamentAxes.Curiosity));
            Assert.AreEqual(TemperamentModes.Manual, back.TemperamentMode);

            var legacy = new PersonaDefinition { Id = "legacy", DisplayName = "Legacy", Temperament = "tsundere" };
            Assert.IsNull(legacy.Axes);
            var loaded = PersonaJson.Parse(PersonaJson.Write(legacy));
            Assert.IsNull(loaded.Axes);
            Assert.AreEqual(PersonaTemperaments.TsundereId, loaded.Temperament);
        }

        static TemperamentAxes One(int axis, int value)
        {
            var axes = TemperamentAxes.Neutral();
            axes.Set(axis, value);
            return axes;
        }

        static string Fingerprint(PersonaTemperament temperament)
        {
            var text = new StringBuilder();
            text.Append((int)temperament.Baseline).Append('|').Append((int)temperament.RestBias)
                .Append('|').Append(temperament.RestBiasStrength.ToString("R"));
            for (var i = 1; i < CompanionMoodNames.Count; i++)
            {
                var mood = (CompanionMood)i;
                text.Append('|').Append(temperament.Gain(mood, true).ToString("R"))
                    .Append('|').Append(temperament.HalfLifeScale(mood).ToString("R"));
            }

            return text.ToString();
        }

        static float MaxGainDelta(PersonaTemperament a, PersonaTemperament b)
        {
            var max = 0f;
            for (var i = 1; i < CompanionMoodNames.Count; i++)
            {
                var mood = (CompanionMood)i;
                var delta = System.Math.Abs(a.Gain(mood, true) - b.Gain(mood, true));
                if (delta > max)
                    max = delta;
            }

            return max;
        }
    }
}
