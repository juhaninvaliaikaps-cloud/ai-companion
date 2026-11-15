using System;
using System.IO;
using AICompanion.AI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-126: the separate Pisara key, its environment override and Grok fallback.</summary>
    public sealed class PisaraApiKeyStoreTests
    {
        string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "ai-companion-pisara-" + Guid.NewGuid().ToString("N"), "pisara-api-key.json");
        }

        [TearDown]
        public void TearDown()
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        [Test]
        public void Save_Read_AndDelete_UseThePisaraField()
        {
            PisaraApiKeyStore.Save(_path, "pisara-secret", new PlainSecretProtector());

            Assert.IsTrue(File.Exists(_path));
            StringAssert.Contains("pisaraApiKey", File.ReadAllText(_path));
            Assert.IsTrue(PisaraApiKeyStore.TryRead(_path, new PlainSecretProtector(), out var key));
            Assert.AreEqual("pisara-secret", key);

            PisaraApiKeyStore.Delete(_path);
            Assert.IsFalse(File.Exists(_path));
        }

        [Test]
        public void Source_PrefersItsOwnEnvironmentKey()
        {
            var source = new PisaraApiKeySource(
                name => name == PisaraApiKeyStore.EnvName ? "env-pisara" : null,
                () => _path,
                new PlainSecretProtector(),
                new FixedSource("grok-key"));

            Assert.IsTrue(source.TryGetOwn(out var own));
            Assert.AreEqual("env-pisara", own);
        }

        [Test]
        public void Source_ReadsItsOwnFile()
        {
            PisaraApiKeyStore.Save(_path, "file-pisara", new PlainSecretProtector());
            var source = new PisaraApiKeySource(
                _ => null,
                () => _path,
                new PlainSecretProtector(),
                new FixedSource("grok-key"));

            Assert.IsTrue(source.TryGetOwn(out var key));
            Assert.AreEqual("file-pisara", key);
        }

        [Test]
        public void Source_ExposesTheGrokKeySeparately()
        {
            var source = new PisaraApiKeySource(
                _ => null,
                () => _path,
                new PlainSecretProtector(),
                new FixedSource("grok-key"));

            Assert.IsFalse(source.TryGetOwn(out _));
            Assert.IsTrue(source.TryGetGrok(out var key));
            Assert.AreEqual("grok-key", key);
        }

        [Test]
        public void Source_ReportsNoKeyWhenNeitherExists()
        {
            var source = new PisaraApiKeySource(
                _ => null,
                () => _path,
                new PlainSecretProtector(),
                new FixedSource(null));

            Assert.IsFalse(source.TryGetOwn(out var key));
            Assert.IsNull(key);
            Assert.IsFalse(source.TryGetGrok(out key));
            Assert.IsNull(key);
        }

        sealed class FixedSource : IApiKeySource
        {
            readonly string _key;

            public FixedSource(string key)
            {
                _key = key;
            }

            public bool TryGet(out string key)
            {
                key = _key;
                return !string.IsNullOrWhiteSpace(key);
            }
        }
    }
}
