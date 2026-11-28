using System.Collections.Generic;
using AICompanion.AI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-230: pure URL/JSON coverage for the installed-model dropdown. The network fetch lives
    /// in SettingsPanel and is checked by hand; this test has no endpoint.
    /// </summary>
    public sealed class OllamaModelCatalogTests
    {
        [Test]
        public void TagsUrl_AcceptsTheOllamaRootAndV1Address()
        {
            Assert.AreEqual("http://127.0.0.1:11434/api/tags", OllamaModelCatalog.TagsUrl("http://127.0.0.1:11434"));
            Assert.AreEqual("http://127.0.0.1:11434/api/tags", OllamaModelCatalog.TagsUrl("http://127.0.0.1:11434/v1"));
            Assert.AreEqual("http://127.0.0.1:11434/api/tags", OllamaModelCatalog.TagsUrl("http://127.0.0.1:11434/api/tags"));
            Assert.AreEqual("http://127.0.0.1:11634/api/tags", OllamaModelCatalog.TagsUrl("http://127.0.0.1:11634/v1/"));
        }

        [Test]
        public void Parse_UsesNamesInServerOrderAndDropsDuplicatesAndBlanks()
        {
            const string json = "{\"models\":[" +
                                "{\"name\":\"happi-qwen35-9b:latest\",\"model\":\"ignored\"}," +
                                "{\"model\":\"happi-defiant-9b:latest\"}," +
                                "{\"name\":\"\"}," +
                                "{\"name\":\"happi-qwen35-9b:latest\"}" +
                                "]}";
            var names = OllamaModelCatalog.Parse(json);
            CollectionAssert.AreEqual(new[] { "happi-qwen35-9b:latest", "happi-defiant-9b:latest" }, names);
        }

        [Test]
        public void Parse_ReturnsEmptyForMissingOrBrokenJson()
        {
            CollectionAssert.IsEmpty(OllamaModelCatalog.Parse(null));
            CollectionAssert.IsEmpty(OllamaModelCatalog.Parse(string.Empty));
            CollectionAssert.IsEmpty(OllamaModelCatalog.Parse("{}"));
            CollectionAssert.IsEmpty(OllamaModelCatalog.Parse("not json"));
        }
    }
}
