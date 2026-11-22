using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-184: an innocence reading logs its number and band, never its reason.</summary>
    public sealed class InnocenceLogTests
    {
        [Test]
        public void TheLine_HasTheNumberAndBand_Only()
        {
            var line = InnocenceLog.ReadLine("hermione", 30f, false);
            Assert.AreEqual("Innocence: hermione read as 30 (" + InnocenceMath.BandName(30f) + ")", line);
            StringAssert.DoesNotContain(":  ", line);
        }

        [Test]
        public void AUserSetting_IsNoted()
        {
            StringAssert.EndsWith(", your setting is kept", InnocenceLog.ReadLine("x", 70f, true));
        }

        [Test]
        public void TheKeeper_NoLongerLogsTheReason()
        {
            var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/Scripts/Settings/InnocenceKeeper.cs");
            var source = System.IO.File.ReadAllText(path);
            StringAssert.Contains("InnocenceLog.ReadLine(", source);
            StringAssert.DoesNotContain("+ _state.ReadReason)", source);
        }
    }
}
