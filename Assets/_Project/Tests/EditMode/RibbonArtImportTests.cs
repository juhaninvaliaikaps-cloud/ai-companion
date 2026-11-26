using AICompanion.NewUI;
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
        [TestCase("ChatPortrait")]
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

        [TestCase("RibbonGlyph")]
        [TestCase("RibbonChatFrameEdge")]
        public void CodeBuiltGraphicsCreateTheirCanvasRenderer(string name)
        {
            var graphicType = typeof(HappiChatView).Assembly.GetType("AICompanion.NewUI." + name, throwOnError: true);
            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                root.AddComponent(graphicType);
                Assert.That(root.GetComponent<CanvasRenderer>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
