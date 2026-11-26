using System.IO;
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
        [TestCase("Settings")]
        [TestCase("Happi-Playful")]
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

        [Test]
        public void PaperJoinStaysUnderTheIllustratedCurl()
        {
            const string path = "Assets/_Project/Resources/RibbonArt/ScrollRoll.png";
            var curl = new Texture2D(2, 2);
            try
            {
                Assert.That(curl.LoadImage(File.ReadAllBytes(path)), Is.True);
                var (overlap, paperEdge) = RibbonMenuPlacement.ScrollJoin(curl.width);
                for (var i = 0; i <= 8; i++)
                {
                    var t = i / 8f;
                    var x = Mathf.RoundToInt(overlap + Mathf.Lerp(paperEdge.x, paperEdge.y, t));
                    var y = Mathf.RoundToInt(Mathf.Lerp(0.955f, 0.045f, t) * (curl.height - 1));
                    Assert.That(curl.GetPixel(x, y).a, Is.GreaterThan(0.9f), $"Paper join at PNG pixel {x},{y}");
                }
            }
            finally
            {
                Object.DestroyImmediate(curl);
            }
        }

        [TestCase("RibbonGlyph")]
        [TestCase("RibbonChatFrameEdge")]
        [TestCase("RibbonScrollFinish")]
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
