using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-154: version history of persona files.</summary>
    public sealed class PersonaHistoryTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "happi-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        static PersonaDefinition Mira(string description)
        {
            var persona = PersonaDefinition.CreateStarter("mira");
            persona.DisplayName = "Mira";
            persona.Description = description;
            return persona;
        }

        [Test]
        public void Save_KeepsThePreviousVersion_NotTheFirstAndNotAnIdenticalOne()
        {
            Assert.IsTrue(PersonaStore.Save(_dir, Mira("one")));
            Assert.AreEqual(0, PersonaHistory.List(_dir, "mira").Count, "nothing to keep on the first save");

            Assert.IsTrue(PersonaStore.Save(_dir, Mira("one")));
            Assert.AreEqual(0, PersonaHistory.List(_dir, "mira").Count, "saving the same text keeps nothing");

            Assert.IsTrue(PersonaStore.Save(_dir, Mira("two")));
            var versions = PersonaHistory.List(_dir, "mira");
            Assert.AreEqual(1, versions.Count);
            StringAssert.StartsWith(Path.Combine(_dir, PersonaHistory.FolderName, "mira.json."), versions[0].Path);
            StringAssert.EndsWith(".bak", versions[0].Path);
            Assert.AreEqual("one", PersonaHistory.Restore(versions[0].Path).Description, "the version is the previous file");
            Assert.IsNotEmpty(versions[0].Label);

            // The history folder is not read as personas.
            var catalog = PersonaCatalog.Load(new string[0], _dir);
            Assert.AreEqual(1, catalog.Entries.Count);
            Assert.AreEqual("two", catalog.Find("mira").Description);
        }

        [Test]
        public void History_KeepsTheLastTen_NewestFirst()
        {
            var start = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(PersonaStore.Save(_dir, Mira("v0")));
            for (var i = 1; i <= 14; i++)
            {
                // Each save keeps the one before; the stamps are a second apart.
                var json = PersonaJson.Write(Mira("v" + i));
                Assert.IsNotNull(PersonaHistory.KeepPrevious(_dir, "mira", json, start.AddSeconds(i)), "kept " + i);
                Assert.IsTrue(AICompanion.Core.ProtectedLocalFile.Write(Path.Combine(_dir, "mira.json"), json));
            }

            var versions = PersonaHistory.List(_dir, "mira");
            Assert.AreEqual(PersonaHistory.Keep, versions.Count, "only the last 10 stay");
            Assert.AreEqual("v13", PersonaHistory.Restore(versions[0].Path).Description, "newest first");
            Assert.AreEqual("v4", PersonaHistory.Restore(versions[9].Path).Description);
            Assert.Greater(versions[0].SavedAtUtc, versions[9].SavedAtUtc);
        }

        [Test]
        public void TwoSavesInTheSameMillisecond_BothKept()
        {
            var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(PersonaStore.Save(_dir, Mira("a")));
            Assert.IsNotNull(PersonaHistory.KeepPrevious(_dir, "mira", PersonaJson.Write(Mira("b")), now));
            AICompanion.Core.ProtectedLocalFile.Write(Path.Combine(_dir, "mira.json"), PersonaJson.Write(Mira("b")));
            Assert.IsNotNull(PersonaHistory.KeepPrevious(_dir, "mira", PersonaJson.Write(Mira("c")), now));
            Assert.AreEqual(2, PersonaHistory.List(_dir, "mira").Count);
        }

        [Test]
        public void Restore_ReadsAVersion_AndBadInputIsSafe()
        {
            Assert.IsNull(PersonaHistory.Restore(null));
            Assert.IsNull(PersonaHistory.Restore(Path.Combine(_dir, "missing.bak")));
            Assert.AreEqual(0, PersonaHistory.List(_dir, "nobody").Count);
            Assert.AreEqual(0, PersonaHistory.List(null, "mira").Count);
            Assert.IsNull(PersonaHistory.KeepPrevious(_dir, "", "{}", DateTime.UtcNow));
        }
    }

    /// <summary>JUH-154: the persona grid's cards and menus.</summary>
    public sealed class PersonaGridTests
    {
        static PersonaEntry Entry(string id, string name, bool builtIn, bool userFile, bool picture = false, string shape = "round", string tagline = "")
        {
            var persona = PersonaDefinition.CreateStarter(id);
            persona.DisplayName = name;
            persona.HasPicture = picture;
            persona.PictureShape = shape;
            persona.Tagline = tagline;
            return new PersonaEntry(persona, builtIn, userFile);
        }

        [Test]
        public void Cards_MarkTheActiveOne_AndKeepNameTaglineAndShape()
        {
            var entries = new List<PersonaEntry>
            {
                Entry("happi", "Happi", true, false),
                Entry("mira", "Mira", false, true, true, "square", "  Bratty stepsister  "),
                Entry("nameless", "", false, true),
                null,
            };
            var cards = PersonaGrid.Cards(entries, "MIRA");
            Assert.AreEqual(3, cards.Count);
            Assert.IsFalse(cards[0].Active);
            Assert.IsTrue(cards[1].Active, "case does not matter");
            Assert.AreEqual("Bratty stepsister", cards[1].Tagline);
            Assert.IsTrue(cards[1].HasPicture);
            Assert.IsFalse(cards[1].Round);
            Assert.AreEqual("nameless", cards[2].Name, "no name: her id");
        }

        [Test]
        public void Menu_BuiltInsCantBeDeleted_SavedCopiesCan()
        {
            var cards = PersonaGrid.Cards(new List<PersonaEntry>
            {
                Entry("happi", "Happi", true, false),
                Entry("friend", "Leia", true, true),
                Entry("mira", "Mira", false, true),
            }, string.Empty);

            CollectionAssert.AreEqual(new[] { "Open in Studio", "Duplicate" }, PersonaGrid.MenuLabels(cards[0]));
            CollectionAssert.AreEqual(new[] { "Open in Studio", "Duplicate", "Delete saved copy" }, PersonaGrid.MenuLabels(cards[1]));
            CollectionAssert.AreEqual(new[] { "Open in Studio", "Duplicate", "Delete" }, PersonaGrid.MenuLabels(cards[2]));
            Assert.AreEqual(0, PersonaGrid.MenuLabels(null).Count);
        }

        [Test]
        public void Layout_ColumnsAndRows()
        {
            Assert.AreEqual(1, PersonaGrid.Columns(100f, 1f));
            Assert.AreEqual(2, PersonaGrid.Columns(320f, 1f));
            Assert.AreEqual(4, PersonaGrid.Columns(2000f, 1f), "at most four");
            Assert.AreEqual(1, PersonaGrid.Columns(320f, 2f), "wider cards at a higher scale");
            Assert.AreEqual(0, PersonaGrid.Rows(0, 3));
            Assert.AreEqual(1, PersonaGrid.Rows(3, 3));
            Assert.AreEqual(2, PersonaGrid.Rows(4, 3));
            Assert.AreEqual(5, PersonaGrid.Rows(5, 0), "zero columns counts as one");
        }

        [Test]
        public void CopyName_FindsAFreeOne()
        {
            var taken = new HashSet<string> { "Mira copy", "Mira copy 2" };
            Assert.AreEqual("Mira copy 3", PersonaGrid.CopyName("Mira", taken.Contains));
            Assert.AreEqual("Persona copy", PersonaGrid.CopyName("  ", null));
        }
    }

    /// <summary>JUH-154: describe her from the picture.</summary>
    public sealed class PictureDescribeTests
    {
        [Test]
        public void Gate_NeverTheCloud_InPrivateModeOrWithTheAdultGateOpen()
        {
            // keepsLocal is IntimateExpressions.KeepsLocal(privateMode): Private mode or the 18+ gate.
            var local = PictureDescribeGate.Choose(true, true, true);
            Assert.AreEqual(PictureDescribeRoute.Local, local.Route, "even when the cloud could see it");

            var none = PictureDescribeGate.Choose(true, true, false);
            Assert.AreEqual(PictureDescribeRoute.None, none.Route, "no local model: nothing is sent, the cloud is not a fallback");
            Assert.AreEqual(PictureDescribeGate.PrivateNoLocal, none.Reason);
        }

        [Test]
        public void Gate_PrefersTheCloudWhenItSeesPictures_ElseLocal_ElseNothing()
        {
            Assert.AreEqual(PictureDescribeRoute.Cloud, PictureDescribeGate.Choose(false, true, true).Route);
            Assert.AreEqual(PictureDescribeRoute.Local, PictureDescribeGate.Choose(false, false, true).Route);
            var nothing = PictureDescribeGate.Choose(false, false, false);
            Assert.AreEqual(PictureDescribeRoute.None, nothing.Route);
            Assert.AreEqual(PictureDescribeGate.NoModel, nothing.Reason);
        }

        [Test]
        public void KeepsLocal_FollowsPrivateModeAndTheGate()
        {
            try
            {
                AICompanion.Core.IntimateExpressions.Configure(false, false, false);
                Assert.IsFalse(AICompanion.Core.IntimateExpressions.KeepsLocal(false));
                Assert.IsTrue(AICompanion.Core.IntimateExpressions.KeepsLocal(true), "Private mode");
                AICompanion.Core.IntimateExpressions.Configure(true, true, true);
                Assert.IsTrue(AICompanion.Core.IntimateExpressions.KeepsLocal(false), "the open 18+ gate");
                Assert.AreEqual(PictureDescribeRoute.Local,
                    PictureDescribeGate.Choose(AICompanion.Core.IntimateExpressions.KeepsLocal(false), true, true).Route);
            }
            finally
            {
                AICompanion.Core.IntimateExpressions.Reset();
            }
        }

        [Test]
        public void Reply_IsCleaned_AndAppendedOnlyWhenAccepted()
        {
            Assert.AreEqual("She has long dark hair.", PictureDescribeGate.Clean("<animation emotion=\"happy\" />\"She has long dark hair.\""));
            Assert.AreEqual(string.Empty, PictureDescribeGate.Clean("   "));
            Assert.LessOrEqual(PictureDescribeGate.Clean(new string('a', 5000)).Length, 1000);

            Assert.AreEqual("A student.\n\nAppearance:\nShe wears glasses.", PictureDescribeGate.Append("A student.", "She wears glasses."));
            Assert.AreEqual("Appearance:\nShe wears glasses.", PictureDescribeGate.Append("", "She wears glasses."));
            Assert.AreEqual("A student.", PictureDescribeGate.Append("A student.", "  "), "nothing to add: unchanged");
            StringAssert.Contains("non-explicit", PictureDescribeGate.Prompt);
        }
    }
}
