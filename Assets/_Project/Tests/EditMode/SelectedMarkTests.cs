using System.IO;
using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-187: the selected mark is one Segoe UI has (the check mark showed as an empty box).</summary>
    public sealed class SelectedMarkTests
    {
        [Test]
        public void TheMark_IsABullet()
        {
            Assert.AreEqual("• ", HappiTheme.SelectedMark);
        }

        [Test]
        public void NoNewUiFile_UsesTheCheckMark()
        {
            var folder = Path.Combine(Application.dataPath, "_Project/Scripts/UI/NewUI");
            foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                var withoutComments = System.Text.RegularExpressions.Regex.Replace(text, @"//.*", string.Empty);
                StringAssert.DoesNotContain("✓", withoutComments, Path.GetFileName(file));
            }
        }
    }
}
