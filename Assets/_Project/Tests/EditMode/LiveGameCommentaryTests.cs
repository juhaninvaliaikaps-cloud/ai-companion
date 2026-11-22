using AICompanion.Core;
using AICompanion.GameCompanion;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-160: the game note and the live prompt stay separate from her persona.</summary>
    public sealed class LiveGameCommentaryTests
    {
        [Test]
        public void GameContextAddsGameObservationAndPatch()
        {
            GameContextNow.Clear();
            GameContextNow.SetGame("Arknights: Endfield");
            GameContextNow.SetObservation("The player reached a new area.");
            GameContextNow.SetPatchContext("Patch 1.2 changed the stamina cap.");

            var prompt = GameContextNow.Append("You are Hermione.");

            StringAssert.Contains("Arknights: Endfield", prompt);
            StringAssert.Contains("reached a new area", prompt);
            StringAssert.Contains("stamina cap", prompt);
            GameContextNow.Clear();
            Assert.IsFalse(GameContextNow.Active);
        }

        [Test]
        public void LivePromptRecognisesSilence()
        {
            Assert.IsTrue(LiveGamePrompt.IsSilent("SILENT"));
            Assert.IsTrue(LiveGamePrompt.IsSilent(" silent "));
            Assert.IsFalse(LiveGamePrompt.IsSilent("That was a clean landing."));
        }
    }
}
