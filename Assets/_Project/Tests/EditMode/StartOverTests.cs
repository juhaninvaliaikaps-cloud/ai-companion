using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.Settings.Presenters;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-117: "Start over with this persona" resets what she feels and knows about the user only.</summary>
    public class StartOverTests
    {
        const float Day = 14f;

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
        }

        [Test]
        public void Mood_StartsOverAtTheTemperamentsOwnStart()
        {
            var balanced = new CompanionMoodState();
            balanced.Push(CompanionMood.Annoyed, 0.9f);
            balanced.Push(CompanionMood.Sad, 0.5f);
            Assert.AreEqual(CompanionMood.Annoyed, balanced.Shown);

            Assert.IsTrue(balanced.StartOver());
            Assert.AreEqual(CompanionMood.Content, balanced.Shown);
            Assert.AreEqual("none", balanced.DescribeTop3());

            // A Cheerful persona starts on her happy baseline again, a Shy one on her bias.
            var cheerful = new CompanionMoodState { Temperament = PersonaTemperaments.Get(PersonaTemperaments.CheerfulId) };
            cheerful.Push(CompanionMood.Sad, 0.9f);
            cheerful.StartOver();
            Assert.AreEqual(CompanionMood.Happy, cheerful.Shown);
            Assert.AreEqual(PersonaTemperament.BaselineStrength, cheerful.Strength(CompanionMood.Happy), 0.0001f);
            Assert.AreEqual(0f, cheerful.Strength(CompanionMood.Sad));

            var shy = new CompanionMoodState { Temperament = PersonaTemperaments.Get(PersonaTemperaments.ShyId) };
            shy.Push(CompanionMood.Flustered, 0.9f);
            shy.StartOver();
            Assert.AreEqual(CompanionMood.Content, shy.Shown);
            Assert.AreEqual(shy.Temperament.RestBiasStrength, shy.Strength(CompanionMood.Flustered), 0.0001f);

            // Nothing to change: false.
            Assert.IsFalse(new CompanionMoodState().StartOver());
        }

        [Test]
        public void Mood_StartOver_KeepsTheTemperamentAndTheIntimateGate()
        {
            IntimateExpressions.Configure(true, true, true);
            var state = new CompanionMoodState { Temperament = PersonaTemperaments.Get(PersonaTemperaments.TsundereId) };
            state.SetIntimateAllowed(true);
            state.Push(CompanionMood.Intimate, 0.9f);
            Assert.AreEqual(CompanionMood.Intimate, state.Shown);

            state.StartOver();
            Assert.AreEqual(0f, state.Strength(CompanionMood.Intimate));
            Assert.AreEqual(PersonaTemperaments.TsundereId, state.Temperament.Id);
            Assert.IsTrue(state.IntimateAllowed);

            // Time after a start over behaves like any fresh state.
            state.Tick(36000f, Day);
            Assert.AreEqual(CompanionMood.Content, state.Shown);
        }

        [Test]
        public void TheMoodKeeperTakesPart()
        {
            Assert.IsTrue(typeof(ICompanionStartOver).IsAssignableFrom(typeof(CompanionMoodKeeper)));
        }

        [Test]
        public void Text_SaysWhatGoesAndWhatStays()
        {
            var prompt = PrivacyConfirm.Prompt(PrivacyConfirmKind.StartOver, 12, "Mira");
            Assert.AreEqual(PrivacyConfirm.StartOverPrompt("Mira"), prompt);
            StringAssert.StartsWith("Start over with Mira?", prompt);
            foreach (var cleared in new[] { "chat history", "remembers about you", "mood", "relationship" })
                StringAssert.Contains(cleared, prompt);
            foreach (var kept in new[] { "Her persona", "her description", "her own life" })
                StringAssert.Contains(kept, prompt);
            StringAssert.Contains("cannot be undone", prompt);

            StringAssert.StartsWith("Start over with her?", PrivacyConfirm.StartOverPrompt("  "));
            Assert.AreEqual("Start over with Mira", StartOverText.Button(" Mira "));
            Assert.AreEqual("Start over with her", StartOverText.Button(null));
            StringAssert.StartsWith("Started over with Mira.", StartOverText.Status("Mira"));
        }

        [Test]
        public void TheConfirmStep_HoldsOneRequestAtATime()
        {
            var confirm = new PrivacyConfirm();
            confirm.Request(PrivacyConfirmKind.StartOver);
            Assert.AreEqual(PrivacyConfirmKind.StartOver, confirm.Pending);
            confirm.Request(PrivacyConfirmKind.ClearHistory);
            Assert.AreEqual(PrivacyConfirmKind.ClearHistory, confirm.Pending, "a new request replaces it");
            confirm.Request(PrivacyConfirmKind.StartOver);
            Assert.IsTrue(confirm.Escape());
            Assert.IsFalse(confirm.HasPending);
        }
    }
}
