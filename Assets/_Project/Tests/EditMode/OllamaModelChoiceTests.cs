using System.Collections.Generic;
using AICompanion.AI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-243: the local model dropdown's choices, labels and the "in use" line.</summary>
    public sealed class OllamaModelChoiceTests
    {
        static readonly List<string> Installed = new List<string>
        {
            "happi-qwen35-9b:latest", "happi-defiant-9b:latest", "rocinante:12b",
        };

        [Test]
        public void SameTag_IgnoresLatestAndCase()
        {
            Assert.IsTrue(OllamaModelCatalog.SameTag("happi-qwen35-9b", "happi-qwen35-9b:latest"));
            Assert.IsTrue(OllamaModelCatalog.SameTag("Happi-Qwen35-9B:latest", "happi-qwen35-9b"));
            Assert.IsFalse(OllamaModelCatalog.SameTag("rocinante", "rocinante:12b"), "only :latest is the default tag");
            Assert.IsTrue(OllamaModelCatalog.IsInstalled("happi-defiant-9b", Installed));
            Assert.IsFalse(OllamaModelCatalog.IsInstalled("llama3", Installed));
        }

        [Test]
        public void Options_ShowEachModelOnce_AndKeepAMissingChoice()
        {
            var options = OllamaModelCatalog.Options("happi-qwen35-9b", Installed, false);
            CollectionAssert.AreEqual(Installed, options, "the bare tag is the installed one, listed once");
            Assert.AreEqual("happi-qwen35-9b:latest", OllamaModelCatalog.Selected("happi-qwen35-9b", options), "and it shows as chosen");

            options = OllamaModelCatalog.Options("llama3", Installed, false);
            Assert.AreEqual("llama3", options[0], "a choice Ollama doesn't list stays visible");
            Assert.AreEqual(4, options.Count);
            Assert.AreEqual("llama3 (not installed)", OllamaModelCatalog.Label("llama3", Installed, true, null));
            Assert.AreEqual("llama3", OllamaModelCatalog.Label("llama3", Installed, false, null), "not marked before the list loads");
        }

        [Test]
        public void Options_ForAPersona_StartWithTheGlobalModel()
        {
            var options = OllamaModelCatalog.Options(string.Empty, Installed, true);
            Assert.AreEqual(string.Empty, options[0]);
            Assert.AreEqual(4, options.Count);
            Assert.AreEqual(string.Empty, OllamaModelCatalog.Selected(string.Empty, options));
            Assert.AreEqual("Use the global model (happi-qwen35-9b)",
                OllamaModelCatalog.Label(string.Empty, Installed, true, "happi-qwen35-9b"));
            Assert.AreEqual("rocinante:12b", OllamaModelCatalog.Selected("rocinante:12b", OllamaModelCatalog.Options("rocinante:12b", Installed, true)));
        }

        [Test]
        public void InUse_SaysWhichModel_AndWhenAPersonaOverridesIt()
        {
            Assert.AreEqual("In use now: happi-qwen35-9b:latest.",
                OllamaModelCatalog.InUse("happi-qwen35-9b", "happi-qwen35-9b:latest", "Happi", true));
            var line = OllamaModelCatalog.InUse("happi-qwen35-9b", "happi-defiant-9b", "Mira", true);
            StringAssert.StartsWith("Mira overrides this and uses happi-defiant-9b.", line);
            StringAssert.Contains("This persona", line);
            Assert.AreEqual(string.Empty, OllamaModelCatalog.InUse("happi-qwen35-9b", "happi-defiant-9b", "Mira", false), "not on the cloud route");
        }
    }
}
