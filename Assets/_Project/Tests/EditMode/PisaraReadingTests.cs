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
    /// <summary>JUH-118 section 1: the reading, its fallback, its privacy rule and what settings show.</summary>
    public sealed class PisaraReadingTests
    {
        const string Prompt = "You are Mira, the user's bratty stepsister. You grew up together.";

        const string GoodReply =
            "Here is the sheet:\n```json\n{\n" +
            "  \"temperament\": { \"calm\": 0.1, \"cheerful\": 0.2, \"shy\": 0.0, \"tsundere\": 0.9, \"moody\": 0.3, \"reason\": \"sharp-tongued but caring\" },\n" +
            "  \"relationship\": { \"role\": \"family\", \"label\": \"stepsister\", \"affection\": 25, \"familiarity\": 70, \"sharedHistory\": true, \"reason\": \"grew up together\" },\n" +
            "  \"landing\": { \"touchy\": [\"being called childish\"], \"winsHerOver\": [\"being trusted\"] },\n" +
            "  \"world\": { \"backstory\": [\"She studies design.\"], \"lastWeek\": [ { \"daysAgo\": 2, \"text\": \"Failed a quiz.\" } ],\n" +
            "    \"openThreads\": [ { \"text\": \"Portfolio review.\", \"goodOutcome\": \"It went well.\", \"badOutcome\": \"It went badly.\" } ],\n" +
            "    \"rightNow\": \"Sketching at her desk.\" },\n" +
            "  \"promptHash\": \"fake\", \"source\": \"keywords\"\n}\n```";

        sealed class FakeProvider : IChatProvider
        {
            public string Id = ChatDefaults.LocalId;
            public bool Configured = true;
            public string Reply = GoodReply;
            public bool Ok = true;
            public bool Hang;
            public bool Throw;
            public int Calls;
            public ChatRequest Last;

            public string ProviderId { get { return Id; } }
            public string DisplayName { get { return Id; } }
            public bool IsConfigured { get { return Configured; } }

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                if (Throw)
                    throw new InvalidOperationException("boom");
                if (Hang)
                    return new TaskCompletionSource<ChatResult>().Task;
                return Task.FromResult(Ok ? ChatResult.Success(Reply) : ChatResult.Fail(ChatErrorKind.Unreachable, "down"));
            }
        }

        static PisaraReadResult Read(FakeProvider provider, bool keepsLocal, Func<string, SheetTemperament> keywords = null, float timeout = 30f)
        {
            // Off the main thread: no Unity synchronization context, so waiting cannot deadlock.
            return Task.Run(() => PisaraReader.ReadAsync(provider, "Mira", Prompt, keepsLocal, false, keywords, CancellationToken.None, timeout)).Result;
        }

        // ---- The reading -------------------------------------------------------------------

        [Test]
        public void GoodReply_BecomesAModelSheetStampedByCode()
        {
            var provider = new FakeProvider();
            var result = Read(provider, false);
            Assert.AreEqual(1, provider.Calls, "one call per reading");
            Assert.IsTrue(result.FromModel);
            Assert.AreEqual(string.Empty, result.Failure);
            Assert.AreEqual(CharacterSheet.SourceModel, result.Sheet.source, "the model cannot claim another source");
            Assert.AreEqual(CharacterSheet.HashPrompt(Prompt), result.Sheet.promptHash, "the model cannot set the hash");
            Assert.IsTrue(result.Sheet.IsFor(Prompt));
            Assert.IsNotEmpty(result.Sheet.createdAtUtc);
            Assert.AreEqual(0.9f, result.Sheet.temperament.tsundere, 0.001f);
            Assert.AreEqual("stepsister", result.Sheet.relationship.label);
            Assert.AreEqual(1, result.Sheet.world.backstory.Count);
        }

        [Test]
        public void Request_CarriesTheSheetPromptTheConsentRuleAndThePersonaAsData()
        {
            var provider = new FakeProvider();
            Read(provider, false);
            StringAssert.StartsWith(CharacterSheetPrompt.System, provider.Last.SystemPrompt);
            StringAssert.Contains(IntimateBoundaries.ConsentRule, provider.Last.SystemPrompt, "IntimateBoundaries stays in every prompt");
            StringAssert.Contains(IntimateBoundaries.GateClosedRule, provider.Last.SystemPrompt);
            Assert.AreEqual(1, provider.Last.Messages.Count);
            Assert.AreEqual(ChatRole.User, provider.Last.Messages[0].Role);
            StringAssert.Contains(Prompt, provider.Last.Messages[0].Content);
            Assert.IsNull(provider.Last.Image);
        }

        [Test]
        public void BadJson_FallsBackToNeutralDefaults()
        {
            var provider = new FakeProvider { Reply = "I am sorry, I cannot do that." };
            var result = Read(provider, false);
            Assert.IsTrue(result.Called);
            Assert.IsFalse(result.FromModel);
            Assert.AreEqual(PisaraReader.BadJson, result.Failure);
            Assert.AreEqual(CharacterSheet.SourceDefault, result.Sheet.source);
            Assert.IsTrue(result.Sheet.IsFor(Prompt), "the fallback is cached for this prompt too");
            Assert.AreEqual(RelationshipRoles.Stranger, result.Sheet.relationship.role);
            Assert.AreEqual(0, result.Sheet.world.backstory.Count);
        }

        [Test]
        public void EmptyObject_IsNotAReading()
        {
            var result = Read(new FakeProvider { Reply = "{}" }, false);
            Assert.IsFalse(result.FromModel, "an empty object must not be cached as the model's answer");
            Assert.AreEqual(PisaraReader.BadJson, result.Failure);
        }

        [Test]
        public void NoModel_AnError_OrAnException_FallBackWithoutThrowing()
        {
            var unconfigured = new FakeProvider { Configured = false };
            var none = Read(unconfigured, false);
            Assert.AreEqual(0, unconfigured.Calls);
            Assert.IsFalse(none.Called);
            Assert.AreEqual(PisaraReader.NoModel, none.Failure);

            var missing = Task.Run(() => PisaraReader.ReadAsync(null, "Mira", Prompt, false, false, null, CancellationToken.None)).Result;
            Assert.AreEqual(CharacterSheet.SourceDefault, missing.Sheet.source);

            var down = Read(new FakeProvider { Ok = false }, false);
            Assert.IsFalse(down.FromModel);
            Assert.AreEqual(CharacterSheet.SourceDefault, down.Sheet.source);

            var broken = Read(new FakeProvider { Throw = true }, false);
            Assert.IsFalse(broken.FromModel);
            Assert.IsNotEmpty(broken.Failure);
        }

        [Test]
        public void Timeout_FallsBack()
        {
            var provider = new FakeProvider { Hang = true };
            var result = Read(provider, false, null, 1f);
            Assert.AreEqual(1, provider.Calls);
            Assert.IsFalse(result.FromModel);
            Assert.AreEqual(PisaraReader.TimedOut, result.Failure);
            Assert.AreEqual(CharacterSheet.SourceDefault, result.Sheet.source);
        }

        [Test]
        public void Fallback_UsesTheKeywordTemperamentWhenThereIsOne()
        {
            Func<string, SheetTemperament> keywords = prompt => new SheetTemperament { tsundere = 0.8f };
            var result = Read(new FakeProvider { Reply = "nope" }, false, keywords);
            Assert.AreEqual(CharacterSheet.SourceKeywords, result.Sheet.source);
            Assert.AreEqual(0.8f, result.Sheet.temperament.tsundere, 0.001f);
            Assert.AreEqual(RelationshipRoles.Stranger, result.Sheet.relationship.role, "neutral defaults for the rest");

            Func<string, SheetTemperament> throwing = prompt => { throw new InvalidOperationException(); };
            Assert.AreEqual(CharacterSheet.SourceDefault, Read(new FakeProvider { Reply = "nope" }, false, throwing).Sheet.source);
            Assert.IsNull(PisaraKeywords.Temperament(Prompt), "the JUH-119 scorer is not on main yet");
        }

        // ---- Privacy: a private persona never calls a cloud model ---------------------------

        [Test]
        public void Private_NeverCallsACloudModel()
        {
            var cloud = new FakeProvider { Id = ChatDefaults.GrokId };
            var result = Read(cloud, true);
            Assert.AreEqual(0, cloud.Calls, "nothing may be sent to the cloud for a private persona");
            Assert.IsFalse(result.Called);
            Assert.AreEqual(PisaraReader.CloudRefused, result.Failure);
            Assert.AreEqual(CharacterSheet.SourceDefault, result.Sheet.source);
            Assert.IsFalse(PisaraReader.MayUse(cloud, true));
            Assert.IsTrue(PisaraReader.MayUse(cloud, false));
            Assert.IsFalse(PisaraReader.MayUse(new FakeProvider { Id = "something-else" }, true), "only the local model or the private server");
            Assert.IsFalse(PisaraReader.MayUse(null, false));
        }

        [Test]
        public void Private_UsesTheLocalModelOrThePrivateServer_AndMarksTheRequest()
        {
            var local = new FakeProvider { Id = ChatDefaults.LocalId };
            Assert.IsTrue(Read(local, true).FromModel);
            Assert.AreEqual(1, local.Calls);
            Assert.IsTrue(local.Last.BlockCloud);

            var custom = new FakeProvider { Id = ChatDefaults.CustomId };
            Assert.IsTrue(Read(custom, true).FromModel);

            var open = new FakeProvider { Id = ChatDefaults.GrokId };
            Read(open, false);
            Assert.IsFalse(open.Last.BlockCloud);
        }

        [Test]
        public void Private_RoutingNeverPicksTheCloud()
        {
            // The keeper asks PrivateChatRouting with IntimateExpressions.KeepsLocal, like chat does.
            Assert.AreEqual(ChatDefaults.LocalId, PrivateChatRouting.ProviderId(true, ChatDefaults.GrokId, PrivateEndpointTypes.Local));
            Assert.AreEqual(ChatDefaults.CustomId, PrivateChatRouting.ProviderId(true, ChatDefaults.GrokId, PrivateEndpointTypes.Custom));
            Assert.AreEqual(ChatDefaults.LocalId, PrivateChatRouting.ProviderId(false, ChatDefaults.LocalId, PrivateEndpointTypes.Local), "a persona set to the local model stays local");
        }

        // ---- Which temperament applies -------------------------------------------------------

        [Test]
        public void ManualPickWins_BuiltInKeepsItsPreset_OtherwiseTheSheetDecides()
        {
            var sheet = CharacterSheet.Parse(GoodReply);

            Assert.IsTrue(TemperamentModes.IsManual("manual", "", false));
            Assert.IsTrue(TemperamentModes.IsManual("", "shy", false), "an old file with a preset is a manual pick");
            Assert.IsTrue(TemperamentModes.IsManual("", "", true), "a built-in keeps its hand-picked value");
            Assert.IsFalse(TemperamentModes.IsManual("", "", false), "an old file without a preset is automatic");
            Assert.IsFalse(TemperamentModes.IsManual("auto", "shy", true));
            Assert.AreEqual(string.Empty, TemperamentModes.Normalize("whatever"));

            var manual = PisaraReading.Choose("manual", "shy", false, sheet);
            Assert.IsFalse(manual.Auto);
            Assert.AreEqual(PersonaTemperaments.ShyId, manual.Temperament.Id);
            Assert.AreEqual("Temperament: Shy (your pick)", manual.Line);

            var balanced = PisaraReading.Choose("manual", "", false, sheet);
            Assert.AreEqual(PersonaTemperaments.DefaultId, balanced.Temperament.Id, "a manual Balanced is kept");

            var builtIn = PisaraReading.Choose("", "cheerful", true, sheet);
            Assert.AreEqual(PersonaTemperaments.CheerfulId, builtIn.Temperament.Id);
            Assert.AreEqual("Temperament: Cheerful (built-in)", builtIn.Line);

            var auto = PisaraReading.Choose("", "", false, sheet);
            Assert.IsTrue(auto.Auto);
            Assert.AreEqual(PersonaTemperaments.TsundereId, auto.Temperament.Id);
            Assert.AreEqual("Temperament: Auto (Tsundere 90%): sharp-tongued but caring", auto.Line);

            var waiting = PisaraReading.Choose("auto", "", false, null);
            Assert.AreEqual(PersonaTemperaments.DefaultId, waiting.Temperament.Id, "balanced until the reading is back");
            StringAssert.Contains(PisaraReading.WaitingText, waiting.Line);
        }

        [Test]
        public void Blend_ShowsBothShares()
        {
            var sheet = CharacterSheet.Neutral(Prompt);
            sheet.source = CharacterSheet.SourceModel;
            sheet.temperament = new SheetTemperament { shy = 0.6f, cheerful = 0.4f, reason = "quiet but warm" };
            var choice = PisaraReading.Choose("auto", "", false, sheet);
            Assert.AreEqual("Temperament: Auto (Shy 60%, Cheerful 40%): quiet but warm", choice.Line);
            Assert.AreEqual(PersonaTemperamentBlend.BlendId, choice.Temperament.Id);
        }

        [Test]
        public void PersonaFile_KeepsTheModeAndOldFilesStillLoad()
        {
            var old = PersonaJson.Parse("{ \"version\": 1, \"id\": \"mira\", \"displayName\": \"Mira\", \"personality\": \"x\", \"temperament\": \"shy\" }");
            Assert.AreEqual(string.Empty, old.TemperamentMode, "a missing key is the default");
            Assert.AreEqual("shy", old.Temperament);

            old.TemperamentMode = "AUTO";
            var back = PersonaJson.Parse(PersonaJson.Write(old));
            Assert.AreEqual(TemperamentModes.Auto, back.TemperamentMode);
            Assert.AreEqual("shy", back.Temperament, "the preset is kept, so switching back to manual restores it");
            Assert.AreEqual(TemperamentModes.Auto, back.Copy().TemperamentMode);
        }

        [Test]
        public void Catalog_ResolvesBuiltInsUserPersonasAndTheSetupName()
        {
            var builtIn = new List<string> { "{ \"version\": 1, \"id\": \"friend\", \"displayName\": \"Friend\", \"personality\": \"x\", \"temperament\": \"cheerful\" }" };
            var catalog = PersonaCatalog.Load(builtIn, null);
            var sheet = CharacterSheet.Parse(GoodReply);

            Assert.AreEqual(PersonaTemperaments.CheerfulId, PersonaRuntime.TemperamentChoiceFor(catalog, "friend", sheet).Temperament.Id, "built-ins keep their value");
            Assert.AreEqual(PersonaTemperaments.CheerfulId, PersonaRuntime.TemperamentFor(catalog, "friend").Id);
            Assert.AreEqual(PersonaTemperaments.DefaultId, PersonaRuntime.TemperamentChoiceFor(catalog, "", sheet).Temperament.Id, "the setup name is balanced");
            Assert.AreEqual(PersonaTemperaments.DefaultId, PersonaRuntime.TemperamentChoiceFor(catalog, "missing", sheet).Temperament.Id);
            Assert.AreEqual(PersonaTemperaments.DefaultId, PersonaRuntime.TemperamentChoiceFor(null, "friend", sheet).Temperament.Id);
        }

        // ---- What settings and the log show -------------------------------------------------

        [Test]
        public void Lines_ForSettingsAndTheLog()
        {
            var sheet = PisaraReading.Finish(CharacterSheet.Parse(GoodReply), Prompt, CharacterSheet.SourceModel, new DateTime(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual("Relationship: stepsister (family), affection 25, familiarity 70, shared past: grew up together", PisaraReading.RelationshipLine(sheet));
            Assert.AreEqual("Pisara reading: mira = Tsundere 90%; family, affection 25, familiarity 70; world 1 facts, 1 week entries, 1 threads; source model", PisaraReading.LogLine("Mira", sheet));
            StringAssert.StartsWith("Read by the model on ", PisaraReading.SourceLine(sheet));
            Assert.AreEqual("2026-11-13T20:00:00Z", sheet.createdAtUtc);

            StringAssert.DoesNotContain("She studies design", PisaraReading.LogLine("mira", sheet), "the plain log gets no backstory");
            StringAssert.Contains(PisaraReading.WaitingText, PisaraReading.RelationshipLine(null));
            Assert.AreEqual("Not read yet.", PisaraReading.SourceLine(null));

            var fallback = PisaraReading.Fallback(Prompt, null, DateTime.UtcNow);
            StringAssert.Contains("neutral defaults", PisaraReading.SourceLine(fallback));
            Assert.AreEqual("Relationship: stranger, affection 0, familiarity 0", PisaraReading.RelationshipLine(fallback));
            StringAssert.EndsWith("source default", PisaraReading.Summary(fallback));
        }

        [Test]
        public void Finish_StampsAndClamps()
        {
            var sheet = new CharacterSheet();
            sheet.relationship.affection = 500f;
            sheet.source = "model";
            var done = PisaraReading.Finish(sheet, Prompt, "something odd", DateTime.UtcNow);
            Assert.AreEqual(CharacterSheet.SourceDefault, done.source);
            Assert.AreEqual(100f, done.relationship.affection, 0.001f);
            Assert.IsTrue(done.IsFor(Prompt));
            Assert.IsFalse(done.IsFor(Prompt + " changed"), "a changed prompt means a new reading is due");
        }

        [Test]
        public void Store_ReturnsNothingForAChangedPrompt()
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pisara-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                var path = CharacterSheetStore.FileFor(folder, "Mira");
                StringAssert.EndsWith("sheet-mira.json", path);
                var sheet = PisaraReading.Finish(CharacterSheet.Parse(GoodReply), Prompt, CharacterSheet.SourceModel, DateTime.UtcNow);
                Assert.IsTrue(CharacterSheetStore.Save(path, sheet));
                Assert.IsNotNull(CharacterSheetStore.LoadFor(path, Prompt));
                Assert.IsNull(CharacterSheetStore.LoadFor(path, Prompt + " She is shy now."));
                StringAssert.DoesNotContain("stepsister", System.IO.File.ReadAllText(path), "the sheet is encrypted on disk");
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public void KinshipLabel_MakesTheRoleFamily()
        {
            foreach (var label in new[] { "stepsister", "Older Brother", "mother-in-law", "half-sister", "cousin", "twin sister" })
                Assert.IsTrue(PisaraReading.IsKinLabel(label), label);
            foreach (var label in new[] { "", "coworker", "oldest friend", "person", "season", "stepladder", "girlfriend" })
                Assert.IsFalse(PisaraReading.IsKinLabel(label), label);

            var wrong = new FakeProvider { Reply = GoodReply.Replace("\"role\": \"family\"", "\"role\": \"close_friend\"") };
            Assert.AreEqual(RelationshipRoles.Family, Read(wrong, false).Sheet.relationship.role, "a stepsister is family whatever the model said");

            var partner = new CharacterSheet();
            partner.relationship.role = RelationshipRoles.Partner;
            partner.relationship.label = "stepsister";
            PisaraReading.ApplyKinship(partner);
            Assert.AreEqual(RelationshipRoles.Partner, partner.relationship.role, "the description's partner stays a partner");
        }

        [Test]
        public void WellKnown_MeansASharedPast_ButNeverForAStranger()
        {
            var sheet = new CharacterSheet();
            sheet.relationship.role = RelationshipRoles.Family;
            sheet.relationship.familiarity = 60f;
            PisaraReading.ApplySharedHistory(sheet);
            Assert.IsTrue(sheet.relationship.sharedHistory);

            var barely = new CharacterSheet();
            barely.relationship.role = RelationshipRoles.Acquaintance;
            barely.relationship.familiarity = 30f;
            PisaraReading.ApplySharedHistory(barely);
            Assert.IsFalse(barely.relationship.sharedHistory);

            var stranger = new CharacterSheet();
            stranger.relationship.familiarity = 90f;
            PisaraReading.ApplySharedHistory(stranger);
            Assert.IsFalse(stranger.relationship.sharedHistory, "a stranger never shares a past");

            var reply = GoodReply.Replace("\"sharedHistory\": true", "\"sharedHistory\": false");
            Assert.IsTrue(Read(new FakeProvider { Reply = reply }, false).Sheet.relationship.sharedHistory, "familiarity 70 with family");
        }

        // ---- The developer tool ----------------------------------------------------------------

        [Test]
        public void Tool_ReadsItsArguments()
        {
            var args = new[] { "Happi.exe", "-allowmulti", "-pisara-read", "C:\\temp\\p", "-pisara-model", "mistral-nemo:12b" };
            Assert.IsTrue(PisaraReadingTool.Requested(args));
            Assert.AreEqual("C:\\temp\\p", PisaraReadingTool.Value(args, PisaraReadingTool.Flag));
            Assert.AreEqual("mistral-nemo:12b", PisaraReadingTool.Value(args, PisaraReadingTool.ModelFlag));
            Assert.IsNull(PisaraReadingTool.Value(args, PisaraReadingTool.PromptFlag));
            Assert.IsFalse(PisaraReadingTool.Requested(new[] { "Happi.exe", "-pisara-read" }), "the flag needs a folder");
            Assert.IsFalse(PisaraReadingTool.Requested(null));
        }
    }
}
