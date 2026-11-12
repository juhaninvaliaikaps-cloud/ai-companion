using System.Collections.Generic;
using System.IO;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-99. Which clip plays when: the manifest and the file-name rules, the director's
    /// transitions and interruptions, the speed that follows a walking clip, and the frame
    /// arithmetic that keeps a stale picture off the screen. Decoding, transparency, and
    /// movement on a real desktop need a Windows build and are checked by hand.
    /// </summary>
    public class VideoAvatarDirectorTests
    {
        static readonly string Folder = Path.Combine("D:", "clips");

        static string In(string fileName)
        {
            return Path.Combine(Folder, fileName);
        }

        static string[] Files(params string[] names)
        {
            var paths = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
                paths[i] = In(names[i]);
            return paths;
        }

        /// <summary>The file names DeepSeek delivered on 2026-11-11, including the files that are not clips.</summary>
        static string[] DeliveredFiles()
        {
            return Files(
                "idle.mp4", "idle.mask.mp4", "idle.sbs.mp4", "idle.key.json",
                "idle_breeze.mp4", "idle_breeze.mask.mp4", "idle_breeze.sbs.mp4", "idle_breeze.key.json",
                "walk.mp4", "walk.mask.mp4", "walk.sbs.mp4", "walk.key.json",
                "walk_stop.mp4", "walk_stop.mask.mp4", "walk_stop.sbs.mp4", "walk_stop.key.json",
                "REPORT.md", "validation.json", "key_greenscreen.py");
        }

        static VideoAvatarClipSet Set(int variations = 2, bool walk = true, bool stop = true, bool side = false, bool thenIdle = false)
        {
            var names = new List<string> { "idle.mp4", "idle.mask.mp4" };
            for (var i = 0; i < variations; i++)
            {
                names.Add("idle_" + (i + 1) + ".mp4");
                names.Add("idle_" + (i + 1) + ".mask.mp4");
            }

            if (walk)
            {
                names.Add("walk.mp4");
                names.Add("walk.mask.mp4");
            }

            if (stop)
            {
                names.Add("walk_stop.mp4");
                names.Add("walk_stop.mask.mp4");
            }

            if (side)
            {
                names.Add("idle_side.mp4");
                names.Add("idle_side.mask.mp4");
            }

            var set = VideoAvatarManifest.Resolve(null, Files(names.ToArray()));
            if (thenIdle && set.WalkStop >= 0)
                set.Clips[set.WalkStop].ThenIdle = true;
            // Queue a variation after one second, so tests do not depend on the random delay.
            set.VariationMinSeconds = 1f;
            set.VariationMaxSeconds = 1f;
            return set;
        }

        static void WalkLeft(VideoAvatarDirector director)
        {
            director.SetMovement(true, false, false, false);
        }

        static void Stand(VideoAvatarDirector director)
        {
            director.SetMovement(false, false, false, false);
        }

        // ---- finding clips by file name -------------------------------------------------

        [Test]
        public void FileNames_TheDeliveredSet_GivesIdleOneVariationWalkAndWalkStop()
        {
            var set = VideoAvatarManifest.Resolve(null, DeliveredFiles());

            Assert.IsTrue(set.Found);
            Assert.IsFalse(set.FromManifest);
            Assert.AreEqual(4, set.Clips.Count);
            Assert.AreEqual(0, set.Warnings.Count);

            var idle = set.Clips[set.Idle];
            Assert.AreEqual("idle", idle.Name);
            Assert.IsTrue(idle.Loop);

            Assert.AreEqual(1, set.Variations.Count);
            var breeze = set.Clips[set.Variations[0]];
            Assert.AreEqual("idle_breeze", breeze.Name);
            Assert.IsFalse(breeze.Loop);

            var walk = set.Clips[set.Walk];
            Assert.AreEqual("walk", walk.Name);
            Assert.IsTrue(walk.Loop);
            Assert.IsFalse(walk.FacesRight);

            var stop = set.Clips[set.WalkStop];
            Assert.AreEqual("walk_stop", stop.Name);
            Assert.IsFalse(stop.Loop);
            Assert.IsFalse(stop.ThenIdle);
            Assert.AreEqual(-1, set.SideIdle);
        }

        [Test]
        public void FileNames_WithBothForms_ThePackedClipWins_SoTheOutlineCannotSlip()
        {
            // The delivered folder has name.mp4 + name.mask.mp4 and name.sbs.mp4 for every clip.
            var set = VideoAvatarManifest.Resolve(null, DeliveredFiles());

            for (var i = 0; i < set.Clips.Count; i++)
            {
                var clip = set.Clips[i];
                Assert.AreEqual(VideoMaskKind.Packed, clip.Mask, clip.Name);
                Assert.AreEqual(In(clip.Name + ".sbs.mp4"), clip.ColorPath, clip.Name);
                Assert.AreEqual(string.Empty, clip.MaskPath, clip.Name);
            }
        }

        [Test]
        public void FileNames_WithoutAPackedClip_ThePairIsUsed()
        {
            var set = VideoAvatarManifest.Resolve(null, Files("idle.mp4", "idle.mask.mp4", "walk.sbs.mp4", "walk.mp4", "walk.mask.mp4"));

            Assert.AreEqual(VideoMaskKind.MaskVideo, set.Clips[set.Idle].Mask);
            Assert.AreEqual(In("idle.mp4"), set.Clips[set.Idle].ColorPath);
            Assert.AreEqual(In("idle.mask.mp4"), set.Clips[set.Idle].MaskPath);
            Assert.AreEqual(VideoMaskKind.Packed, set.Clips[set.Walk].Mask);
            Assert.AreEqual(In("walk.sbs.mp4"), set.Clips[set.Walk].ColorPath);
            Assert.AreEqual(2, set.Clips.Count);
        }

        [Test]
        public void Manifest_CanStillNameThePair_WhenAPackedClipIsInTheFolder()
        {
            var set = VideoAvatarManifest.Resolve(Manifest, DeliveredFiles());

            Assert.AreEqual(VideoMaskKind.MaskVideo, set.Clips[set.Idle].Mask);
            Assert.AreEqual(In("idle.mask.mp4"), set.Clips[set.Idle].MaskPath);
            Assert.AreEqual(VideoMaskKind.MaskVideo, set.Clips[set.Walk].Mask);
        }

        [Test]
        public void Manifest_APackedFileAsColor_NeedsNoMask()
        {
            const string json = @"{ ""clips"": [
                { ""name"": ""idle"", ""color"": ""idle.sbs.mp4"" },
                { ""name"": ""walk"", ""color"": ""walk.sbs.mp4"", ""speed"": 0.27 } ] }";

            var set = VideoAvatarManifest.Resolve(json, DeliveredFiles());

            Assert.AreEqual(VideoMaskKind.Packed, set.Clips[set.Idle].Mask);
            Assert.AreEqual(In("idle.sbs.mp4"), set.Clips[set.Idle].ColorPath);
            Assert.AreEqual(VideoMaskKind.Packed, set.Clips[set.Walk].Mask);
            Assert.AreEqual(0, set.Warnings.Count);
        }

        [Test]
        public void FileNames_OnlyTheDefaultIdle_StillPlays()
        {
            var set = VideoAvatarManifest.Resolve(null, Files("idle.mp4", "idle.mask.mp4"));

            Assert.IsTrue(set.Found);
            Assert.AreEqual(0, set.Variations.Count);
            Assert.AreEqual(-1, set.Walk);
            Assert.AreEqual(-1, set.WalkStop);
        }

        [Test]
        public void FileNames_PackedClipIsUsedWhenThereIsNoPair()
        {
            var set = VideoAvatarManifest.Resolve(null, Files("idle.sbs.mp4", "walk.sbs.mp4"));

            Assert.AreEqual(VideoMaskKind.Packed, set.Clips[set.Idle].Mask);
            Assert.AreEqual(In("idle.sbs.mp4"), set.Clips[set.Idle].ColorPath);
            Assert.AreEqual(VideoMaskKind.Packed, set.Clips[set.Walk].Mask);
        }

        [Test]
        public void FileNames_MaskFilesAreNeverClips_WhateverTheirSuffix()
        {
            var set = VideoAvatarManifest.Resolve(null, Files("idle.mp4", "idle_mask.mp4", "walk.mp4", "walk-mask.mp4"));

            Assert.AreEqual(2, set.Clips.Count);
            Assert.AreEqual(In("idle_mask.mp4"), set.Clips[set.Idle].MaskPath);
            Assert.AreEqual(In("walk-mask.mp4"), set.Clips[set.Walk].MaskPath);
            Assert.AreEqual(0, set.Variations.Count);
        }

        [Test]
        public void FileNames_RolesComeFromTheName()
        {
            var set = VideoAvatarManifest.Resolve(null, Files(
                "idle.mp4", "idle-2.mp4", "idle_look.mp4", "idle3.mp4", "idle-side.mp4",
                "Walk-Left.mp4", "walk_stop_left.mp4", "happy.mp4", "notes.txt"));

            Assert.AreEqual(3, set.Variations.Count);
            Assert.AreEqual("idle-side", set.Clips[set.SideIdle].Name);
            Assert.AreEqual("Walk-Left", set.Clips[set.Walk].Name);
            Assert.AreEqual("walk_stop_left", set.Clips[set.WalkStop].Name);
            Assert.GreaterOrEqual(set.FindReaction("HAPPY"), 0);
            Assert.AreEqual(VideoClipRole.Reaction, set.Clips[set.FindReaction("happy")].Role);
            Assert.AreEqual(-1, set.FindReaction("annoyed"));
        }

        [Test]
        public void FileNames_VariationsWithoutADefault_PromoteTheFirstOne()
        {
            var set = VideoAvatarManifest.Resolve(null, Files("idle-1.mp4", "idle-2.mp4", "idle-3.mp4"));

            Assert.IsTrue(set.Found);
            Assert.AreEqual("idle-1", set.Clips[set.Idle].Name);
            Assert.IsTrue(set.Clips[set.Idle].Loop);
            Assert.AreEqual(2, set.Variations.Count);
            Assert.AreEqual(1, set.Warnings.Count);
        }

        [Test]
        public void FileNames_NoIdleAtAll_IsNotFound()
        {
            Assert.IsFalse(VideoAvatarManifest.Resolve(null, Files("walk.mp4", "walk_stop.mp4")).Found);
            Assert.IsFalse(VideoAvatarManifest.Resolve(null, null).Found);
            Assert.IsFalse(VideoAvatarManifest.Resolve("", new string[0]).Found);
        }

        // ---- video-avatar.json ----------------------------------------------------------

        const string Manifest = @"{
  ""variationMinSeconds"": 20,
  ""variationMaxSeconds"": 40,
  ""clips"": [
    { ""name"": ""idle"", ""role"": ""idle"", ""color"": ""idle.mp4"", ""mask"": ""idle.mask.mp4"", ""offsetY"": -0.023 },
    { ""name"": ""breeze"", ""role"": ""idleVariation"", ""color"": ""idle_breeze.mp4"", ""mask"": ""idle_breeze.mask.mp4"", ""weight"": 2 },
    { ""name"": ""walk"", ""role"": ""walk"", ""color"": ""walk.mp4"", ""mask"": ""walk.mask.mp4"", ""facing"": ""left"",
      ""speed"": 0.27, ""moveStart"": 0.15, ""moveEnd"": 4.95, ""moveRamp"": 0.3 },
    { ""name"": ""walk-stop"", ""role"": ""walkStop"", ""color"": ""walk_stop.mp4"", ""mask"": ""walk_stop.mask.mp4"",
      ""playback"": ""once"", ""moveEnd"": 3.8, ""after"": ""idle"" },
    { ""name"": ""happy"", ""role"": ""reaction"", ""color"": ""happy.mp4"", ""mask"": ""happy.mask.mp4"" }
  ]
}";

        [Test]
        public void Manifest_RegistersEveryClipWithItsRoleFilesAndPlayback()
        {
            var set = VideoAvatarManifest.Resolve(Manifest, DeliveredFiles());

            Assert.IsTrue(set.FromManifest);
            Assert.AreEqual(20f, set.VariationMinSeconds, 0.001f);
            Assert.AreEqual(40f, set.VariationMaxSeconds, 0.001f);

            var idle = set.Clips[set.Idle];
            Assert.AreEqual(In("idle.mp4"), idle.ColorPath);
            Assert.AreEqual(VideoMaskKind.MaskVideo, idle.Mask);
            Assert.IsTrue(idle.Loop);
            Assert.AreEqual(-0.023f, idle.OffsetY, 0.0001f);
            Assert.AreEqual(1f, idle.Scale, 0.0001f);

            Assert.AreEqual(1, set.Variations.Count);
            var breeze = set.Clips[set.Variations[0]];
            Assert.AreEqual("breeze", breeze.Name);
            Assert.AreEqual(In("idle_breeze.mp4"), breeze.ColorPath);
            Assert.IsFalse(breeze.Loop);
            Assert.AreEqual(2f, breeze.Weight, 0.0001f);

            var walk = set.Clips[set.Walk];
            Assert.IsTrue(walk.Loop);
            Assert.IsFalse(walk.FacesRight);
            Assert.AreEqual(0.27f, walk.Speed, 0.0001f);
            Assert.AreEqual(0.15f, walk.MoveStart, 0.0001f);
            Assert.AreEqual(4.95f, walk.MoveEnd, 0.0001f);
            Assert.AreEqual(0.3f, walk.MoveRamp, 0.0001f);

            var stop = set.Clips[set.WalkStop];
            Assert.IsFalse(stop.Loop);
            Assert.IsTrue(stop.ThenIdle);
            Assert.AreEqual(3.8f, stop.MoveEnd, 0.0001f);
            Assert.AreEqual(VideoAvatarManifest.DefaultMoveRamp, stop.MoveRamp, 0.0001f);
        }

        [Test]
        public void Manifest_AMissingClip_IsLeftOutWithAWarningAndTheRestStillLoads()
        {
            // happy.mp4 is listed in the manifest and is not in the folder.
            var set = VideoAvatarManifest.Resolve(Manifest, DeliveredFiles());

            Assert.IsTrue(set.Found);
            Assert.AreEqual(4, set.Clips.Count);
            Assert.AreEqual(-1, set.FindReaction("happy"));
            Assert.AreEqual(1, set.Warnings.Count);
            StringAssert.Contains("happy", set.Warnings[0]);
        }

        [Test]
        public void Manifest_AMissingMask_LeavesTheClipOut_SoItsBackgroundIsNeverShown()
        {
            var set = VideoAvatarManifest.Resolve(Manifest, Files(
                "idle.mp4", "idle.mask.mp4", "idle_breeze.mp4", "walk.mp4", "walk.mask.mp4", "walk_stop.mp4", "walk_stop.mask.mp4"));

            Assert.IsTrue(set.Found);
            Assert.AreEqual(0, set.Variations.Count);
            Assert.GreaterOrEqual(set.Walk, 0);
            StringAssert.Contains("idle_breeze.mask.mp4", string.Join(" ", set.Warnings.ToArray()));
        }

        [Test]
        public void Manifest_WithoutTheDefaultIdleFile_IsNotFound()
        {
            var set = VideoAvatarManifest.Resolve(Manifest, Files("walk.mp4", "walk.mask.mp4"));

            Assert.IsFalse(set.Found);
            Assert.GreaterOrEqual(set.Walk, 0);
        }

        [Test]
        public void Manifest_ThatCannotBeRead_FallsBackToFileNames()
        {
            var set = VideoAvatarManifest.Resolve("{ this is not json", DeliveredFiles());

            Assert.IsTrue(set.Found);
            Assert.IsFalse(set.FromManifest);
            Assert.AreEqual(4, set.Clips.Count);
            StringAssert.Contains(VideoAvatarManifest.FileName, set.Warnings[0]);
        }

        [Test]
        public void Manifest_FileNamesNeverLeaveTheFolder()
        {
            const string json = @"{ ""clips"": [ { ""name"": ""idle"", ""color"": ""..\\elsewhere\\idle.mp4"", ""mask"": ""C:/other/idle.mask.mp4"" } ] }";

            var set = VideoAvatarManifest.Resolve(json, Files("idle.mp4", "idle.mask.mp4"));

            Assert.AreEqual(In("idle.mp4"), set.Clips[set.Idle].ColorPath);
            Assert.AreEqual(In("idle.mask.mp4"), set.Clips[set.Idle].MaskPath);
        }

        [Test]
        public void Manifest_DefaultsAndClamps()
        {
            const string json = @"{ ""clips"": [
                { ""color"": ""idle.mp4"", ""scale"": 99, ""offsetX"": -7, ""playback"": ""once"" },
                { ""color"": ""walk.mp4"", ""facing"": ""right"", ""speed"": 50 },
                { ""color"": ""walk.mp4"", ""name"": ""second walk"", ""role"": ""walk"" } ] }";

            var set = VideoAvatarManifest.Resolve(json, Files("idle.mp4", "walk.mp4"));

            var idle = set.Clips[set.Idle];
            Assert.AreEqual("idle", idle.Name);
            Assert.AreEqual(VideoMaskKind.VideoAlpha, idle.Mask);
            Assert.IsFalse(idle.Loop);
            Assert.AreEqual(VideoAvatarManifest.MaxScale, idle.Scale, 0.0001f);
            Assert.AreEqual(-VideoAvatarManifest.MaxClipOffset, idle.OffsetX, 0.0001f);

            var walk = set.Clips[set.Walk];
            Assert.IsTrue(walk.FacesRight);
            Assert.AreEqual(VideoAvatarManifest.MaxWalkSpeed, walk.Speed, 0.0001f);
            Assert.AreEqual(2, set.Clips.Count);
            Assert.AreEqual(1, set.Warnings.Count);
        }

        [Test]
        public void ClipSet_SameAs_SeesAChangedFileOrValue()
        {
            var a = VideoAvatarManifest.Resolve(Manifest, DeliveredFiles());
            var b = VideoAvatarManifest.Resolve(Manifest, DeliveredFiles());
            var c = VideoAvatarManifest.Resolve(Manifest.Replace("0.27", "0.35"), DeliveredFiles());
            var d = VideoAvatarManifest.Resolve(null, DeliveredFiles());

            Assert.IsTrue(a.SameAs(b));
            Assert.IsFalse(a.SameAs(c));
            Assert.IsFalse(a.SameAs(d));
            Assert.IsFalse(a.SameAs(null));
        }

        [Test]
        public void Status_ListsWhatIsLoadedAndWhatWasLeftOut()
        {
            var text = VideoAvatarStatus.DescribeSet(VideoAvatarManifest.Resolve(Manifest, DeliveredFiles()));

            StringAssert.Contains("Playing idle.mp4", text);
            StringAssert.Contains("Idle variations: 1", text);
            StringAssert.Contains("Walk: walk.", text);
            StringAssert.Contains("Walk-stop: walk-stop.", text);
            StringAssert.Contains("happy", text);
            StringAssert.Contains(VideoAvatarManifest.FileName, text);
        }

        // ---- the director: idle and variations ------------------------------------------

        [Test]
        public void Director_StartsOnTheDefaultIdle()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
            Assert.AreEqual(-1, director.Queued);
            Assert.IsFalse(director.Mirrored);
        }

        [Test]
        public void Director_AVariationStartsOnlyAtTheIdleLoopPoint()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            var entry = director.Entry;

            // Before the delay has passed, a loop point changes nothing.
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);

            director.Tick(1.5f);
            Assert.GreaterOrEqual(director.Queued, 0);
            // Queued is not playing: the idle is still mid-loop.
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
            Assert.AreEqual(entry, director.Entry);

            var queued = director.Queued;
            director.OnBoundary(true);

            Assert.AreEqual(VideoAvatarAct.Variation, director.Act);
            Assert.AreEqual(queued, director.Clip);
            Assert.AreEqual(entry + 1, director.Entry);
            Assert.Contains(queued, set.Variations);
        }

        [Test]
        public void Director_AVariationThatIsNotLoadedYet_WaitsForTheNextLoopPoint()
        {
            var director = new VideoAvatarDirector(Set(), 1);
            director.Tick(1.5f);
            var queued = director.Queued;

            director.OnBoundary(false);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(queued, director.Queued);

            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Variation, director.Act);
        }

        [Test]
        public void Director_AfterAVariation_TheDefaultIdleReturnsAndTheDelayStartsAgain()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            director.Tick(1.5f);
            director.OnBoundary(true);

            // A loop point of the variation itself must not start another one.
            director.Tick(5f);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Variation, director.Act);

            director.OnEnded();

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
            Assert.AreEqual(-1, director.Queued);
            Assert.Greater(director.UntilVariation, 0f);
        }

        [Test]
        public void Director_WithTwoVariations_NeverPlaysTheSameOneTwiceInARow()
        {
            var director = new VideoAvatarDirector(Set(variations: 2), 12345);
            var last = -1;
            for (var i = 0; i < 20; i++)
            {
                director.Tick(1.5f);
                director.OnBoundary(true);
                Assert.AreEqual(VideoAvatarAct.Variation, director.Act);
                Assert.AreNotEqual(last, director.Clip);
                last = director.Clip;
                director.OnEnded();
            }
        }

        [Test]
        public void Director_WithOneVariation_RepeatsIt()
        {
            var set = Set(variations: 1);
            var director = new VideoAvatarDirector(set, 3);
            for (var i = 0; i < 3; i++)
            {
                director.Tick(1.5f);
                director.OnBoundary(true);
                Assert.AreEqual(set.Variations[0], director.Clip);
                director.OnEnded();
            }
        }

        [Test]
        public void Director_WithoutVariations_TheDefaultIdleJustLoops()
        {
            var set = Set(variations: 0);
            var director = new VideoAvatarDirector(set, 1);

            director.Tick(1000f);
            director.OnBoundary(true);

            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(1, director.Entry);
        }

        [Test]
        public void Director_AVariationThatFailedToLoad_IsNeverChosenAgain()
        {
            var set = Set(variations: 2);
            var director = new VideoAvatarDirector(set, 1);
            director.SetUnavailable(set.Variations[0]);

            for (var i = 0; i < 6; i++)
            {
                director.Tick(1.5f);
                Assert.AreEqual(set.Variations[1], director.Queued);
                director.OnBoundary(true);
                director.OnEnded();
            }

            director.SetUnavailable(set.Variations[1]);
            director.Tick(100f);
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void Director_AQueuedVariationThatFails_IsDroppedWithoutLeavingTheIdle()
        {
            var set = Set(variations: 1);
            var director = new VideoAvatarDirector(set, 1);
            director.Tick(1.5f);
            var entry = director.Entry;

            director.SetUnavailable(director.Queued);
            director.OnBoundary(true);

            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(entry, director.Entry);
        }

        [Test]
        public void Director_TheShowingVariationFails_SheReturnsToTheDefaultIdle()
        {
            var set = Set(variations: 1);
            var director = new VideoAvatarDirector(set, 1);
            director.Tick(1.5f);
            director.OnBoundary(true);
            var entry = director.Entry;

            director.SetUnavailable(director.Clip);

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
            Assert.AreEqual(entry + 1, director.Entry);
        }

        [Test]
        public void Director_TheDefaultIdleCannotBeMarkedUnavailable()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);

            director.SetUnavailable(set.Idle);
            director.SetUnavailable(-5);
            director.SetUnavailable(999);

            Assert.AreEqual(set.Idle, director.Clip);
            Assert.AreEqual(1, director.Entry);
        }

        // ---- the director: walking and stopping -----------------------------------------

        [Test]
        public void Director_AWalkStartsAtOnceAndFacesTheWayTheClipDoes()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            var entry = director.Entry;

            WalkLeft(director);

            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(set.Walk, director.Clip);
            Assert.IsFalse(director.Mirrored);
            Assert.AreEqual(entry + 1, director.Entry);

            // More frames of the same walk do not enter the clip again.
            WalkLeft(director);
            WalkLeft(director);
            Assert.AreEqual(entry + 1, director.Entry);
        }

        [Test]
        public void Director_WalkingRight_MirrorsTheLeftFacingClip()
        {
            var director = new VideoAvatarDirector(Set(), 1);

            director.SetMovement(true, true, false, false);
            Assert.IsTrue(director.Mirrored);

            // Turning mid-walk flips the picture without restarting the clip.
            var entry = director.Entry;
            director.SetMovement(true, false, false, false);
            Assert.IsFalse(director.Mirrored);
            Assert.AreEqual(entry, director.Entry);
        }

        [Test]
        public void Director_AClipThatFacesRight_IsMirroredForWalkingLeft()
        {
            var set = Set();
            set.Clips[set.Walk].FacesRight = true;
            var director = new VideoAvatarDirector(set, 1);

            director.SetMovement(true, false, false, false);
            Assert.IsTrue(director.Mirrored);

            director.SetMovement(true, true, false, false);
            Assert.IsFalse(director.Mirrored);
        }

        [Test]
        public void Director_AWalkInterruptsAVariationAndDropsTheQueue()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            director.Tick(1.5f);
            Assert.GreaterOrEqual(director.Queued, 0);

            WalkLeft(director);
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(-1, director.Queued);

            // A loop point of the walk never starts a variation.
            director.Tick(100f);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
        }

        [Test]
        public void Director_AStop_WaitsForTheWalkLoopPoint_ThenPlaysTheWalkStop()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            var entry = director.Entry;

            // The presence reports: stop asked, still at full speed.
            director.SetMovement(true, false, true, false);
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(entry, director.Entry);

            director.OnBoundary(false);

            Assert.AreEqual(VideoAvatarAct.WalkStop, director.Act);
            Assert.AreEqual(set.WalkStop, director.Clip);
            Assert.AreEqual(entry + 1, director.Entry);
        }

        [Test]
        public void Director_WhileTheWalkStopCarriesHer_ItIsNotMistakenForANewWalk()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            director.SetMovement(true, false, true, false);
            director.OnBoundary(false);
            var entry = director.Entry;

            // The presence still moves the window while the walk-stop takes its last steps.
            for (var i = 0; i < 100; i++)
                director.SetMovement(true, false, true, false);

            Assert.AreEqual(VideoAvatarAct.WalkStop, director.Act);
            Assert.AreEqual(entry, director.Entry);
        }

        [Test]
        public void Director_AWalkWithoutAStopRequest_KeepsLoopingAtItsLoopPoint()
        {
            var director = new VideoAvatarDirector(Set(), 1);
            WalkLeft(director);
            var entry = director.Entry;

            director.OnBoundary(false);
            director.OnBoundary(true);

            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(entry, director.Entry);
        }

        [Test]
        public void Director_AWalkThatEndsAtOnce_PlaysTheWalkStopWithoutWaiting()
        {
            // A screen edge, a drag, or a fall ends the walk with no lead.
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);

            Stand(director);

            Assert.AreEqual(VideoAvatarAct.WalkStop, director.Act);
            Assert.AreEqual(set.WalkStop, director.Clip);
        }

        [Test]
        public void Director_SlowingDownWhileTheWalkStillShows_StartsTheWalkStop()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);

            director.SetMovement(true, false, false, true);

            Assert.AreEqual(VideoAvatarAct.WalkStop, director.Act);
        }

        [Test]
        public void Director_AfterTheWalkStop_SheHoldsItsLastFrame()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            Stand(director);
            var entry = director.Entry;

            director.OnEnded();

            Assert.AreEqual(VideoAvatarAct.Hold, director.Act);
            Assert.AreEqual(set.WalkStop, director.Clip);
            // No new entry: the player must not restart the clip or change the picture.
            Assert.AreEqual(entry, director.Entry);

            // Standing still, loop points, and time do not send her to the front idle.
            for (var i = 0; i < 50; i++)
            {
                Stand(director);
                director.Tick(10f);
                director.OnBoundary(true);
                director.OnEnded();
            }

            Assert.AreEqual(VideoAvatarAct.Hold, director.Act);
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(entry, director.Entry);
        }

        [Test]
        public void Director_TheHeldPose_ReturnsToTheFrontOnlyWhenAsked()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            Stand(director);
            director.OnEnded();

            director.RequestFront();

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
            Assert.IsFalse(director.Mirrored);
        }

        [Test]
        public void Director_AWalkStopThatEndsInTheIdlePose_IsFollowedByTheDefaultIdle()
        {
            var set = Set(thenIdle: true);
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            Stand(director);

            director.OnEnded();

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
        }

        [Test]
        public void Director_ASideIdle_FollowsTheWalkStopAndKeepsHerDirection()
        {
            var set = Set(side: true, thenIdle: true);
            var director = new VideoAvatarDirector(set, 1);
            director.SetMovement(true, true, false, false);
            Stand(director);
            Assert.IsTrue(director.Mirrored);

            director.OnEnded();

            Assert.AreEqual(VideoAvatarAct.SideIdle, director.Act);
            Assert.AreEqual(set.SideIdle, director.Clip);
            Assert.IsTrue(director.Mirrored);

            // The side idle loops. It does not start variations and does not turn to the front.
            director.Tick(100f);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.SideIdle, director.Act);
        }

        [Test]
        public void Director_TheWalkStop_IsMirroredLikeTheWalkBeforeIt()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            director.SetMovement(true, true, false, false);

            Stand(director);

            Assert.AreEqual(VideoAvatarAct.WalkStop, director.Act);
            Assert.IsTrue(director.Mirrored);
        }

        [Test]
        public void Director_ANewWalkDuringTheWalkStop_StartsWalkingAgain()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            director.SetMovement(true, false, true, false);
            director.OnBoundary(false);
            var entry = director.Entry;

            // The presence cancelled its stop: walking, no lead, the other way.
            director.SetMovement(true, true, false, false);

            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(set.Walk, director.Clip);
            Assert.IsTrue(director.Mirrored);
            Assert.AreEqual(entry + 1, director.Entry);

            // The end of the abandoned walk-stop arrives late and must be ignored.
            director.OnEnded();
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
        }

        [Test]
        public void Director_ANewWalkFromTheHeldPose_StartsWalking()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);
            Stand(director);
            director.OnEnded();

            WalkLeft(director);

            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(set.Walk, director.Clip);
        }

        [Test]
        public void Director_StopAndWalkEveryFrame_NeverLeavesAnUnknownState()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 99);
            for (var i = 0; i < 400; i++)
            {
                switch (i % 7)
                {
                    case 0: director.SetMovement(true, i % 2 == 0, false, false); break;
                    case 1: director.SetMovement(true, false, true, false); break;
                    case 2: director.OnBoundary(i % 3 == 0); break;
                    case 3: director.SetMovement(false, false, false, false); break;
                    case 4: director.OnEnded(); break;
                    case 5: director.Tick(0.7f); break;
                    default: director.RequestFront(); break;
                }

                Assert.GreaterOrEqual(director.Clip, 0);
                Assert.Less(director.Clip, set.Clips.Count);
                if (director.Act == VideoAvatarAct.Walk)
                    Assert.AreEqual(set.Walk, director.Clip);
                if (director.Act == VideoAvatarAct.Idle)
                    Assert.AreEqual(set.Idle, director.Clip);
                if (director.Act == VideoAvatarAct.WalkStop || director.Act == VideoAvatarAct.Hold)
                    Assert.AreEqual(set.WalkStop, director.Clip);
            }
        }

        [Test]
        public void Director_RequestFront_IsIgnoredWhileWalking()
        {
            var director = new VideoAvatarDirector(Set(), 1);
            WalkLeft(director);
            var entry = director.Entry;

            director.RequestFront();

            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(entry, director.Entry);
        }

        [Test]
        public void Director_WithoutAWalkClip_WalkingLeavesTheIdleOnScreen()
        {
            var set = Set(walk: false, stop: false);
            var director = new VideoAvatarDirector(set, 1);

            WalkLeft(director);
            Stand(director);

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(1, director.Entry);
        }

        [Test]
        public void Director_WithoutAWalkStopClip_TheWalkEndsOnTheDefaultIdle()
        {
            var set = Set(stop: false);
            var director = new VideoAvatarDirector(set, 1);
            WalkLeft(director);

            Stand(director);

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
        }

        [Test]
        public void Director_AWalkStopThatFailedToLoad_IsSkipped()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            director.SetUnavailable(set.WalkStop);
            WalkLeft(director);

            Stand(director);

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void Director_AWalkClipThatFailedToLoad_IsNotEntered()
        {
            var set = Set();
            var director = new VideoAvatarDirector(set, 1);
            director.SetUnavailable(set.Walk);

            WalkLeft(director);

            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        // ---- speed that follows the clip -------------------------------------------------

        [TestCase(0f, 0f)]
        [TestCase(0.15f, 0f)]
        [TestCase(0.3f, 0.5f)]
        [TestCase(0.45f, 1f)]
        [TestCase(2.5f, 1f)]
        [TestCase(4.8f, 0.5f)]
        [TestCase(4.95f, 0f)]
        [TestCase(5.1f, 0f)]
        [TestCase(-1f, 0f)]
        public void Gait_SpeedRisesAfterTheFirstStepAndFallsBeforeTheLast(float seconds, float expected)
        {
            // The delivered walk clip: she stands, starts at 0.15 s, plants her last step at 4.95 s.
            Assert.AreEqual(expected, VideoGait.Factor(seconds, 0.15f, 4.95f, 0.3f, 5.1667f), 0.001f);
        }

        [Test]
        public void Gait_NoMoveEnd_MeansTheWholeClip()
        {
            Assert.AreEqual(1f, VideoGait.Factor(2f, 0f, 0f, 0.25f, 5f), 0.001f);
            Assert.AreEqual(0f, VideoGait.Factor(5f, 0f, 0f, 0.25f, 5f), 0.001f);
            Assert.AreEqual(5f, VideoGait.MoveEnd(0f, 5f), 0.001f);
            Assert.AreEqual(3.8f, VideoGait.MoveEnd(3.8f, 5f), 0.001f);
            Assert.AreEqual(5f, VideoGait.MoveEnd(9f, 5f), 0.001f);
        }

        [Test]
        public void Gait_NoRamp_IsFullSpeedInsideTheWindow()
        {
            Assert.AreEqual(1f, VideoGait.Factor(0.2f, 0.1f, 1f, 0f, 2f), 0.001f);
            Assert.AreEqual(0f, VideoGait.Factor(0.05f, 0.1f, 1f, 0f, 2f), 0.001f);
            Assert.AreEqual(0f, VideoGait.Factor(0.5f, 1f, 0.5f, 0.2f, 2f), 0.001f);
            Assert.AreEqual(0f, VideoGait.Factor(float.NaN, 0f, 1f, 0.2f, 2f), 0.001f);
        }

        // ---- frame arithmetic -----------------------------------------------------------

        [Test]
        public void Progress_LoopPointIsReportedTwoFramesEarly()
        {
            // 124 frames: the last one is 123.
            Assert.IsFalse(VideoClipProgress.NearLoopPoint(120, 124));
            Assert.IsTrue(VideoClipProgress.NearLoopPoint(121, 124));
            Assert.IsTrue(VideoClipProgress.NearLoopPoint(123, 124));
            Assert.IsFalse(VideoClipProgress.NearLoopPoint(123, 0));
            Assert.IsFalse(VideoClipProgress.NearLoopPoint(-1, 124));
        }

        [Test]
        public void Progress_AWrapIsAFrameNumberThatWentDown()
        {
            Assert.IsTrue(VideoClipProgress.Wrapped(123, 0));
            Assert.IsTrue(VideoClipProgress.Wrapped(122, 1));
            Assert.IsFalse(VideoClipProgress.Wrapped(10, 11));
            Assert.IsFalse(VideoClipProgress.Wrapped(10, 10));
            Assert.IsFalse(VideoClipProgress.Wrapped(-1, 0));
        }

        [Test]
        public void Progress_APlayOnceClipIsHeldOneFrameBeforeItsEnd()
        {
            Assert.IsFalse(VideoClipProgress.AtEnd(121, 124));
            Assert.IsTrue(VideoClipProgress.AtEnd(122, 124));
            Assert.IsTrue(VideoClipProgress.AtEnd(123, 124));
            Assert.IsFalse(VideoClipProgress.AtEnd(123, 0));
        }

        [Test]
        public void Progress_SecondsToTheLoopPoint()
        {
            Assert.AreEqual(123f / 24f, VideoClipProgress.SecondsToLoopPoint(0, 124, 24f), 0.001f);
            Assert.AreEqual(1f, VideoClipProgress.SecondsToLoopPoint(99, 124, 24f), 0.001f);
            Assert.AreEqual(0f, VideoClipProgress.SecondsToLoopPoint(123, 124, 24f), 0.001f);
            Assert.AreEqual(0f, VideoClipProgress.SecondsToLoopPoint(10, 0, 24f), 0.001f);
            Assert.AreEqual(0f, VideoClipProgress.SecondsToLoopPoint(10, 124, 0f), 0.001f);
        }

        [Test]
        public void Progress_AStaleFrameNumberIsNotAFreshPicture()
        {
            // Just started: frame 0 is not proof of a new picture, frame 1 is.
            Assert.IsFalse(VideoClipProgress.IsFresh(0, 0.05f, 24f));
            Assert.IsFalse(VideoClipProgress.IsFresh(-1, 0.05f, 24f));
            Assert.IsTrue(VideoClipProgress.IsFresh(1, 0.05f, 24f));
            Assert.IsTrue(VideoClipProgress.IsFresh(3, 0.1f, 24f));

            // The decoder still reports the frame it stopped on before the rewind.
            Assert.IsFalse(VideoClipProgress.IsFresh(123, 0.05f, 24f));
            Assert.IsFalse(VideoClipProgress.IsFresh(60, 0.5f, 24f));

            // Later in the run the same number is plausible.
            Assert.IsTrue(VideoClipProgress.IsFresh(60, 2.5f, 24f));
            // An unknown frame rate falls back to 30.
            Assert.IsTrue(VideoClipProgress.IsFresh(5, 0.1f, 0f));
        }

        // ---- reactions --------------------------------------------------------------------

        static VideoAvatarClipSet ReactionSet()
        {
            var set = VideoAvatarManifest.Resolve(null, Files(
                "idle.sbs.mp4", "idle_breeze.sbs.mp4", "walk.sbs.mp4", "walk_stop.sbs.mp4",
                "happy.sbs.mp4", "concerned.sbs.mp4", "turn360.sbs.mp4", "walk_pivot180.sbs.mp4"));
            set.VariationMinSeconds = 1f;
            set.VariationMaxSeconds = 1f;
            return set;
        }

        [Test]
        public void FileNames_TheNewClips_AreReactions_AndThePivotIsNotASecondWalk()
        {
            var set = ReactionSet();

            Assert.AreEqual(0, set.Warnings.Count);
            Assert.AreEqual("walk", set.Clips[set.Walk].Name);
            Assert.AreEqual("walk_stop", set.Clips[set.WalkStop].Name);
            Assert.AreEqual(1, set.Variations.Count);
            foreach (var name in new[] { "happy", "concerned", "turn360", "walk_pivot180" })
            {
                var clip = set.FindReaction(name);
                Assert.GreaterOrEqual(clip, 0, name);
                Assert.IsFalse(set.Clips[clip].Loop, name);
                Assert.AreEqual(VideoMaskKind.Packed, set.Clips[clip].Mask, name);
            }
        }

        [Test]
        public void Reaction_StartsFromTheIdleAsSoonAsItIsLoaded_AndReturnsToTheIdle()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);
            var entry = director.Entry;

            Assert.IsTrue(director.RequestReaction("Happy"));
            // Asked, not loaded yet: the idle stays, and the player is told which clip to load.
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.FindReaction("happy"), director.Queued);
            Assert.IsTrue(director.QueuedIsReaction);
            Assert.AreEqual(entry, director.Entry);

            // Loaded: it starts without waiting for the idle's loop point.
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);
            Assert.AreEqual(set.FindReaction("happy"), director.Clip);
            Assert.IsFalse(director.Mirrored);
            Assert.AreEqual(-1, director.Queued);
            Assert.IsFalse(director.QueuedIsReaction);

            director.OnEnded();
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
        }

        [Test]
        public void Reaction_AlsoStartsAtTheIdleLoopPoint_WhenItIsLoadedBythen()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);

            director.RequestReaction("concerned");
            director.OnBoundary(false);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);
            Assert.AreEqual(set.FindReaction("concerned"), director.Clip);
        }

        [Test]
        public void Reaction_UnknownOrMissingClip_IsRefused_AndNothingChanges()
        {
            var director = new VideoAvatarDirector(Set(), 1);
            var entry = director.Entry;

            Assert.IsFalse(director.RequestReaction("happy"));
            Assert.IsFalse(director.RequestReaction(null));
            Assert.IsFalse(director.RequestReaction("walk"));
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(entry, director.Entry);
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void Reaction_DoesNotStartWhileSheWalksStopsOrPlaysAnotherClip()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);

            director.SetMovement(true, false, false, false);
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.IsFalse(director.RequestReaction("happy"));

            director.SetMovement(true, false, true, false);
            director.OnBoundary(false);
            Assert.AreEqual(VideoAvatarAct.WalkStop, director.Act);
            Assert.IsFalse(director.RequestReaction("happy"));

            // A reaction is not interrupted by another reaction.
            var second = new VideoAvatarDirector(set, 1);
            second.RequestReaction("happy");
            second.OnQueuedReady();
            Assert.IsFalse(second.RequestReaction("concerned"));
            Assert.AreEqual(set.FindReaction("happy"), second.Clip);
        }

        [Test]
        public void Reaction_AWalkInterruptsIt_AndAWaitingOneIsDropped()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);

            director.RequestReaction("turn360");
            director.OnQueuedReady();
            director.SetMovement(true, true, false, false);
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.IsTrue(director.Mirrored);

            var waiting = new VideoAvatarDirector(set, 1);
            waiting.RequestReaction("turn360");
            waiting.SetMovement(true, false, false, false);
            Assert.AreEqual(-1, waiting.Queued);
            waiting.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Walk, waiting.Act);
        }

        [Test]
        public void Reaction_TakesThePlaceOfAWaitingVariation_AndVariationsStillWaitForTheLoopPoint()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);

            director.Tick(1.5f);
            Assert.AreEqual(set.Variations[0], director.Queued);
            Assert.IsFalse(director.QueuedIsReaction);
            // A loaded variation does not start early.
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);

            Assert.IsTrue(director.RequestReaction("happy"));
            Assert.AreEqual(set.FindReaction("happy"), director.Queued);
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);

            // Back in the idle, variations are scheduled again.
            director.OnEnded();
            director.Tick(1.5f);
            Assert.AreEqual(set.Variations[0], director.Queued);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Variation, director.Act);
        }

        [Test]
        public void Reaction_ThatFailsToLoad_IsDroppedAndNeverAskedAgain()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);
            var happy = set.FindReaction("happy");

            director.RequestReaction("happy");
            director.SetUnavailable(happy);
            Assert.AreEqual(-1, director.Queued);
            Assert.IsFalse(director.QueuedIsReaction);
            Assert.IsFalse(director.RequestReaction("happy"));

            // Failing while it shows: back to the idle.
            director.RequestReaction("concerned");
            director.OnQueuedReady();
            director.SetUnavailable(set.FindReaction("concerned"));
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        // ---- the clips delivered on 2026-11-12 and the held back pose -----------------------

        static readonly string[] PackedNames =
        {
            "idle", "idle_breeze", "walk", "walk_stop", "happy", "concerned", "turn360",
            "embarrassed", "flustered", "pout", "wink_dance", "turn_around_back", "back_idle",
        };

        static string[] PackedFolder()
        {
            var names = new string[PackedNames.Length];
            for (var i = 0; i < names.Length; i++)
                names[i] = PackedNames[i] + ".sbs.mp4";
            return Files(names);
        }

        // The same entries as tools/video-avatar/leia-keyed.video-avatar.json for the new clips.
        const string PoseManifest = @"{ ""clips"": [
            { ""name"": ""idle"", ""color"": ""idle.sbs.mp4"" },
            { ""name"": ""walk"", ""color"": ""walk.sbs.mp4"" },
            { ""name"": ""embarrassed"", ""role"": ""reaction"", ""color"": ""embarrassed.sbs.mp4"", ""playback"": ""once"" },
            { ""name"": ""pout"", ""role"": ""reaction"", ""color"": ""pout.sbs.mp4"", ""playback"": ""once"" },
            { ""name"": ""turn_around_back"", ""role"": ""reaction"", ""color"": ""turn_around_back.sbs.mp4"", ""playback"": ""once"", ""after"": ""back_idle"" },
            { ""name"": ""back_idle"", ""role"": ""reaction"", ""color"": ""back_idle.sbs.mp4"", ""playback"": ""loop"", ""hold"": 12 } ] }";

        [Test]
        public void FileNames_TheNewClips_AreReactions_AndBackIdleIsNotARandomIdleVariation()
        {
            var set = VideoAvatarManifest.Resolve(null, PackedFolder());

            Assert.AreEqual(0, set.Warnings.Count);
            Assert.AreEqual(1, set.Variations.Count, "Only idle_breeze is a variation. back_idle must never play at random.");
            foreach (var name in new[] { "embarrassed", "flustered", "pout", "wink_dance", "turn_around_back", "back_idle" })
            {
                var clip = set.FindReaction(name);
                Assert.GreaterOrEqual(clip, 0, name);
                Assert.AreEqual(VideoMaskKind.Packed, set.Clips[clip].Mask, name);
                Assert.IsFalse(set.Clips[clip].Loop, name);
                Assert.AreEqual(string.Empty, set.Clips[clip].After, name);
            }
        }

        [Test]
        public void Manifest_TurnAroundBack_IsFollowedByTheHeldBackPose()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            Assert.AreEqual(0, set.Warnings.Count);

            var turn = set.FindReaction("turn_around_back");
            var back = set.FindReaction("back_idle");
            Assert.AreEqual("back_idle", set.Clips[turn].After);
            Assert.IsTrue(set.Clips[back].Loop);
            Assert.AreEqual(12f, set.Clips[back].HoldSeconds, 0.001f);
            Assert.AreEqual(string.Empty, set.Clips[set.FindReaction("pout")].After);
        }

        [Test]
        public void Pose_FollowsTheReaction_IsHeld_ThenSheFacesFrontAtALoopPoint()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);
            var back = set.FindReaction("back_idle");

            Assert.IsTrue(director.RequestReaction("turn_around_back"));
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);
            // The pose is loaded while the turn plays.
            Assert.AreEqual(back, director.Next);

            director.OnEnded();
            Assert.AreEqual(VideoAvatarAct.Pose, director.Act);
            Assert.AreEqual(back, director.Clip);
            Assert.IsFalse(director.Mirrored);
            Assert.AreEqual(-1, director.Next);
            Assert.AreEqual(12f, director.PoseLeft, 0.001f);

            // Held: a loop point before the time is up keeps the pose, and nothing else starts.
            director.Tick(5f);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Pose, director.Act);
            Assert.IsFalse(director.RequestReaction("pout"));
            Assert.AreEqual(-1, director.Queued);

            // Time is up: she faces front at the next loop point, not in the middle of the loop.
            director.Tick(8f);
            Assert.AreEqual(VideoAvatarAct.Pose, director.Act);
            director.OnBoundary(false);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            Assert.AreEqual(set.Idle, director.Clip);
        }

        [Test]
        public void Pose_EndsAtOnce_ForTheFrontButtonOrAWalk()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());

            var front = new VideoAvatarDirector(set, 1);
            front.RequestReaction("turn_around_back");
            front.OnQueuedReady();
            front.OnEnded();
            front.RequestFront();
            Assert.AreEqual(VideoAvatarAct.Idle, front.Act);

            var walk = new VideoAvatarDirector(set, 1);
            walk.RequestReaction("turn_around_back");
            walk.OnQueuedReady();
            walk.OnEnded();
            walk.SetMovement(true, false, false, false);
            Assert.AreEqual(VideoAvatarAct.Walk, walk.Act);
        }

        [Test]
        public void Pose_ThatFailsToLoad_SendsHerBackToTheIdle()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);
            var back = set.FindReaction("back_idle");

            director.RequestReaction("turn_around_back");
            director.OnQueuedReady();
            director.SetUnavailable(back);
            Assert.AreEqual(-1, director.Next);
            director.OnEnded();
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void Manifest_AfterThatIsNotALoopingReaction_IsIgnoredWithAWarning()
        {
            const string json = @"{ ""clips"": [
                { ""name"": ""idle"", ""color"": ""idle.sbs.mp4"" },
                { ""name"": ""turn_around_back"", ""role"": ""reaction"", ""color"": ""turn_around_back.sbs.mp4"", ""after"": ""back_idle"" },
                { ""name"": ""back_idle"", ""role"": ""reaction"", ""color"": ""back_idle.sbs.mp4"" },
                { ""name"": ""pout"", ""role"": ""reaction"", ""color"": ""pout.sbs.mp4"", ""after"": ""nowhere"" } ] }";

            var set = VideoAvatarManifest.Resolve(json, PackedFolder());

            Assert.AreEqual(2, set.Warnings.Count);
            Assert.AreEqual(string.Empty, set.Clips[set.FindReaction("turn_around_back")].After);
            Assert.AreEqual(string.Empty, set.Clips[set.FindReaction("pout")].After);
            var director = new VideoAvatarDirector(set, 1);
            director.RequestReaction("turn_around_back");
            director.OnQueuedReady();
            director.OnEnded();
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void Reaction_NamesFollowTheEmotions()
        {
            Assert.IsNull(VideoReactionNames.For(AICompanion.Core.Emotion.Neutral));
            Assert.AreEqual("happy", VideoReactionNames.For(AICompanion.Core.Emotion.Happy));
            Assert.AreEqual("concerned", VideoReactionNames.For(AICompanion.Core.Emotion.Concerned));
            Assert.IsNull(VideoReactionNames.For((AICompanion.Core.Emotion)99));

            // Every emotion in the list has the name of its enum value, in lower case.
            foreach (AICompanion.Core.Emotion emotion in System.Enum.GetValues(typeof(AICompanion.Core.Emotion)))
            {
                if (emotion == AICompanion.Core.Emotion.Neutral)
                    continue;
                Assert.AreEqual(emotion.ToString().ToLowerInvariant(), VideoReactionNames.For(emotion));
            }
        }

        // ---- reactions asked for while she is busy (JUH-99, 2026-11-12) -------------------

        [Test]
        public void Request_WhileWalking_WaitsAndPlaysWhenSheIsBackOnTheIdle()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);
            var pout = set.FindReaction("pout");

            director.SetMovement(true, false, false, false);
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Waiting, director.Request("pout", true));
            Assert.AreEqual(pout, director.PendingReaction);
            Assert.AreEqual(-1, director.Queued);

            // Still walking: it waits.
            director.Tick(1f);
            Assert.AreEqual(pout, director.PendingReaction);

            // She stops and is back on the idle within the wait: it is queued and starts when loaded.
            director.SetMovement(false, false, false, false);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
            director.Tick(0.1f);
            Assert.AreEqual(-1, director.PendingReaction);
            Assert.AreEqual(pout, director.Queued);
            Assert.IsTrue(director.QueuedIsReaction);
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);
            Assert.AreEqual(pout, director.Clip);
        }

        [Test]
        public void Request_StillLoadingWhenAWalkStarts_WaitsInsteadOfBeingLost()
        {
            // A reply with both a reaction and a walk: the reaction is accepted on the idle,
            // then the walk starts before its clip has loaded.
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);
            var pout = set.FindReaction("pout");

            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Started, director.Request("pout", true));
            director.SetMovement(true, false, false, false);
            Assert.AreEqual(VideoAvatarAct.Walk, director.Act);
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(pout, director.PendingReaction);

            director.SetMovement(false, false, false, false);
            director.Tick(0.1f);
            Assert.AreEqual(pout, director.Queued);
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);
        }

        [Test]
        public void Request_ThatWaitsTooLong_IsDropped()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);

            director.SetMovement(true, false, false, false);
            director.Request("pout", true);
            director.Tick(VideoAvatarDirector.PendingSeconds + 0.1f);
            Assert.AreEqual(-1, director.PendingReaction);

            director.SetMovement(false, false, false, false);
            director.Tick(0.1f);
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(VideoAvatarAct.Idle, director.Act);
        }

        [Test]
        public void Request_DuringAnotherReaction_PlaysAfterIt_AndTheLatestWins()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);

            director.Request("embarrassed", true);
            director.OnQueuedReady();
            Assert.AreEqual(VideoAvatarAct.Reaction, director.Act);

            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Waiting, director.Request("pout", true));
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Waiting, director.Request("turn_around_back", true));
            Assert.AreEqual(set.FindReaction("turn_around_back"), director.PendingReaction);

            director.OnEnded();
            director.Tick(0.1f);
            Assert.AreEqual(set.FindReaction("turn_around_back"), director.Queued);
        }

        [Test]
        public void Request_DuringAVariation_PlaysAfterIt()
        {
            var set = ReactionSet();
            var director = new VideoAvatarDirector(set, 1);

            director.Tick(1.5f);
            director.OnBoundary(true);
            Assert.AreEqual(VideoAvatarAct.Variation, director.Act);

            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Waiting, director.Request("happy", true));
            director.OnEnded();
            director.Tick(0.1f);
            Assert.AreEqual(set.FindReaction("happy"), director.Queued);
            Assert.IsTrue(director.QueuedIsReaction);
        }

        [Test]
        public void Request_FromAnEmotion_NeverReplacesOneTheReplyNamed()
        {
            var set = ReactionSet();

            // Loading on the idle.
            var idle = new VideoAvatarDirector(set, 1);
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Started, idle.Request("concerned", true));
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Refused, idle.Request("happy", false));
            Assert.AreEqual(set.FindReaction("concerned"), idle.Queued);
            // A newer named one does replace it.
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Started, idle.Request("turn360", true));
            Assert.AreEqual(set.FindReaction("turn360"), idle.Queued);

            // Waiting while she walks.
            var walking = new VideoAvatarDirector(set, 1);
            walking.SetMovement(true, false, false, false);
            walking.Request("concerned", true);
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Refused, walking.Request("happy", false));
            Assert.AreEqual(set.FindReaction("concerned"), walking.PendingReaction);

            // An emotion reaction does give way to a named one.
            var emotion = new VideoAvatarDirector(set, 1);
            emotion.Request("happy", false);
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Started, emotion.Request("concerned", true));
            Assert.AreEqual(set.FindReaction("concerned"), emotion.Queued);
        }

        [Test]
        public void Request_UnknownClipOrAHeldPose_IsRefused()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);

            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Refused, director.Request("moonwalk", true));
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Refused, director.Request(null, true));
            // back_idle only follows turn_around_back. On its own it would start with her back turned.
            Assert.AreEqual(AICompanion.Core.AvatarReactionResult.Refused, director.Request("back_idle", true));
            Assert.AreEqual(-1, director.Queued);
            Assert.AreEqual(-1, director.PendingReaction);
        }

        [Test]
        public void Request_WaitingForAClipThatFailsToLoad_IsDropped()
        {
            var set = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            var director = new VideoAvatarDirector(set, 1);

            director.SetMovement(true, false, false, false);
            director.Request("pout", true);
            director.SetUnavailable(set.FindReaction("pout"));
            Assert.AreEqual(-1, director.PendingReaction);
        }

        [Test]
        public void OfferedReactions_LeaveOutHeldPosesAndWalkTurns()
        {
            var poses = VideoAvatarManifest.Resolve(PoseManifest, PackedFolder());
            CollectionAssert.AreEquivalent(new[] { "embarrassed", "pout", "turn_around_back" }, poses.OfferedReactions());

            var files = VideoAvatarManifest.Resolve(null, Files(
                "idle.sbs.mp4", "walk.sbs.mp4", "happy.sbs.mp4", "pout.sbs.mp4", "walk_pivot180.sbs.mp4"));
            CollectionAssert.AreEquivalent(new[] { "happy", "pout" }, files.OfferedReactions());
        }

        // ---- clip sound -----------------------------------------------------------------

        [Test]
        public void Manifest_Sound_IsOnlyForClipsThatSayIt()
        {
            const string json = @"{ ""clips"": [
                { ""name"": ""idle"", ""color"": ""idle.sbs.mp4"" },
                { ""name"": ""pout"", ""role"": ""reaction"", ""color"": ""pout.sbs.mp4"", ""sound"": true } ] }";

            var set = VideoAvatarManifest.Resolve(json, PackedFolder());

            Assert.IsTrue(set.Clips[set.FindReaction("pout")].Sound);
            Assert.IsFalse(set.Clips[set.Idle].Sound);
            Assert.IsFalse(VideoAvatarManifest.Resolve(null, PackedFolder()).Clips[0].Sound, "Without a manifest no clip has sound.");
        }

        [Test]
        public void ClipSound_IsSilentWhenOff_Muted_OrWhileSheSpeaks()
        {
            Assert.AreEqual(0.7f, VideoClipSound.Volume(true, 0.7f, true, false, false), 0.0001f);
            Assert.AreEqual(0f, VideoClipSound.Volume(false, 0.7f, true, false, false));
            Assert.AreEqual(0f, VideoClipSound.Volume(true, 0.7f, false, false, false));
            Assert.AreEqual(0f, VideoClipSound.Volume(true, 0.7f, true, true, false), "The Mute button silences clip sound too.");
            Assert.AreEqual(0f, VideoClipSound.Volume(true, 0.7f, true, false, true), "Never over her TTS voice.");
            Assert.AreEqual(1f, VideoClipSound.Volume(true, 3f, true, false, false));
            Assert.AreEqual(VideoClipSound.DefaultVolume, VideoClipSound.ClampVolume(float.NaN));

            Assert.AreEqual("clip sound pout: silent while she speaks", VideoClipSound.Describe("pout", true, false, true));
            Assert.AreEqual("clip sound pout: off (Play clip sounds is off)", VideoClipSound.Describe("pout", false, false, false));
        }

        [Test]
        public void Settings_ClipSound_IsOffByDefault_AndSurvivesASaveAndLoad()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsFalse(fresh.PlayClipSounds);
            Assert.AreEqual(VideoClipSound.DefaultVolume, fresh.ClipSoundVolume, 0.0001f);

            fresh.PlayClipSounds = true;
            fresh.ClipSoundVolume = 0.35f;
            var loaded = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None);
            Assert.IsTrue(loaded.PlayClipSounds);
            Assert.AreEqual(0.35f, loaded.ClipSoundVolume, 0.001f);

            var broken = CompanionSettingsJson.Parse("{ \"version\": 1, \"clipSoundVolume\": 7 }", SettingsLegacyPrefs.None);
            Assert.AreEqual(1f, broken.ClipSoundVolume, 0.0001f);
            Assert.IsFalse(broken.PlayClipSounds);
        }

        [Test]
        public void ShippedManifest_IsPackedAndMarksTheSixVoiceClips()
        {
            var path = Path.Combine(Application.dataPath, "..", "tools", "video-avatar", "leia-keyed.video-avatar.json");
            var set = VideoAvatarManifest.Resolve(File.ReadAllText(path), PackedFolder());

            Assert.AreEqual(0, set.Warnings.Count);
            Assert.AreEqual(13, set.Clips.Count);
            foreach (var clip in set.Clips)
                Assert.AreEqual(VideoMaskKind.Packed, clip.Mask, clip.Name);
            foreach (var name in new[] { "embarrassed", "flustered", "pout", "wink_dance", "turn_around_back", "back_idle" })
                Assert.IsTrue(set.Clips[set.FindReaction(name)].Sound, name);
            Assert.IsFalse(set.Clips[set.Idle].Sound);
            Assert.IsFalse(set.Clips[set.Walk].Sound);
            Assert.AreEqual("back_idle", set.Clips[set.FindReaction("turn_around_back")].After);
        }

        // ---- keeping the mask on the picture's frame -------------------------------------

        [TestCase(10L, 10L, 124L, 0L)]
        [TestCase(10L, 11L, 124L, 1L)]
        [TestCase(11L, 10L, 124L, -1L)]
        [TestCase(40L, 12L, 124L, -28L)]
        // At the loop point: the mask has started again and the picture is on its last frame.
        [TestCase(123L, 0L, 124L, 1L)]
        [TestCase(0L, 123L, 124L, -1L)]
        [TestCase(122L, 1L, 124L, 3L)]
        // No frame yet, or an unknown length.
        [TestCase(-1L, 5L, 124L, 0L)]
        [TestCase(5L, -1L, 124L, 0L)]
        [TestCase(10L, 13L, 0L, 3L)]
        public void MaskSync_OffsetIsMaskMinusPicture_AcrossTheLoopPoint(long color, long mask, long count, long expected)
        {
            Assert.AreEqual(expected, VideoMaskSync.SignedOffset(color, mask, count));
        }

        // ---- the sync log (Windows log on 960aab3) --------------------------------------

        [Test]
        public void SyncCounter_CountsEachPictureFrameOnce_NotEachAppFrame()
        {
            var sync = new VideoSyncCounter();
            long offset;

            // The app draws at 60 fps, the clip plays at 24: each picture frame is seen 2 or 3 times.
            for (var app = 0; app < 60; app++)
            {
                var frame = (long)(app * 24 / 60);
                sync.Sample(frame, frame, 124, true, out offset);
            }

            Assert.AreEqual(24, sync.Frames);
            Assert.AreEqual(24, sync.InStep);
        }

        [Test]
        public void SyncCounter_ARewindOrSeekInFlight_IsSkipped_NotCountedAsDrift()
        {
            var sync = new VideoSyncCounter();
            long offset;

            // After a rewind the picture is on 0 while the mask still reports its old frame.
            Assert.IsFalse(sync.Sample(0, 87, 124, false, out offset));
            Assert.IsFalse(sync.Sample(1, 87, 124, false, out offset));
            // No frame yet from the mask.
            Assert.IsFalse(sync.Sample(2, -1, 124, true, out offset));
            // Settled.
            Assert.IsTrue(sync.Sample(3, 3, 124, true, out offset));

            Assert.AreEqual(1, sync.Frames);
            Assert.AreEqual(0, sync.OffByMore);
            Assert.AreEqual(3, sync.Skipped);
        }

        [Test]
        public void SyncCounter_TheLoopPoint_IsOneFrame_NotTheWholeClip()
        {
            var sync = new VideoSyncCounter();
            long offset;

            sync.Sample(123, 0, 124, true, out offset);
            Assert.AreEqual(1, offset);
            sync.Sample(0, 0, 124, true, out offset);

            Assert.AreEqual(1, sync.OffByOne);
            Assert.AreEqual(1, sync.InStep);
            Assert.AreEqual(0, sync.OffByMore);
        }

        [Test]
        public void SyncCounter_ANewRun_CountsItsFirstFrameEvenWhenTheNumberRepeats()
        {
            var sync = new VideoSyncCounter();
            long offset;

            sync.Sample(0, 0, 124, true, out offset);
            sync.Restart();
            sync.Sample(0, 0, 124, true, out offset);

            Assert.AreEqual(2, sync.Frames);
        }

        [Test]
        public void SyncCounter_Report_NamesTheFiles_AndCoversPackedClips()
        {
            var sync = new VideoSyncCounter();
            long offset;
            Assert.IsNull(sync.Report("idle", "idle.sbs.mp4", 1));

            for (var f = 0L; f < 10; f++)
                sync.Sample(f, f, 124, true, out offset);
            var packed = sync.Report("idle", "idle.sbs.mp4", 1);
            StringAssert.Contains("sync idle file=idle.sbs.mp4 decoders=1 frames=10", packed);
            StringAssert.DoesNotContain("offByMore", packed);

            // Reading it starts a new count.
            Assert.IsNull(sync.Report("idle", "idle.sbs.mp4", 1));

            sync.Sample(0, 0, 124, true, out offset);
            sync.Sample(1, 3, 124, true, out offset);
            sync.Sample(2, 3, 124, true, out offset);
            sync.Sample(3, 3, 124, false, out offset);
            sync.CountSeek();
            var pair = sync.Report("walk", "walk.mp4+walk.mask.mp4", 2);
            Assert.AreEqual(
                "sync walk file=walk.mp4+walk.mask.mp4 decoders=2 frames=3 inStep=33.3% offByOne=33.3% offByMore=33.3% skipped=1 seeks=1",
                pair);
        }

        [Test]
        public void SyncCounter_OnlySkippedFrames_ReportNoPercentages()
        {
            var sync = new VideoSyncCounter();
            long offset;
            sync.Sample(0, 50, 124, false, out offset);

            StringAssert.Contains("frames=0 inStep=n/a", sync.Report("walk", "walk.mp4+walk.mask.mp4", 2));
        }

        [Test]
        public void MaskSync_OneFrameOff_IsNudgedBack_NotJumped()
        {
            // Behind: play a little faster. Ahead: a little slower. In step, or far off: normal speed.
            Assert.Greater(VideoMaskSync.MaskSpeed(-1), 1f);
            Assert.Less(VideoMaskSync.MaskSpeed(1), 1f);
            Assert.AreEqual(1f, VideoMaskSync.MaskSpeed(0), 0.0001f);
            Assert.AreEqual(1f, VideoMaskSync.MaskSpeed(5), 0.0001f);
            Assert.AreEqual(1f, VideoMaskSync.MaskSpeed(-5), 0.0001f);

            // The jump is kept for more than one frame, and a nudge is never used there.
            Assert.IsFalse(VideoMaskSync.NeedsResync(10, 11, 124));
            Assert.IsTrue(VideoMaskSync.NeedsResync(10, 12, 124));
        }

        [Test]
        public void MaskSync_TheNudgeClosesAOneFrameGapWithinASecondOrSo()
        {
            // 24 fps clip, mask one frame behind, playing at the catch-up speed.
            var gapFrames = 1f;
            var seconds = 0f;
            while (gapFrames > 0f && seconds < 5f)
            {
                gapFrames -= (VideoMaskSync.CatchUpSpeed - 1f) * 24f * (1f / 60f);
                seconds += 1f / 60f;
            }

            Assert.Less(seconds, 1f);
        }

        [Test]
        public void Packed_EachHalfIsReadInsideItsOwnEdges()
        {
            // 1536 px wide file: picture in columns 0..767, mask in 768..1535.
            var texel = 1f / 1536f;
            var uv = VideoAvatarLayout.PackedRects(texel, false);

            // u runs 0..1 across the clip on screen. The picture's samples stay left of the middle.
            Assert.Greater(uv.Color.z, 0f);
            Assert.Less(uv.Color.x * 1f + uv.Color.z, 0.5f);
            // The mask's samples stay right of the middle and left of the file's edge.
            Assert.Greater(uv.Mask.z, 0.5f);
            Assert.Less(uv.Mask.x * 1f + uv.Mask.z, 1f);
            // Both halves have the same width, so the mask lines up with the picture.
            Assert.AreEqual(uv.Color.x, uv.Mask.x, 0.000001f);
            Assert.AreEqual(0.5f, uv.Mask.z - uv.Color.z, 0.000001f);
            // The first sample is the centre of the half's first column.
            Assert.AreEqual(0.5f / 1536f, uv.Color.z, 0.000001f);
        }

        [Test]
        public void Packed_Mirrored_ReadsTheSameColumnsRightToLeft()
        {
            var texel = 1f / 1536f;
            var plain = VideoAvatarLayout.PackedRects(texel, false);
            var mirrored = VideoAvatarLayout.PackedRects(texel, true);

            // u = 0 mirrored is u = 1 plain, and the other way round, for both halves.
            Assert.AreEqual(plain.Color.x + plain.Color.z, mirrored.Color.z, 0.000001f);
            Assert.AreEqual(plain.Color.z, mirrored.Color.x + mirrored.Color.z, 0.000001f);
            Assert.AreEqual(plain.Mask.x + plain.Mask.z, mirrored.Mask.z, 0.000001f);
            Assert.AreEqual(plain.Mask.z, mirrored.Mask.x + mirrored.Mask.z, 0.000001f);
        }

        [Test]
        public void Packed_UnknownWidth_UsesPlainHalves()
        {
            var uv = VideoAvatarLayout.PackedRects(0f, false);

            Assert.AreEqual(0.5f, uv.Color.x, 0.000001f);
            Assert.AreEqual(0f, uv.Color.z, 0.000001f);
            Assert.AreEqual(0.5f, uv.Mask.z, 0.000001f);
        }

        // ---- the expression portrait and the avatar mode ---------------------------------

        [Test]
        public void Portrait_FollowsTheHeadOfTheAvatarOnScreen_ThroughEveryModeSwitch()
        {
            var host = new GameObject("portrait-mode-host");
            try
            {
                var mount = host.AddComponent<AICompanion.Avatar.AvatarMount>();
                Assert.AreEqual(PortraitAnchorSource.Model3D, PortraitAnchor.Source(mount.ModelHidden, false));

                // The states the video avatar goes through, in the order a session can see them.
                var states = new[]
                {
                    VideoAvatarState.Loading, VideoAvatarState.Playing, VideoAvatarState.Off,
                    VideoAvatarState.Loading, VideoAvatarState.Playing, VideoAvatarState.Failed,
                    VideoAvatarState.NoClip, VideoAvatarState.Loading, VideoAvatarState.Playing,
                    VideoAvatarState.NoFolder, VideoAvatarState.Off,
                };
                for (var i = 0; i < states.Length; i++)
                {
                    // VideoAvatar hides the model in exactly the states HidesModel names, and
                    // knows her head only while a clip plays.
                    mount.SetModelHidden(VideoAvatarStatus.HidesModel(states[i]));
                    var playing = states[i] == VideoAvatarState.Playing;
                    var expected = playing ? PortraitAnchorSource.Video : PortraitAnchorSource.Model3D;
                    Assert.AreEqual(expected, PortraitAnchor.Source(mount.ModelHidden, playing), states[i].ToString());
                }

                // Never the hidden 3D head: before the video's head is known, a fixed point.
                Assert.AreEqual(PortraitAnchorSource.Fixed, PortraitAnchor.Source(true, false));
                Assert.AreEqual(PortraitAnchorSource.Model3D, PortraitAnchor.Source(mount.ModelHidden, false));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Portrait_VideoHead_IsAtTheIdleHead_InTopLeftPixels()
        {
            // A 912 x 1368 window, the idle 760 tall from 20 px above the bottom.
            var idle = new Rect(100f, 20f, 434f, 760f);
            var head = PortraitAnchor.VideoHead(idle, 1368f, PortraitAnchor.DefaultHeadX, PortraitAnchor.DefaultHeadY);

            Assert.AreEqual(100f + 434f * 0.5f, head.x, 0.01f);
            // 15% down from the top of the frame: 20 + 760 * 0.85 from the bottom.
            Assert.AreEqual(1368f - (20f + 760f * 0.85f), head.y, 0.01f);

            // Unset or broken values use the defaults.
            Assert.AreEqual(head, PortraitAnchor.VideoHead(idle, 1368f, 0f, 0f));
            Assert.AreEqual(head, PortraitAnchor.VideoHead(idle, 1368f, float.NaN, 2f));
        }

        [Test]
        public void Portrait_VideoHead_StaysPutWhileSheWalksStopsAndReacts()
        {
            // The head comes from the default idle's own rectangle (with the idle's offsetY
            // from the delivered manifest), which no other clip changes, so a walk, a mirrored
            // walk, or a reaction cannot move it.
            var idle = VideoAvatarLayout.ComputeAligned(912f, 1368f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 1f, 0f, -0.017f, false);
            var before = PortraitAnchor.VideoHead(idle, 1368f, PortraitAnchor.DefaultHeadX, PortraitAnchor.DefaultHeadY);

            var walk = VideoAvatarLayout.ComputeAligned(912f, 1368f, 768f, 1344f, 768f, 1344f, 1f, 0f, -0.017f, 1f, 0.05f, 0f, true);
            Assert.AreNotEqual(idle, walk);
            var again = VideoAvatarLayout.ComputeAligned(912f, 1368f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 1f, 0f, -0.017f, false);
            // The idle's offset moves the head with her: 1.7% of her height lower than without it.
            var unshifted = VideoAvatarLayout.Compute(912f, 1368f, 768f, 1344f, 1f, 0f, 0f);
            var plainHead = PortraitAnchor.VideoHead(unshifted, 1368f, PortraitAnchor.DefaultHeadX, PortraitAnchor.DefaultHeadY);
            Assert.AreEqual(plainHead.y + 0.017f * unshifted.height, before.y, 0.5f);
            Assert.AreEqual(before, PortraitAnchor.VideoHead(again, 1368f, PortraitAnchor.DefaultHeadX, PortraitAnchor.DefaultHeadY));

            // The portrait beside that head is the same rectangle every frame, and it is not on her body.
            var none = new Rect(0f, 0f, 0f, 0f);
            var first = PortraitPlacement.Beside(before, 912f, 1368f, none, none, false);
            var second = PortraitPlacement.Beside(before, 912f, 1368f, none, none, false);
            Assert.AreEqual(first, second);
            Assert.Less(first.center.y, 1368f - (idle.y + idle.height * 0.7f), "The portrait is level with her head, not her chest.");
        }

        [Test]
        public void Manifest_HeadPosition_CanBeSet_AndDefaultsOtherwise()
        {
            var plain = VideoAvatarManifest.Resolve(null, DeliveredFiles());
            Assert.AreEqual(PortraitAnchor.DefaultHeadX, plain.HeadX);
            Assert.AreEqual(PortraitAnchor.DefaultHeadY, plain.HeadY);

            var set = VideoAvatarManifest.Resolve(@"{ ""headX"": 0.45, ""headY"": 0.12, ""clips"": [ { ""name"": ""idle"", ""color"": ""idle.sbs.mp4"" } ] }", DeliveredFiles());
            Assert.AreEqual(0.45f, set.HeadX, 0.0001f);
            Assert.AreEqual(0.12f, set.HeadY, 0.0001f);
        }

        // ---- layout for a set of clips ---------------------------------------------------

        [Test]
        public void Layout_AClipLikeTheReference_SitsExactlyWhereTheSingleClipDid()
        {
            var single = VideoAvatarLayout.Compute(480f, 720f, 768f, 1344f, 0.8f, 0.1f, -0.05f);
            var aligned = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 0.8f, 0.1f, -0.05f, 1f, 0f, 0f, false);

            Assert.AreEqual(single.x, aligned.x, 0.01f);
            Assert.AreEqual(single.y, aligned.y, 0.01f);
            Assert.AreEqual(single.width, aligned.width, 0.01f);
            Assert.AreEqual(single.height, aligned.height, 0.01f);
        }

        [Test]
        public void Layout_AClipWithOtherFraming_KeepsItsOwnAspectAndTheSameCentreAndGround()
        {
            var reference = VideoAvatarLayout.Compute(480f, 720f, 768f, 1344f, 1f, 0f, 0f);
            // A wider render of the walk, the same height.
            var wide = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 1344f, 1344f, 1f, 0f, 0f, 1f, 0f, 0f, false);

            Assert.AreEqual(reference.height, wide.height, 0.01f);
            Assert.AreEqual(wide.height, wide.width, 0.01f);
            Assert.AreEqual(reference.x + reference.width * 0.5f, wide.x + wide.width * 0.5f, 0.01f);
            Assert.AreEqual(reference.y, wide.y, 0.01f);
        }

        [Test]
        public void Layout_PerClipScaleAndOffsets_AreSharesOfTheClipsHeightOnScreen()
        {
            var plain = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 1f, 0f, 0f, false);
            var tuned = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 0.5f, 0.1f, -0.02f, false);

            Assert.AreEqual(plain.height * 0.5f, tuned.height, 0.01f);
            Assert.AreEqual(plain.width / plain.height, tuned.width / tuned.height, 0.0001f);
            var centre = plain.x + plain.width * 0.5f;
            Assert.AreEqual(centre + 0.1f * tuned.height, tuned.x + tuned.width * 0.5f, 0.01f);
            Assert.AreEqual(plain.y - 0.02f * tuned.height, tuned.y, 0.01f);
        }

        [Test]
        public void Layout_AMirroredClip_FlipsItsSidewaysOffset()
        {
            var left = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 1f, 0.1f, 0f, false);
            var right = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 1f, 0.1f, 0f, true);
            var centred = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, 1f, 0f, 0f, false);

            Assert.AreEqual(centred.x - left.x, right.x - centred.x, 0.01f);
            Assert.Greater(left.x, centred.x);
            Assert.AreEqual(left.y, right.y, 0.01f);
        }

        [Test]
        public void Layout_BadClipNumbers_FallBackToTheReference()
        {
            var bad = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 768f, 1344f, 1f, 0f, 0f, float.NaN, float.NaN, float.PositiveInfinity, false);
            var good = VideoAvatarLayout.Compute(480f, 720f, 768f, 1344f, 1f, 0f, 0f);
            var empty = VideoAvatarLayout.ComputeAligned(480f, 720f, 768f, 1344f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, false);

            Assert.AreEqual(good.x, bad.x, 0.01f);
            Assert.AreEqual(good.height, bad.height, 0.01f);
            Assert.AreEqual(0f, empty.width, 0.0001f);
        }

        [Test]
        public void Layout_WalkingSpeedScalesWithHerSizeOnScreen()
        {
            var full = VideoAvatarLayout.DisplayedHeight(480f, 720f, 768f, 1344f, 1f, 1f);
            var half = VideoAvatarLayout.DisplayedHeight(480f, 720f, 768f, 1344f, 0.5f, 1f);
            var bigWindow = VideoAvatarLayout.DisplayedHeight(960f, 1440f, 768f, 1344f, 1f, 1f);

            Assert.AreEqual(720f, full, 0.01f);
            Assert.AreEqual(360f, half, 0.01f);
            Assert.AreEqual(1440f, bigWindow, 0.01f);
        }

        // ---- settings -------------------------------------------------------------------

        [Test]
        public void Settings_WalkSpeed_DefaultsToTheClipAndSurvivesAWriteAndARead()
        {
            var settings = CompanionSettings.CreateDefault();
            Assert.AreEqual(0f, settings.VideoAvatarWalkSpeed, 0.0001f);

            settings.VideoAvatarWalkSpeed = 0.35f;
            var loaded = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.AreEqual(0.35f, loaded.VideoAvatarWalkSpeed, 0.001f);

            settings.VideoAvatarWalkSpeed = -2f;
            settings.Normalize();
            Assert.AreEqual(0f, settings.VideoAvatarWalkSpeed, 0.0001f);

            settings.VideoAvatarWalkSpeed = 500f;
            settings.Normalize();
            Assert.AreEqual(VideoAvatarManifest.MaxWalkSpeed, settings.VideoAvatarWalkSpeed, 0.0001f);
        }
    }
}
