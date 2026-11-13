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

        [Test]
        public void ChatBoxPlacementKeys_HaveDefaultsAndRoundTrip()
        {
            var s = CompanionSettings.CreateDefault();
            Assert.AreEqual(-1, s.ChatBoxSnap, "default spot");
            s.ChatBoxSnap = 2;
            s.ChatBoxOffsetX = -120.5f;
            s.ChatBoxOffsetY = 40f;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(s), SettingsLegacyPrefs.None);
            Assert.AreEqual(2, back.ChatBoxSnap);
            Assert.AreEqual(-120.5f, back.ChatBoxOffsetX, 0.01f);
            Assert.AreEqual(40f, back.ChatBoxOffsetY, 0.01f);
            s.ChatBoxSnap = 9;
            Assert.AreEqual(-1, CompanionSettingsJson.Parse(CompanionSettingsJson.Write(s), SettingsLegacyPrefs.None).ChatBoxSnap, "an unknown spot is the default");
            s.ChatBoxSnap = 5;
            Assert.AreEqual(5, CompanionSettingsJson.Parse(CompanionSettingsJson.Write(s), SettingsLegacyPrefs.None).ChatBoxSnap, "spot 5 (under her) is kept");
        }

        [Test]
        public void PortraitPlacementKeys_HaveDefaultsAndRoundTrip()
        {
            var s = CompanionSettings.CreateDefault();
            Assert.AreEqual(0, s.PortraitDock, "automatic by default");
            s.PortraitDock = 2;
            s.PortraitOffsetX = -150f;
            s.PortraitOffsetY = 33.5f;
            var back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(s), SettingsLegacyPrefs.None);
            Assert.AreEqual(2, back.PortraitDock);
            Assert.AreEqual(-150f, back.PortraitOffsetX, 0.01f);
            Assert.AreEqual(33.5f, back.PortraitOffsetY, 0.01f);
            s.PortraitDock = 7;
            s.PortraitOffsetX = float.NaN;
            back = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(s), SettingsLegacyPrefs.None);
            Assert.AreEqual(0, back.PortraitDock, "an unknown dock is automatic");
            Assert.AreEqual(0f, back.PortraitOffsetX, 0.01f);
        }
    }

    public sealed class ChatSnapSpotTests
    {
        // Her head at x 600, 500 px above her feet at y 20; a 340 x 280 box; 12 px gap.
        static HerBody Body => new HerBody { Head = new Vector2(600f, 520f), FeetY = 20f };
        static readonly Vector2 Box = new Vector2(340f, 280f);
        const float Gap = 12f;

        [Test]
        public void Spots1And2_AreBesideHerHead()
        {
            var left = ChatSnapSpots.Spot(ChatSnapSpots.LeftOfHead, Body, Box, Gap);
            var right = ChatSnapSpots.Spot(ChatSnapSpots.RightOfHead, Body, Box, Gap);
            Assert.AreEqual(520f, left.y, 0.01f, "level with her head");
            Assert.AreEqual(520f, right.y, 0.01f);
            Assert.Less(left.x + Box.x * 0.5f, 600f, "the whole box is left of her head centre");
            Assert.Greater(right.x - Box.x * 0.5f, 600f, "the whole box is right of her head centre");
            Assert.AreEqual(600f - left.x, right.x - 600f, 0.01f, "mirror images");
        }

        [Test]
        public void Spots3And4_AreBesideHerBodyAtNavelHeight()
        {
            var body = Body;
            var left = ChatSnapSpots.Spot(ChatSnapSpots.LeftBeside, body, Box, Gap);
            var right = ChatSnapSpots.Spot(ChatSnapSpots.RightBeside, body, Box, Gap);
            Assert.AreEqual(body.NavelY, left.y, 0.01f);
            Assert.AreEqual(body.NavelY, right.y, 0.01f);
            Assert.LessOrEqual(left.x + Box.x * 0.5f, 600f - body.BodyHalfWidth, "clear of her body");
            Assert.GreaterOrEqual(right.x - Box.x * 0.5f, 600f + body.BodyHalfWidth);
        }

        [Test]
        public void Spots3And4_BottomNeverGoesBelowHerFeet()
        {
            var tall = new Vector2(340f, 700f);
            var spot = ChatSnapSpots.Spot(ChatSnapSpots.RightBeside, Body, tall, Gap);
            Assert.AreEqual(20f, spot.y - tall.y * 0.5f, 0.01f, "the box bottom is level with her feet");
        }

        [Test]
        public void Nearest_SnapsOnlyWhenClose()
        {
            var spot3 = ChatSnapSpots.Spot(ChatSnapSpots.LeftBeside, Body, Box, Gap);
            Assert.AreEqual(3, ChatSnapSpots.Nearest(spot3 + new Vector2(30f, -20f), Body, Box, Gap, 90f, false));
            Assert.AreEqual(0, ChatSnapSpots.Nearest(spot3 + new Vector2(200f, 300f), Body, Box, Gap, 90f, false), "far from every spot: free");
        }

        [Test]
        public void Resolve_FreeDefaultAndSnapped()
        {
            var body = Body;
            Assert.AreEqual(ChatSnapSpots.Spot(4, body, Box, Gap), ChatSnapSpots.Resolve(-1, Vector2.zero, 1f, body, Box, Gap), "default is spot 4");
            Assert.AreEqual(ChatSnapSpots.Spot(2, body, Box, Gap), ChatSnapSpots.Resolve(2, new Vector2(999f, 999f), 1f, body, Box, Gap));
            var free = ChatSnapSpots.Resolve(0, new Vector2(-300f, 50f), 2f, body, Box, Gap);
            Assert.AreEqual(new Vector2(0f, 620f), free, "offset from her head, at 200% scaling");
            Assert.AreEqual(new Vector2(-300f, 50f), ChatSnapSpots.OffsetFor(free, body, 2f));
        }

        [Test]
        public void FreePlacement_FollowsHerWhenSheMoves()
        {
            var body = Body;
            var before = ChatSnapSpots.Resolve(0, new Vector2(250f, -100f), 1f, body, Box, Gap);
            body.Head += new Vector2(-180f, 0f);
            var after = ChatSnapSpots.Resolve(0, new Vector2(250f, -100f), 1f, body, Box, Gap);
            Assert.AreEqual(before.x - 180f, after.x, 0.01f);
        }

        [Test]
        public void Spot2_SendsThePortraitLeft()
        {
            Assert.IsTrue(ChatSnapSpots.PortraitGoesLeft(ChatSnapSpots.RightOfHead));
            Assert.IsFalse(ChatSnapSpots.PortraitGoesLeft(ChatSnapSpots.LeftOfHead));
            Assert.IsFalse(ChatSnapSpots.PortraitGoesLeft(ChatSnapSpots.Free));
        }

        [Test]
        public void ASpotWithoutRoom_FlipsToHerOtherSide()
        {
            // She stands near the right screen edge: the visible part of the window ends 100 px right of her.
            var visible = new Rect(0f, 0f, 700f, 720f);
            Assert.AreEqual(3, ChatSnapSpots.Fit(4, Body, Box, Gap, visible, 8f), "spot 4 has no room: use 3");
            Assert.AreEqual(3, ChatSnapSpots.Fit(-1, Body, Box, Gap, visible, 8f), "the default flips too");
            Assert.AreEqual(1, ChatSnapSpots.Fit(2, Body, Box, Gap, visible, 8f));
            var wide = new Rect(0f, 0f, 1400f, 720f);
            Assert.AreEqual(4, ChatSnapSpots.Fit(4, Body, Box, Gap, wide, 8f), "room on the right: keep 4");
            Assert.AreEqual(0, ChatSnapSpots.Fit(0, Body, Box, Gap, visible, 8f), "free placement is only clamped");
        }

        [Test]
        public void Spot5_IsCentredUnderHerFeet()
        {
            var spot = ChatSnapSpots.Spot(ChatSnapSpots.Under, Body, Box, Gap);
            Assert.AreEqual(600f, spot.x, 0.01f, "centred under her");
            Assert.AreEqual(20f - Gap, spot.y + Box.y * 0.5f, 0.01f, "the box top is one gap below her feet");
        }

        [Test]
        public void Spot5_IsTheDefaultOnlyWhereItIsPossible()
        {
            Assert.AreEqual(ChatSnapSpots.Under, ChatSnapSpots.Effective(ChatSnapSpots.Default, true), "2D video: under her");
            Assert.AreEqual(ChatSnapSpots.RightBeside, ChatSnapSpots.Effective(ChatSnapSpots.Default, false), "3D: beside her on the right");
            Assert.AreEqual(ChatSnapSpots.RightBeside, ChatSnapSpots.Effective(ChatSnapSpots.Under, false), "a saved spot 5 falls back in 3D");
            Assert.AreEqual(ChatSnapSpots.Spot(5, Body, Box, Gap), ChatSnapSpots.Resolve(-1, Vector2.zero, 1f, Body, Box, Gap, true));
            var visible = new Rect(0f, 0f, 760f, 900f);
            Assert.AreEqual(5, ChatSnapSpots.Fit(5, Body, Box, Gap, visible, 8f, true), "spot 5 is never mirrored");
            Assert.AreEqual(5, ChatSnapSpots.Mirror(5));
        }

        [Test]
        public void Nearest_FindsSpot5OnlyWhenAllowed()
        {
            var under = ChatSnapSpots.Spot(ChatSnapSpots.Under, Body, Box, Gap) + new Vector2(20f, 10f);
            Assert.AreEqual(5, ChatSnapSpots.Nearest(under, Body, Box, Gap, 90f, true));
            Assert.AreNotEqual(5, ChatSnapSpots.Nearest(under, Body, Box, Gap, 90f, false));
        }

        [Test]
        public void Spot5_CanBeReachedWhileSheStandsOnTheWindowBottom()
        {
            // No room under her yet: her feet are at the window bottom, so spot 5 lies below the
            // window. The box can only be dragged down to the bottom edge, where the marker is shown.
            var body = new HerBody { Head = new Vector2(600f, 520f), FeetY = 0f };
            var visible = new Rect(0f, 0f, 1400f, 800f);
            var shown = ChatBoxPlacement.Clamp(ChatSnapSpots.Spot(ChatSnapSpots.Under, body, Box, Gap), Box, visible, 8f);
            var dropped = shown + new Vector2(30f, 5f);
            Assert.AreEqual(ChatSnapSpots.Under, ChatSnapSpots.Nearest(dropped, body, Box, Gap, 90f, true, visible, 8f), "dropped on the shown marker: snaps under her");
            Assert.AreEqual(ChatSnapSpots.Free, ChatSnapSpots.Nearest(dropped, body, Box, Gap, 90f, true), "measured to the unclamped spot it was out of reach");
            Assert.AreNotEqual(ChatSnapSpots.Under, ChatSnapSpots.Nearest(dropped, body, Box, Gap, 90f, false, visible, 8f), "3D: never spot 5");
        }

        [Test]
        public void BottomRoom_FitsTheBoxAndItsGaps()
        {
            Assert.AreEqual(304, ChatSnapSpots.BottomRoomFor(280f, 12f));
        }

        [Test]
        public void DockedPortrait_LinesUpWithTheBoxTopAndBottom()
        {
            // Box centre (500, 300) bottom-left based, 340 x 280, in a 1000 px tall window.
            var center = new Vector2(500f, 300f);
            var left = PortraitDocking.DockRect(center, Box, PortraitDocking.LeftOfBox, Gap, 1000f);
            var right = PortraitDocking.DockRect(center, Box, PortraitDocking.RightOfBox, Gap, 1000f);
            var boxTop = 1000f - (center.y + Box.y * 0.5f);
            foreach (var rect in new[] { left, right })
            {
                Assert.AreEqual(boxTop, rect.yMin, 0.01f, "tops line up");
                Assert.AreEqual(boxTop + Box.y, rect.yMax, 0.01f, "bottoms line up");
                Assert.AreEqual(Box.y * PortraitDocking.Aspect, rect.width, 0.01f, "keeps the portrait's shape");
            }

            Assert.AreEqual(center.x - Box.x * 0.5f - Gap, left.xMax, 0.01f, "left of the box, one gap away");
            Assert.AreEqual(center.x + Box.x * 0.5f + Gap, right.xMin, 0.01f, "right of the box, one gap away");
        }

        [Test]
        public void DockedPortrait_FlipsSideWithoutRoom()
        {
            var left = new Rect(-40f, 100f, 249f, 280f);
            var right = new Rect(682f, 100f, 249f, 280f);
            Assert.AreEqual(PortraitDocking.RightOfBox, PortraitDocking.FitSide(PortraitDocking.LeftOfBox, left, right, 0f, 1200f));
            Assert.AreEqual(PortraitDocking.RightOfBox, PortraitDocking.FitSide(PortraitDocking.RightOfBox, left, right, 0f, 1200f));
            Assert.AreEqual(PortraitDocking.LeftOfBox, PortraitDocking.FitSide(PortraitDocking.LeftOfBox, new Rect(10f, 0f, 100f, 100f), right, 0f, 1200f));
        }

        [Test]
        public void DraggedPortrait_DocksOnlyWhenClose()
        {
            var left = new Rect(100f, 100f, 249f, 280f);
            var right = new Rect(800f, 100f, 249f, 280f);
            var nearLeft = new Rect(left.center.x - 64f + 30f, left.center.y - 72f, 128f, 144f);
            Assert.AreEqual(PortraitDocking.LeftOfBox, PortraitDocking.Nearest(nearLeft, left, right, 90f));
            var nearRight = new Rect(right.center.x - 64f - 20f, right.center.y - 72f + 40f, 128f, 144f);
            Assert.AreEqual(PortraitDocking.RightOfBox, PortraitDocking.Nearest(nearRight, left, right, 90f));
            Assert.AreEqual(PortraitDocking.Auto, PortraitDocking.Nearest(new Rect(450f, 600f, 128f, 144f), left, right, 90f), "far from both: free");
        }

        [Test]
        public void PortraitClamp_KeepsItOnScreen()
        {
            var area = new Rect(0f, 0f, 1000f, 800f);
            var clamped = PortraitDocking.Clamp(new Rect(950f, -30f, 128f, 144f), area);
            Assert.AreEqual(872f, clamped.x, 0.01f);
            Assert.AreEqual(0f, clamped.y, 0.01f);
            Assert.AreEqual(128f, clamped.width, 0.01f, "size kept");
        }

        [Test]
        public void SideRoom_FitsTheBoxAndItsGaps()
        {
            Assert.AreEqual(364, ChatSnapSpots.SideRoomFor(340f, 12f));
        }

        [Test]
        public void SpokenGlow_IsClearlyVisibleAtTheDefaultSetting()
        {
            var theme = new HappiTheme();
            var strength = SpokenGlowTracker.Strength(0.6f, theme.SpokenGlowMinStrength);
            Assert.GreaterOrEqual(strength, 0.75f, "the default slider position glows strongly");
            Assert.Greater(theme.SpokenGlowPop, 0.05f, "lit words also grow");
            Assert.Greater(HappiTheme.Contrast(theme.SpokenGlowColor, theme.Text), 1.15f, "the glow colour differs clearly from the text colour");
        }
    }
}
