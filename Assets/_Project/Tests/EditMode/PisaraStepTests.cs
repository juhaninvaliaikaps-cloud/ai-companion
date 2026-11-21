using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-151: the Persona Studio's Pisara step: condensing and the chat text.</summary>
    public sealed class PisaraStepTests
    {
        sealed class FakeProvider : IChatProvider
        {
            readonly string _id;
            readonly ChatResult _reply;
            public ChatRequest Last;
            public int Calls;

            public FakeProvider(string id, ChatResult reply)
            {
                _id = id;
                _reply = reply;
            }

            public string ProviderId { get { return _id; } }
            public string DisplayName { get { return _id; } }
            public bool IsConfigured { get { return true; } }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                return Task.FromResult(_reply);
            }
        }

        static ChatResult Reply(string text)
        {
            return new ChatResult(true, text, ChatErrorKind.None, string.Empty, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
        }

        [Test]
        public void Condensing_UsesTheModel_AndStaysWithinTheBudget()
        {
            var provider = new FakeProvider(ChatDefaults.LocalId, Reply("\"You are Lola. " + new string('a', 5000) + ". The end.\""));
            var result = PersonaCondenser.CondenseAsync(provider, "Lola", "long text", 4000, true, false, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result.Ok, result.Failure);
            Assert.LessOrEqual(result.Text.Length, 4000);
            StringAssert.StartsWith("You are Lola.", result.Text);
            Assert.IsTrue(provider.Last.NoMemory, "no memories in a condense request");
            Assert.IsTrue(provider.Last.BlockCloud);
            StringAssert.Contains("At most 4000 characters", provider.Last.SystemPrompt);
            StringAssert.Contains("Her description:\nlong text", provider.Last.Messages[0].Content);
        }

        [Test]
        public void APrivateCondense_NeverGoesToTheCloud()
        {
            var cloud = new FakeProvider(ChatDefaults.GrokId, Reply("You are Lola."));
            var result = PersonaCondenser.CondenseAsync(cloud, "Lola", "text", 4000, true, false, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(PersonaCondenser.CloudRefused, result.Failure);
            Assert.AreEqual(0, cloud.Calls);

            Assert.AreEqual(PersonaCondenser.NoModel,
                PersonaCondenser.CondenseAsync(null, "Lola", "text", 4000, false, false, CancellationToken.None).GetAwaiter().GetResult().Failure);
        }

        [Test]
        public void AnEmptyOrFailedReply_ChangesNothing()
        {
            var empty = new FakeProvider(ChatDefaults.LocalId, Reply("  "));
            Assert.AreEqual(PersonaCondenser.Empty, PersonaCondenser.CondenseAsync(empty, "Lola", "text", 4000, true, false, CancellationToken.None).GetAwaiter().GetResult().Failure);
            var failed = new FakeProvider(ChatDefaults.LocalId, new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, "Ollama isn't running.", string.Empty));
            Assert.AreEqual("Ollama isn't running.", PersonaCondenser.CondenseAsync(failed, "Lola", "text", 4000, true, false, CancellationToken.None).GetAwaiter().GetResult().Failure);
        }

        [Test]
        public void TheCleanUp_DropsALabelAndCutsAtASentence()
        {
            Assert.AreEqual("You are Lola.", PersonaCondenser.Clean("Condensed description: You are Lola.", 4000));
            var text = "You are Lola. " + new string('b', 60) + ". " + new string('c', 60);
            var cut = PersonaCondenser.Clean(text, 100);
            Assert.LessOrEqual(cut.Length, 100);
            StringAssert.EndsWith(".", cut);
        }

        [Test]
        public void TheChatText_FollowsTheDescription_AndKeepsACondensedOne()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Description = "Short.";
            persona.Personality = "Old text.";
            PersonaDescription.ApplyTo(persona);
            Assert.AreEqual("Short.", persona.Personality);
            Assert.IsFalse(persona.Condensed);

            var longText = new string('a', 3000) + "\n\n" + new string('b', 3000);
            persona.Description = longText;
            persona.Personality = "You are Lola, condensed.";
            persona.Condensed = true;
            PersonaDescription.ApplyTo(persona);
            Assert.AreEqual("You are Lola, condensed.", persona.Personality, "Pisara's condensed text is kept");

            persona.Condensed = false;
            PersonaDescription.ApplyTo(persona);
            Assert.AreEqual(new string('a', 3000), persona.Personality, "not condensed: cut at a paragraph");

            var old = PersonaDefinition.CreateStarter("old");
            old.Description = string.Empty;
            old.Personality = "You are Old.";
            PersonaDescription.ApplyTo(old);
            Assert.AreEqual("You are Old.", old.Personality, "an old persona keeps its text");
        }

        [Test]
        public void TheCondensedFlag_RoundTrips()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Condensed = true;
            Assert.IsTrue(PersonaJson.Parse(PersonaJson.Write(persona)).Condensed);
            Assert.IsFalse(PersonaJson.Parse("{\n  \"version\": 1,\n  \"id\": \"x\"\n}\n").Condensed);
        }

        [Test]
        public void ACorrectedRelationship_IsLocked_AndAReadSheetKeepsItsHashFromTheChatPrompt()
        {
            var sheet = CharacterSheet.Neutral("draft text");
            PisaraCorrections.SetRelationship(sheet, RelationshipRoles.Family, "stepsister", 0f, 0f, true);
            Assert.IsTrue(sheet.relationshipLocked);
            Assert.AreEqual(RelationshipRoles.Family, sheet.relationship.role);

            var persona = PersonaDefinition.CreateStarter("lola");
            var prompt = PersonaText.Build(persona, "Lola", RelationshipRoles.Friend);
            PisaraReading.Finish(sheet, prompt, CharacterSheet.SourceModel, DateTime.UtcNow);
            Assert.IsTrue(sheet.IsFor(prompt), "the keeper finds the Studio's sheet and doesn't read again");
            Assert.AreEqual(CharacterSheet.SourceModel, sheet.source);
        }
    }
}
