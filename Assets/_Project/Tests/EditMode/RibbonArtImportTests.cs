using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonArtImportTests
    {
        [TestCase("Profile")]
        [TestCase("AI")]
        [TestCase("Talk")]
        [TestCase("ScrollRoll")]
        public void RibbonArtworkLoadsAsA2DResource(string name)
        {
            Assert.That(Resources.Load<Texture2D>("RibbonArt/" + name), Is.Not.Null);
        }

        [Test]
        public void ScrollRollKeepsItsCutoutAlphaAndAspectRatio()
        {
            const string path = "Assets/_Project/Resources/RibbonArt/ScrollRoll.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = Resources.Load<Texture2D>("RibbonArt/ScrollRoll");

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(67));
            Assert.That(texture.height, Is.EqualTo(164));
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            Assert.That(importer.alphaIsTransparency, Is.True);
        }
    }
}
