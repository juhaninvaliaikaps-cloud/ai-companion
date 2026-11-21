using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AICompanion.Core;
using AICompanion.NewUI;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-159: editing lorebook entries in the Studio, and exporting her lorebook.</summary>
    public sealed class LorebookExportTests
    {
        static PersonaDefinition Mira()
        {
            var persona = PersonaDefinition.CreateStarter("mira");
            persona.DisplayName = "Mira";
            persona.Description = "Mira runs the night ferry.";
            persona.AdultNotes = string.Empty;
            return persona;
        }

        static Lorebook Book(bool adult)
        {
            var book = new Lorebook { Name = "Harbor", Adult = adult };
            book.Entries.Add(new LoreEntry { Name = "Lighthouse", Keys = new List<string> { "lighthouse", "beacon" }, Content = "The lighthouse \"Old Tom\" has been dark for years.", Order = 10 });
            book.Entries.Add(new LoreEntry { Name = "Inn", Content = "Her mother runs the inn.\nIt has six rooms.", Constant = true, Order = 20 });
            return book;
        }

        [Test]
        public void CardJson_CarriesHerLorebook_AndReadsBack()
        {
            var json = CharacterCards.ToCardJson(Mira(), false, Book(false));
            StringAssert.Contains("\"character_book\":", json);
            var card = CharacterCards.Import(Encoding.UTF8.GetBytes(json));
            Assert.IsTrue(card.Ok, card.Error);
            Assert.AreEqual("Mira runs the night ferry.", card.Persona.Description, "the rest of the card still reads");
            Assert.IsNotNull(card.Lorebook);
            Assert.AreEqual(2, card.Lorebook.Entries.Count);
            Assert.AreEqual("The lighthouse \"Old Tom\" has been dark for years.", card.Lorebook.Entries[0].Content);
            Assert.AreEqual("Her mother runs the inn.\nIt has six rooms.", card.Lorebook.Entries[1].Content);
            Assert.IsTrue(card.Lorebook.Entries[1].Constant);
            CollectionAssert.AreEqual(new[] { "lighthouse", "beacon" }, card.Lorebook.Entries[0].Keys);
        }

        [Test]
        public void WithoutALorebook_TheCardIsAsBefore()
        {
            Assert.AreEqual(CharacterCards.ToCardJson(Mira(), false), CharacterCards.ToCardJson(Mira(), false, null));
            Assert.AreEqual(CharacterCards.ToCardJson(Mira(), false), CharacterCards.ToCardJson(Mira(), false, new Lorebook()), "an empty book adds nothing");
        }

        [Test]
        public void An18PlusLorebook_GoesInOnlyWhen18PlusIsTicked_AndTagsTheCard()
        {
            var left = CharacterCards.ToCardJson(Mira(), false, Book(true));
            StringAssert.DoesNotContain("character_book", left);
            StringAssert.DoesNotContain("Old Tom", left);
            Assert.IsFalse(CharacterCards.ExportsLorebook(Book(true), false));

            var card = CharacterCards.Import(Encoding.UTF8.GetBytes(CharacterCards.ToCardJson(Mira(), true, Book(true))));
            Assert.IsTrue(card.Ok, card.Error);
            Assert.IsTrue(card.Adult, "a card with an 18+ lorebook is tagged nsfw");
            Assert.IsNotNull(card.Lorebook);
            Assert.IsTrue(card.Lorebook.Adult);
        }

        [Test]
        public void CardPng_CarriesHerLorebook()
        {
            var png = CharacterCards.Export(Mira(), null, CardExportFormat.CardPng, false, Book(false));
            var card = CharacterCards.Import(png);
            Assert.IsTrue(card.Ok, card.Error);
            Assert.IsNotNull(card.Lorebook);
            Assert.AreEqual(2, card.Lorebook.Entries.Count);
        }

        [Test]
        public void TheZip_HasHerLorebookJson()
        {
            var withBook = Encoding.ASCII.GetString(CharacterCards.Export(Mira(), null, CardExportFormat.Zip, false, Book(false)));
            StringAssert.Contains("mira.lorebook.json", withBook);
            var adultLeftOut = Encoding.ASCII.GetString(CharacterCards.Export(Mira(), null, CardExportFormat.Zip, false, Book(true)));
            StringAssert.DoesNotContain("mira.lorebook.json", adultLeftOut);
        }

        [Test]
        public void EditedKeys_AreTrimmed_WithoutRepeats_AndCapped()
        {
            var keys = new List<string> { "old" };
            PersonaStudioView.SetLoreKeys(keys, " tea,  teahouse ,tea,, " + new string('x', Lorebook.MaxKeyLength + 1));
            CollectionAssert.AreEqual(new[] { "tea", "teahouse" }, keys);

            var many = new StringBuilder();
            for (var i = 0; i < Lorebook.MaxKeys + 5; i++)
                many.Append("k").Append(i).Append(',');
            PersonaStudioView.SetLoreKeys(keys, many.ToString());
            Assert.AreEqual(Lorebook.MaxKeys, keys.Count);
        }

        [Test]
        public void TheStudioAndTheExport_AreWiredUp()
        {
            var root = Path.Combine(Application.dataPath, "_Project/Scripts/");
            var studio = File.ReadAllText(root + "UI/NewUI/PersonaStudioView.cs");
            StringAssert.Contains("\"Export lorebook JSON…\"", studio);
            StringAssert.Contains("\"Add an entry\"", studio);
            StringAssert.Contains("\"Delete this entry\"", studio);
            StringAssert.Contains("\"Start a lorebook\"", studio);
            StringAssert.Contains("LorebookWriter.ToJson(_lore)", studio);
            var cards = File.ReadAllText(root + "Settings/SettingsPanel.Cards.cs");
            StringAssert.Contains("CharacterCards.Export(persona, picture, format, includeAdult, lore)", cards);
        }
    }
}
