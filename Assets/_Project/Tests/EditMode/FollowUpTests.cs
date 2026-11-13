using AICompanion.NewUI;
using AICompanion.NewUI.VisualNovel;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    // JUH-104 / JUH-108 follow-up: spoken-text glow timing, movable chat box, new settings keys.

    public sealed class SpokenGlowTests
    {
        static SpokenGlowTracker Make(string line)
        {
            var tracker = new SpokenGlowTracker { FadeSeconds = 0.3f, CommaPause = 2f, SentencePause = 4f, SecondsPerLetter = 0.1f };
            tracker.BeginLine(line);
            return tracker;
        }

        [Test]
        public void Words_AreWeightedByLettersWithPausesAfterPunctuation()
        {
            var t = Make("Hi, there friend.");
            Assert.AreEqual(3, t.WordCount);
            Assert.AreEqual(2f, t.WordWeight(0), "'Hi,' has two letters");
            Assert.AreEqual(2f, t.PauseWeight(0), "comma pause");
            Assert.AreEqual(0f, t.PauseWeight(1));
            Assert.AreEqual(6f, t.WordWeight(2));
            Assert.AreEqual(4f, t.PauseWeight(2), "full stop pause");
            Assert.AreEqual(4, t.WordStart(1));
            Assert.AreEqual(9, t.WordEnd(1));
        }

        [Test]
        public void ClipFraction_SweepsWordByWordByWeight()
        {
            // "aaaa bbbb": weights 4 + 4 = 8, no pauses.
            var t = Make("aaaa bbbb");
            t.Update(0f, true, true, 1, "aaaa bbbb", 0f, 1f);
            Assert.AreEqual(1f, t.Glow(0, 0f), "the first word lights at once");
            Assert.AreEqual(0f, t.Glow(1, 0f));
            t.Update(0.3f, true, true, 1, "aaaa bbbb", 0.3f, 1f);
            Assert.AreEqual(1f, t.Glow(0, 0.3f), "still being said at 30%");
            Assert.AreEqual(0f, t.Glow(1, 0.3f));
            t.Update(0.6f, true, true, 1, "aaaa bbbb", 0.6f, 1f);
            Assert.AreEqual(1f, t.Glow(1, 0.6f), "second word now");
            Assert.Less(t.Glow(0, 0.7f), 1f, "first word started fading");
        }

        [Test]
        public void AWord_FadesBackAboutPoint3SecondsAfterItWasSaid()
        {
            var t = Make("aaaa bbbb");
            t.Update(0f, true, true, 1, "aaaa bbbb", 0f, 1f);
            t.Update(1f, true, true, 1, "aaaa bbbb", 0.6f, 1f);
            Assert.AreEqual(1f, t.Glow(0, 1f), "ended just now");
            Assert.Greater(t.Glow(0, 1.15f), 0f, "half way through the fade");
            Assert.Less(t.Glow(0, 1.15f), 1f);
            Assert.AreEqual(0f, t.Glow(0, 1.31f), "back to normal after 0.3 s");
        }

        [Test]
        public void SecondClip_ContinuesWhereTheFirstEnded()
        {
            var t = Make("One two. Three four.");
            t.Update(0f, true, true, 7, "One two.", 0f, 1f);
            t.Update(0.9f, true, true, 7, "One two.", 0.99f, 1f);
            Assert.AreEqual(0f, t.Glow(2, 0.9f), "'Three' is in the next clip");
            t.Update(1f, true, true, 8, "Three four.", 0f, 1f);
            Assert.AreEqual(1f, t.Glow(2, 1f), "the next clip starts at 'Three'");
            Assert.Less(t.Glow(1, 1.2f), 1f, "the first clip's words fade");
        }

        [Test]
        public void WithoutClipTiming_AFixedPaceIsUsed()
        {
            var t = Make("aa bb");
            t.Update(0f, true, false, 0, null, 0f, 0f);
            Assert.AreEqual(1f, t.Glow(0, 0f));
            Assert.AreEqual(0f, t.Glow(1, 0f));
            t.Update(0.25f, true, false, 0, null, 0f, 0f);
            Assert.AreEqual(1f, t.Glow(1, 0.25f), "0.1 s per letter: 'bb' starts after 0.2 s");
        }

        [Test]
        public void StoppingSpeech_EndsTheSweepAndEverythingFades()
        {
            var t = Make("aaaa bbbb cccc");
            t.Update(0f, true, true, 1, "aaaa bbbb cccc", 0.4f, 1f);
            Assert.IsTrue(t.AnyGlowing(0f));
            t.Update(0.1f, false, false, 0, null, 0f, 0f);
            Assert.AreEqual(0f, t.Glow(2, 0.1f), "words not reached do not light");
            Assert.IsFalse(t.AnyGlowing(0.5f), "all back to normal after the fade");
        }

        [Test]
        public void Clear_TurnsTheGlowOffAtOnce()
        {
            var t = Make("aaaa");
            t.Update(0f, true, true, 1, "aaaa", 0.5f, 1f);
            t.Clear();
            Assert.AreEqual(0, t.WordCount);
            Assert.IsFalse(t.AnyGlowing(0f));
        }

        [Test]
        public void Strength_MapsTheSliderFromSubtleToStrong()
        {
            Assert.AreEqual(0.25f, SpokenGlowTracker.Strength(0f, 0.25f), 0.0001f);
            Assert.AreEqual(1f, SpokenGlowTracker.Strength(1f, 0.25f), 0.0001f);
            Assert.AreEqual(1f, SpokenGlowTracker.Strength(5f, 0.25f), 0.0001f);
        }

        [Test]
        public void LineStart_CountsEarlierLinesAndLineBreaks()
        {
            var script = VnScriptParser.Parse("*smiles* Hello there. *waves*", true);
            Assert.AreEqual(0, VnRender.LineStart(script.Segments, 0));
            Assert.AreEqual("smiles".Length + 1, VnRender.LineStart(script.Segments, 1));
            Assert.AreEqual("smiles".Length + 1 + "Hello there.".Length + 1, VnRender.LineStart(script.Segments, 2));
        }

        [Test]
        public void CountWords_DoesNotNeedAllocations()
        {
            Assert.AreEqual(0, SpokenGlowTracker.CountWords(null));
            Assert.AreEqual(3, SpokenGlowTracker.CountWords("  one  two\tthree "));
        }
    }

    public sealed class ChatBoxPlacementTests
    {
        static readonly Vector2 Screen = new Vector2(432f, 648f);
        static readonly Vector2 Box = new Vector2(416f, 260f);

        [Test]
        public void Default_IsBottomCentre()
        {
            var center = ChatBoxPlacement.Resolve(-1f, -1f, Screen, Box, new Rect(0f, 0f, Screen.x, Screen.y), 8f);
            Assert.AreEqual(216f, center.x, 0.01f);
            Assert.AreEqual(8f + 130f, center.y, 0.01f);
        }

        [Test]
        public void Saved_IsRestoredAsAFractionOfTheWindow()
        {
            var small = new Vector2(200f, 100f);
            var center = ChatBoxPlacement.Resolve(0.5f, 0.75f, Screen, small, new Rect(0f, 0f, Screen.x, Screen.y), 8f);
            Assert.AreEqual(new Vector2(216f, 486f), center);
            var saved = ChatBoxPlacement.ToSaved(center, Screen);
            Assert.AreEqual(0.5f, saved.x, 0.0001f);
            Assert.AreEqual(0.75f, saved.y, 0.0001f);
        }

        [Test]
        public void Dragging_IsClampedInsideTheVisibleArea()
        {
            var small = new Vector2(200f, 100f);
            var bounds = new Rect(0f, 0f, Screen.x, Screen.y);
            var center = ChatBoxPlacement.Clamp(new Vector2(-500f, 5000f), small, bounds, 8f);
            Assert.AreEqual(108f, center.x, 0.01f, "left edge + half width + margin");
            Assert.AreEqual(648f - 58f, center.y, 0.01f, "top edge - half height - margin");
        }

        [Test]
        public void OffScreenSavedSpot_SnapsBackOnScreen()
        {
            // The window sticks out to the right of its monitor (after a monitor change): only
            // its left 300 px are on the work area.
            var window = new RectInt(1700, 100, 432, 648);
            var work = new RectInt(0, 0, 2000, 1040);
            var visible = ChatBoxPlacement.VisibleArea(Screen, window, work);
            Assert.AreEqual(0f, visible.xMin, 0.01f);
            Assert.AreEqual(300f, visible.xMax, 0.01f);
            var small = new Vector2(200f, 100f);
            var center = ChatBoxPlacement.Resolve(0.95f, 0.5f, Screen, small, visible, 8f);
            Assert.LessOrEqual(center.x + 100f, 300f, "the box is fully on the monitor");
        }

        [Test]
        public void VisibleArea_FallsBackToTheWholeWindow()
        {
            var visible = ChatBoxPlacement.VisibleArea(Screen, new RectInt(5000, 5000, 432, 648), new RectInt(0, 0, 1920, 1040));
            Assert.AreEqual(new Rect(0f, 0f, Screen.x, Screen.y), visible);
        }

        [Test]
        public void ABoxBiggerThanTheArea_IsCentred()
        {
            var center = ChatBoxPlacement.Clamp(new Vector2(0f, 0f), new Vector2(1000f, 1000f), new Rect(0f, 0f, 400f, 300f), 8f);
            Assert.AreEqual(new Vector2(200f, 150f), center);
        }
    }

    public sealed class FollowUpSettingsTests
    {
        [Test]
        public void NewKeys_HaveSafeDefaults()
        {
            var s = CompanionSettings.CreateDefault();
            Assert.AreEqual(-1f, s.ChatBoxX);
            Assert.AreEqual(-1f, s.ChatBoxY);
            Assert.IsFalse(s.ShowDebugInfo, "the debug text is hidden by default");
            Assert.IsTrue(s.SpokenGlow, "the glow is on by default");
            Assert.AreEqual(0.6f, s.SpokenGlowIntensity, 0.0001f);
        }

        [Test]
        public void OldFile_LoadsWithDefaults()
        {
            var s = CompanionSettingsJson.Parse("{\n  \"version\": 1,\n  \"showMoodDebug\": true\n}\n", SettingsLegacyPrefs.None);
            Assert.IsTrue(s.ShowMoodDebug, "the mood readout setting is kept");
            Assert.IsFalse(s.ShowDebugInfo);
            Assert.AreEqual(-1f, s.ChatBoxX);
            Assert.IsTrue(s.SpokenGlow);
        }

        [Test]
        public void NewKeys_RoundTripAndAreCleaned()
        {
            var s = CompanionSettings.CreateDefault();
            s.ChatBoxX = 0.25f;
            s.ChatBoxY = 7f;
            s.ShowDebugInfo = true;
            s.SpokenGlow = false;
            s.SpokenGlowIntensity = 3f;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(s), SettingsLegacyPrefs.None);
            Assert.AreEqual(0.25f, back.ChatBoxX, 0.001f);
            Assert.AreEqual(1f, back.ChatBoxY, 0.001f, "clamped into the window");
            Assert.IsTrue(back.ShowDebugInfo);
            Assert.IsFalse(back.SpokenGlow);
            Assert.AreEqual(1f, back.SpokenGlowIntensity, 0.001f);
        }
    }
}
