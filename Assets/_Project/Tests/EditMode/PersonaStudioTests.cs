using System;
using System.IO;
using AICompanion.Core;
using AICompanion.NewUI;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-150: the Persona Studio's data: description, picture, examples, 18+ notes.</summary>
    public sealed class PersonaStudioTests
    {
        string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "happi-studio-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            IntimateExpressions.Reset();
            AdultNotesNow.Reset();
            try
            {
                if (Directory.Exists(_folder))
                    Directory.Delete(_folder, true);
            }
            catch (Exception)
            {
            }
        }

        [Test]
        public void TheNewFields_RoundTrip_AndAnOldFileLoadsWithoutThem()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.Tagline = "Your sleepy gamer friend";
            persona.Description = "A long description.\n\nWith paragraphs.";
            persona.Greeting = "Oh, hi!";
            persona.ExampleLines = "You: Hi!\nHer: Oh, it's you again.";
            persona.AdultNotes = "Private.";
            persona.HasPicture = true;
            persona.PictureShape = PersonaPictureShapes.Square;

            var back = PersonaJson.Parse(PersonaJson.Write(persona));
            Assert.AreEqual("Your sleepy gamer friend", back.Tagline);
            Assert.AreEqual("A long description.\n\nWith paragraphs.", back.Description);
            Assert.AreEqual("Oh, hi!", back.Greeting);
            Assert.AreEqual("You: Hi!\nHer: Oh, it's you again.", back.ExampleLines);
            Assert.AreEqual("Private.", back.AdultNotes);
            Assert.IsTrue(back.HasPicture);
            Assert.AreEqual(PersonaPictureShapes.Square, back.PictureShape);

            var old = PersonaJson.Parse("{\n  \"version\": 1,\n  \"id\": \"old\",\n  \"displayName\": \"Old\",\n  \"personality\": \"You are Old.\"\n}\n");
            Assert.IsNotNull(old);
            Assert.AreEqual(string.Empty, old.Description);
            Assert.IsFalse(old.HasPicture);
            Assert.AreEqual(PersonaPictureShapes.Round, old.PictureShape);
            Assert.AreEqual("You are Old.", old.Personality);
        }

        [Test]
        public void TheLimits_AreTheStudios()
        {
            var persona = PersonaDefinition.CreateStarter("big");
            persona.Description = new string('a', PersonaDefinition.MaxDescriptionLength + 500);
            persona.Tagline = "one\nline";
            persona.Normalize();
            Assert.AreEqual(32000, persona.Description.Length);
            Assert.AreEqual("oneline", persona.Tagline);
        }

        [Test]
        public void AShortDescription_IsWhatTheModelSees_ALongOneIsCutAtAParagraph()
        {
            Assert.AreEqual("Short.", PersonaDescription.ModelText("  Short.  "));
            Assert.IsTrue(PersonaDescription.Fits("Short."));
            StringAssert.Contains("sees all of it", PersonaDescription.Meter("Short."));

            var first = new string('a', 3000);
            var second = new string('b', 3000);
            var text = first + "\n\n" + second;
            Assert.IsFalse(PersonaDescription.Fits(text));
            Assert.AreEqual(first, PersonaDescription.ModelText(text), "cut at the paragraph break");
            StringAssert.Contains("Pisara reads all of it", PersonaDescription.Meter(text));

            var noBreaks = new string('c', 9000);
            Assert.AreEqual(PersonaDescription.ModelBudget, PersonaDescription.ModelText(noBreaks).Length);
        }

        [Test]
        public void ExampleLines_GoIntoHerPrompt()
        {
            var persona = PersonaDefinition.CreateStarter("lola");
            persona.ExampleLines = "You: Hi!\nHer: Oh, it's you again.";
            var prompt = PersonaText.Build(persona, "Lola", "friend");
            StringAssert.Contains(PersonaText.ExamplesLabel, prompt);
            StringAssert.EndsWith("Her: Oh, it's you again.", prompt);
            StringAssert.Contains(PersonaText.StyleLabel, prompt);
        }

        [Test]
        public void AdultNotes_ReachChat_OnlyThroughTheOpenGate()
        {
            AdultNotesNow.Set("Private notes.");
            Assert.AreEqual("You are Lola.", AdultNotesNow.Append("You are Lola."), "gate closed: nothing added");

            IntimateExpressions.Configure(true, true, true);
            StringAssert.Contains("Private notes.", AdultNotesNow.Append("You are Lola."));
            StringAssert.Contains(AdultNotesNow.Label, AdultNotesNow.Append("You are Lola."));

            AdultNotesNow.Set("   ");
            Assert.AreEqual("You are Lola.", AdultNotesNow.Append("You are Lola."));
        }

        [Test]
        public void ThePicture_IsKeptEncrypted_AndRemovable()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 };
            Assert.IsTrue(PersonaPictureStore.Save(_folder, "lola", png));
            var path = PersonaPictureStore.PathFor(_folder, "lola");
            StringAssert.EndsWith("lola.picture", path);
            CollectionAssert.AreEqual(png, PersonaPictureStore.Load(_folder, "lola"));
            Assert.IsTrue(PersonaPictureStore.Delete(_folder, "lola"));
            Assert.IsNull(PersonaPictureStore.Load(_folder, "lola"));
            Assert.IsFalse(PersonaPictureStore.Save(_folder, "lola", new byte[0]));
            var evil = PersonaPictureStore.PathFor(_folder, "../../evil");
            StringAssert.StartsWith(_folder, evil, "a path can't leave the folder");
            StringAssert.DoesNotContain("..", evil);
        }

        [Test]
        public void TheCrop_IsTheCentreSquare()
        {
            int x;
            int y;
            int side;
            PictureCrop.Square(1920, 1080, out x, out y, out side);
            Assert.AreEqual(1080, side);
            Assert.AreEqual(420, x);
            Assert.AreEqual(0, y);
            PictureCrop.Square(600, 900, out x, out y, out side);
            Assert.AreEqual(600, side);
            Assert.AreEqual(0, x);
            Assert.AreEqual(150, y);
        }

        [Test]
        public void EveryTemplate_GivesAStart()
        {
            foreach (var id in PersonaTemplates.Ids)
            {
                var persona = PersonaTemplates.Create(id, "draft");
                Assert.IsFalse(string.IsNullOrWhiteSpace(persona.Description), id);
                Assert.AreEqual(persona.Description, persona.Personality, id);
                Assert.AreEqual(TemperamentModes.Manual, persona.TemperamentMode, id);
                Assert.AreEqual(PersonaTemperaments.Normalize(id), persona.Temperament, id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(PersonaTemplates.Name(id)), id);
            }

            Assert.AreEqual(PersonaPictureShapes.Square, PersonaPictureShapes.Normalize("SQUARE"));
            Assert.AreEqual(PersonaPictureShapes.Round, PersonaPictureShapes.Normalize("hexagon"));
        }
    }
}
