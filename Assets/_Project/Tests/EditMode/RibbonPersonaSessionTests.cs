using AICompanion.NewUI;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonPersonaSessionTests
    {
        [Test]
        public void DraftsFollowThePersonaAndSentDraftsDoNotComeBack()
        {
            var session = new RibbonPersonaSession("happi");

            Assert.That(session.TryChange("coach", "A draft for Happi", out var draft), Is.True);
            Assert.That(draft, Is.Empty);
            session.TryChange("HAPPI", "A draft for Avery", out draft);
            Assert.That(draft, Is.EqualTo("A draft for Happi"));
            session.TryChange("coach", string.Empty, out draft);
            Assert.That(draft, Is.EqualTo("A draft for Avery"));
            session.TryChange("happi", draft, out draft);
            Assert.That(draft, Is.Empty);
        }

        [Test]
        public void PickerShowsTheActivePersonaThenRecentChoicesAndBuiltIns()
        {
            var cards = new[]
            {
                new PersonaCard { Id = "happi", BuiltIn = true }, new PersonaCard { Id = "friend", BuiltIn = true },
                new PersonaCard { Id = "coach", BuiltIn = true }, new PersonaCard { Id = "calm", BuiltIn = true },
                new PersonaCard { Id = "custom", Active = true }
            };
            var session = new RibbonPersonaSession("custom");
            session.TryChange("coach", string.Empty, out _);
            session.TryChange("custom", string.Empty, out _);

            Assert.That(session.Choices(cards, null).ConvertAll(c => c.Id),
                Is.EqualTo(new[] { "custom", "coach", "happi", "friend" }));
        }

        [Test]
        public void SetupNameOccupiesTheActiveSlotAndUnchangedNamesKeepTheDraft()
        {
            var session = new RibbonPersonaSession(null);

            Assert.That(session.TryChange(string.Empty, "draft", out var draft), Is.False);
            Assert.That(draft, Is.EqualTo("draft"));
            var choices = session.Choices(new PersonaCard[0], "My companion");
            Assert.That(choices[0].Name, Is.EqualTo("My companion"));
            Assert.That(choices[0].Active, Is.True);
        }
    }
}
