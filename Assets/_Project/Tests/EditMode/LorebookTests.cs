using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-156: lorebook import (World Info, card books, PNG cards), matching and storage.</summary>
    public sealed class LorebookTests
    {
        const string WorldInfo = "{\"name\":\"Harbor\",\"entries\":{"
            + "\"1\":{\"uid\":1,\"key\":[\"lighthouse\"],\"keysecondary\":[],\"comment\":\"The lighthouse\",\"content\":\"The lighthouse on the north cliff has been dark for years.\",\"constant\":false,\"selective\":true,\"order\":50,\"disable\":false},"
            + "\"0\":{\"uid\":0,\"key\":[\"Mira\"],\"comment\":\"Mira\",\"content\":\"{{char}} grew up in the harbor town.\",\"constant\":true,\"order\":100,\"disable\":false},"
            + "\"2\":{\"uid\":2,\"key\":[\"ferry\"],\"comment\":\"Old ferry\",\"content\":\"The old ferry sank.\",\"disable\":true}"
            + "}}";

        static string CardWithBook(string tags)
        {
            return "{\"spec\":\"chara_card_v2\",\"spec_version\":\"2.0\",\"data\":{\"name\":\"Mira\",\"description\":\"{{char}} runs the ferry.\","
                + "\"tags\":[" + tags + "],"
                + "\"character_book\":{\"name\":\"Mira's book\",\"entries\":["
                + "{\"keys\":[\"tea\",\"teahouse\"],\"secondary_keys\":[\"rain\"],\"selective\":true,\"content\":\"{{char}} and {{user}} meet at the teahouse when it rains.\",\"enabled\":true,\"insertion_order\":10,\"name\":\"Teahouse\"},"
                + "{\"keys\":[\"captain\"],\"content\":\"The captain is her uncle.\",\"enabled\":true,\"insertion_order\":20}"
                + "]}}}";
        }

        [Test]
        public void JsonTree_ReadsNestedValues_AndRefusesBrokenJson()
        {
            object value;
            Assert.IsTrue(JsonTree.TryParse("{\"a\":[1,2.5,true,null,\"x\\n\\u00e4\"],\"b\":{\"c\":false}}", out value));
            var o = JsonTree.Object(value);
            var a = (List<object>)o["a"];
            Assert.AreEqual(2.5d, a[1]);
            Assert.AreEqual(true, a[2]);
            Assert.IsNull(a[3]);
            Assert.AreEqual("x\nä", a[4]);
            Assert.IsFalse(JsonTree.Bool(JsonTree.Object(o, "b"), "c", true));
            Assert.IsFalse(JsonTree.TryParse("{\"a\":1", out value));
            Assert.IsFalse(JsonTree.TryParse("{\"a\":1} trailing", out value));
            Assert.IsFalse(JsonTree.TryParse(new string('[', JsonTree.MaxDepth + 5), out value), "too deep");
        }

        [Test]
        public void WorldInfo_MapsEntries_InTheirNumberOrder()
        {
            var import = LorebookImport.FromJson(WorldInfo, "Mira");
            Assert.IsTrue(import.Ok, import.Error);
            var book = import.Book;
            Assert.AreEqual("Harbor", book.Name);
            Assert.AreEqual(3, book.Entries.Count);
            Assert.AreEqual("Mira", book.Entries[0].Name, "entries keyed \"0\", \"1\", \"2\" keep that order");
            Assert.IsTrue(book.Entries[0].Constant);
            Assert.AreEqual("Mira grew up in the harbor town.", book.Entries[0].Content, "{{char}} becomes her name");
            Assert.AreEqual(50, book.Entries[1].Order);
            CollectionAssert.AreEqual(new[] { "lighthouse" }, book.Entries[1].Keys);
            Assert.IsFalse(book.Entries[2].Enabled, "disable: true");
            Assert.IsFalse(book.Adult);
        }

        [Test]
        public void CardJson_BringsItsBook_AndAnAdultCardsBookIsAdult()
        {
            var card = CharacterCards.Import(Encoding.UTF8.GetBytes(CardWithBook("\"slice of life\"")));
            Assert.IsTrue(card.Ok, card.Error);
            Assert.IsNotNull(card.Lorebook);
            Assert.AreEqual(2, card.Lorebook.Entries.Count);
            Assert.AreEqual("Mira and the user meet at the teahouse when it rains.", card.Lorebook.Entries[0].Content);
            CollectionAssert.AreEqual(new[] { "rain" }, card.Lorebook.Entries[0].SecondaryKeys);
            Assert.IsFalse(card.Lorebook.Adult);
            CollectionAssert.Contains(card.Notes, CharacterCards.LorebookNote(card.Lorebook));

            var adult = CharacterCards.Import(Encoding.UTF8.GetBytes(CardWithBook("\"nsfw\"")));
            Assert.IsTrue(adult.Lorebook.Adult, "an 18+ card's book is used only with the 18+ gate open");
        }

        [Test]
        public void CardPng_BringsItsBookToo()
        {
            var png = CharacterCards.WithCardChunk(CharacterCards.PlaceholderPng(8, 10, 20, 30), CharacterCards.KeywordV2, CardWithBook(string.Empty));
            var card = CharacterCards.Import(png);
            Assert.IsTrue(card.Ok, card.Error);
            Assert.IsNotNull(card.PicturePng);
            Assert.IsNotNull(card.Lorebook, "a Character Tavern PNG carries the whole card, lorebook included");
            Assert.AreEqual(2, card.Lorebook.Entries.Count);

            var plain = CharacterCards.Import(Encoding.UTF8.GetBytes("{\"spec\":\"chara_card_v2\",\"data\":{\"name\":\"Aino\",\"description\":\"Shy.\"}}"));
            Assert.IsTrue(plain.Ok, plain.Error);
            Assert.IsNull(plain.Lorebook);
        }

        [Test]
        public void OtherShapes_NovelAiAndAgnaistic_AreRead()
        {
            var novel = LorebookImport.FromJson("{\"lorebookVersion\":5,\"entries\":[{\"displayName\":\"Docks\",\"text\":\"The docks smell of tar.\",\"keys\":[\"docks\"],\"enabled\":true}]}", "Mira");
            Assert.IsTrue(novel.Ok, novel.Error);
            Assert.AreEqual("The docks smell of tar.", novel.Book.Entries[0].Content);
            Assert.AreEqual("Docks", novel.Book.Entries[0].Name);

            var agnai = LorebookImport.FromJson("{\"kind\":\"memory\",\"entries\":[{\"name\":\"Bell\",\"entry\":\"The bell rings at noon.\",\"keywords\":[\"bell\"],\"enabled\":true}]}", "Mira");
            Assert.IsTrue(agnai.Ok, agnai.Error);
            CollectionAssert.AreEqual(new[] { "bell" }, agnai.Book.Entries[0].Keys);

            var keyString = LorebookImport.FromJson("{\"entries\":[{\"key\":\"fog, mist\",\"content\":\"Fog rolls in at dusk.\"}]}", "Mira");
            CollectionAssert.AreEqual(new[] { "fog", "mist" }, keyString.Book.Entries[0].Keys, "comma-separated keys");

            Assert.IsFalse(LorebookImport.FromJson("{\"name\":\"no entries\"}", "Mira").Ok);
            Assert.IsFalse(LorebookImport.FromJson("not json", "Mira").Ok);
        }

        [Test]
        public void Import_DropsSecrets_AndCapsLongEntries()
        {
            var json = "{\"entries\":[{\"key\":[\"a\"],\"content\":\"api_key=sk-1234567890abcdef\"},{\"key\":[\"b\"],\"content\":\"" + new string('x', Lorebook.MaxEntryLength + 10) + "\"}]}";
            var import = LorebookImport.FromJson(json, "Mira");
            Assert.IsTrue(import.Ok, import.Error);
            Assert.AreEqual(1, import.Book.Entries.Count);
            Assert.AreEqual(Lorebook.MaxEntryLength, import.Book.Entries[0].Content.Length);
            CollectionAssert.Contains(import.Notes, LorebookImport.SecretNote);
            CollectionAssert.Contains(import.Notes, LorebookImport.TrimmedNote);
        }

        [Test]
        public void Matcher_UsesWholeWords_SecondaryKeys_ConstantsFirst_AndTheBudget()
        {
            var book = LorebookImport.FromJson(CardWithBook(string.Empty), "Mira").Book;
            book.Entries.Add(new LoreEntry { Name = "Always", Content = "She hates mornings.", Constant = true, Order = 999 });

            var none = LoreMatcher.Select(book, "I like teapots.", false);
            Assert.AreEqual(1, none.Count, "only the constant one: \"teapots\" isn't \"tea\"");

            var noRain = LoreMatcher.Select(book, "Want some TEA?", false);
            Assert.AreEqual(1, noRain.Count, "the teahouse entry also needs \"rain\"");

            var both = LoreMatcher.Select(book, "Want some tea? It's going to rain. Ask the Captain.", false);
            Assert.AreEqual(3, both.Count);
            Assert.AreEqual("Always", both[0].Name, "constant entries first");
            Assert.AreEqual("Teahouse", both[1].Name, "then insertion order");

            var tight = LoreMatcher.Select(book, "tea rain captain", false, 30);
            Assert.AreEqual(1, tight.Count, "the budget keeps only what fits");

            StringAssert.StartsWith(LoreMatcher.Heading, LoreMatcher.Block(book, "captain", false));
            Assert.IsTrue(LoreMatcher.Contains("Mira's ferry", "Mira", false));
            Assert.IsFalse(LoreMatcher.Contains("Miranda", "Mira", false));
        }

        [Test]
        public void An18PlusBook_IsSilentWhileTheGateIsClosed()
        {
            var book = LorebookImport.FromJson(CardWithBook("\"nsfw\""), "Mira").Book;
            Assert.IsTrue(book.Adult);
            Assert.AreEqual(0, LoreMatcher.Select(book, "captain", false).Count);
            Assert.AreEqual(1, LoreMatcher.Select(book, "captain", true).Count);
        }

        [Test]
        public void TheWriter_RoundTrips_WithThe18PlusFlag()
        {
            var book = LorebookImport.FromJson(WorldInfo, "Mira").Book;
            book.Adult = true;
            var again = LorebookImport.FromJson(LorebookWriter.ToJson(book), null);
            Assert.IsTrue(again.Ok, again.Error);
            Assert.IsTrue(again.Book.Adult);
            Assert.AreEqual(book.Entries.Count, again.Book.Entries.Count);
            for (var i = 0; i < book.Entries.Count; i++)
            {
                Assert.AreEqual(book.Entries[i].Content, again.Book.Entries[i].Content);
                Assert.AreEqual(book.Entries[i].Enabled, again.Book.Entries[i].Enabled);
                Assert.AreEqual(book.Entries[i].Constant, again.Book.Entries[i].Constant);
                Assert.AreEqual(book.Entries[i].Order, again.Book.Entries[i].Order);
                CollectionAssert.AreEqual(book.Entries[i].Keys, again.Book.Entries[i].Keys);
            }
        }

        [Test]
        public void TheStore_KeepsItEncryptedBesideHerFile()
        {
            var folder = Path.Combine(Path.GetTempPath(), "happi-lore-" + Guid.NewGuid().ToString("N"));
            try
            {
                var book = LorebookImport.FromJson(WorldInfo, "Mira").Book;
                Assert.IsTrue(LorebookStore.Save(folder, "mira", book));
                var path = LorebookStore.PathFor(folder, "mira");
                Assert.IsTrue(File.Exists(path));
                StringAssert.DoesNotContain("lighthouse", File.ReadAllText(path), "encrypted at rest");
                Assert.AreEqual(3, LorebookStore.Load(folder, "mira").Entries.Count);
                Assert.IsTrue(LorebookStore.Save(folder, "mira", null));
                Assert.IsFalse(File.Exists(path), "saving no lorebook removes the file");
                Assert.IsNull(LorebookStore.PathFor(folder, "../.."));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Test]
        public void ChatAndSettings_AreWiredUp()
        {
            var root = Path.Combine(Application.dataPath, "_Project/Scripts/");
            var chat = File.ReadAllText(root + "UI/ChatPanel.cs");
            Assert.Greater(chat.IndexOf("prompt = WithLore(prompt, text);", StringComparison.Ordinal), chat.IndexOf("prompt = WithScene(prompt, privateMode, provider);", StringComparison.Ordinal));
            StringAssert.Contains("LoreNow.Set(", File.ReadAllText(root + "Settings/SettingsApplicator.cs"));
            var studio = File.ReadAllText(root + "UI/NewUI/PersonaStudioView.cs");
            StringAssert.Contains("\"Import lorebook JSON…\"", studio);
            StringAssert.Contains("_settings.StudioSaveLore(savedId, _lore)", studio);
            var grid = File.ReadAllText(root + "Settings/SettingsPanel.Grid.cs");
            StringAssert.Contains("LorebookStore.Delete(", grid);
        }
    }
}
