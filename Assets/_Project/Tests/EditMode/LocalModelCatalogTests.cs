using System.IO;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>The local model dropdown reads the names Ollama actually installed.</summary>
    public sealed class LocalModelCatalogTests
    {
        string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "happi-models-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        void Manifest(string relative)
        {
            var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{}");
        }

        [Test]
        public void ReadsOllamaAndHuggingFaceNames()
        {
            Manifest("registry.ollama.ai/library/dolphin3/8b");
            Manifest("registry.ollama.ai/library/mistral-nemo/12b");
            Manifest("hf.co/HauhauCS/Qwen3.5-9B-Uncensored-HauhauCS-Aggressive/Q4_K_M");

            var models = LocalModelCatalog.FromManifestFolder(_root);

            CollectionAssert.Contains(models, "dolphin3:8b");
            CollectionAssert.Contains(models, "mistral-nemo:12b");
            CollectionAssert.Contains(models, "hf.co/HauhauCS/Qwen3.5-9B-Uncensored-HauhauCS-Aggressive:Q4_K_M");
        }

        [Test]
        public void ChoicesKeepTheCurrentModelAndDoNotDuplicateIt()
        {
            Manifest("registry.ollama.ai/library/dolphin3/8b");

            var models = LocalModelCatalog.ChoicesFromFolder(_root, "dolphin3:8b");

            Assert.AreEqual(1, models.Count);
            Assert.AreEqual("dolphin3:8b", models[0]);
        }
    }
}
