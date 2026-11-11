using System;
using System.IO;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-99. The clip lookup, the layout, the mask sync rule, and the settings fields are
    /// plain logic. Playback and transparency need a Windows build and are checked by hand.
    /// </summary>
    public class VideoAvatarTests
    {
        static readonly string Folder = Path.Combine("C:", "Users", "me", "AppData", "Roaming", "ProjectLeia", "video-avatar");

        static string In(string fileName)
        {
            return Path.Combine(Folder, fileName);
        }

        [TestCase(null, "3d")]
        [TestCase("", "3d")]
        [TestCase("3d", "3d")]
        [TestCase("something", "3d")]
        [TestCase("video", "video")]
        [TestCase(" Video ", "video")]
        public void Mode_NormalizesToThreeDUnlessVideo(string input, string expected)
        {
            Assert.AreEqual(expected, AvatarModeNames.Normalize(input));
        }

        [Test]
        public void Settings_DefaultIsThe3DAvatar()
        {
            var settings = CompanionSettings.CreateDefault();

            Assert.AreEqual(AvatarModeNames.ThreeD, settings.AvatarMode);
            Assert.IsFalse(AvatarModeNames.IsVideo(settings.AvatarMode));
            Assert.AreEqual(string.Empty, settings.VideoAvatarFolder);
            Assert.AreEqual(1f, settings.VideoAvatarSize, 0.0001f);
            Assert.AreEqual(0f, settings.VideoAvatarOffsetX, 0.0001f);
            Assert.AreEqual(0f, settings.VideoAvatarOffsetY, 0.0001f);
        }

        [Test]
        public void Settings_OldFileWithoutVideoFields_StaysOn3D()
        {
            var settings = CompanionSettingsJson.Parse("{ \"providerId\": \"grok\", \"scale\": 1.2 }", SettingsLegacyPrefs.None);

            Assert.AreEqual(AvatarModeNames.ThreeD, settings.AvatarMode);
            Assert.AreEqual(1f, settings.VideoAvatarSize, 0.0001f);
        }

        [Test]
        public void Settings_VideoFieldsSurviveAWriteAndARead()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.AvatarMode = AvatarModeNames.Video;
            settings.VideoAvatarFolder = In("leia");
            settings.VideoAvatarSize = 0.75f;
            settings.VideoAvatarOffsetX = -0.25f;
            settings.VideoAvatarOffsetY = 0.1f;

            var loaded = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);

            Assert.AreEqual(AvatarModeNames.Video, loaded.AvatarMode);
            Assert.AreEqual(In("leia"), loaded.VideoAvatarFolder);
            Assert.AreEqual(0.75f, loaded.VideoAvatarSize, 0.001f);
            Assert.AreEqual(-0.25f, loaded.VideoAvatarOffsetX, 0.001f);
            Assert.AreEqual(0.1f, loaded.VideoAvatarOffsetY, 0.001f);
        }

        [Test]
        public void Settings_Normalize_ClampsSizeAndPositionAndDropsAWebAddress()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.AvatarMode = "hologram";
            settings.VideoAvatarFolder = "https://example.com/clips";
            settings.VideoAvatarSize = 9f;
            settings.VideoAvatarOffsetX = -4f;
            settings.VideoAvatarOffsetY = float.NaN;

            settings.Normalize();

            Assert.AreEqual(AvatarModeNames.ThreeD, settings.AvatarMode);
            Assert.AreEqual(string.Empty, settings.VideoAvatarFolder);
            Assert.AreEqual(VideoAvatarLayout.MaxSize, settings.VideoAvatarSize, 0.0001f);
            Assert.AreEqual(-VideoAvatarLayout.MaxOffsetX, settings.VideoAvatarOffsetX, 0.0001f);
            Assert.AreEqual(0f, settings.VideoAvatarOffsetY, 0.0001f);
        }

        [Test]
        public void Files_PictureAlone_UsesTheClipsOwnAlpha()
        {
            var clip = VideoAvatarFiles.Find(new[] { In("notes.txt"), In("idle.mp4") }, VideoAvatarClipNames.Idle);

            Assert.IsTrue(clip.Found);
            Assert.AreEqual(In("idle.mp4"), clip.ColorPath);
            Assert.AreEqual(VideoMaskKind.VideoAlpha, clip.Mask);
            Assert.AreEqual(string.Empty, clip.MaskPath);
        }

        [Test]
        public void Files_MaskClip_WinsOverMaskPicture()
        {
            var clip = VideoAvatarFiles.Find(
                new[] { In("idle.mask.png"), In("idle.mp4"), In("idle.mask.mp4") },
                VideoAvatarClipNames.Idle);

            Assert.AreEqual(VideoMaskKind.MaskVideo, clip.Mask);
            Assert.AreEqual(In("idle.mp4"), clip.ColorPath);
            Assert.AreEqual(In("idle.mask.mp4"), clip.MaskPath);
        }

        [Test]
        public void Files_MaskPicture_IsUsedWhenThereIsNoMaskClip()
        {
            var clip = VideoAvatarFiles.Find(new[] { In("IDLE.MP4"), In("Idle.Mask.PNG") }, VideoAvatarClipNames.Idle);

            Assert.AreEqual(VideoMaskKind.MaskImage, clip.Mask);
            Assert.AreEqual(In("IDLE.MP4"), clip.ColorPath);
            Assert.AreEqual(In("Idle.Mask.PNG"), clip.MaskPath);
        }

        [Test]
        public void Files_PackedClip_IsUsedWhenThereIsNoSeparateMask()
        {
            var packedOnly = VideoAvatarFiles.Find(new[] { In("idle.sbs.mp4") }, VideoAvatarClipNames.Idle);
            var packedAndPlain = VideoAvatarFiles.Find(new[] { In("idle.mp4"), In("idle.sbs.mp4") }, VideoAvatarClipNames.Idle);
            var maskBeatsPacked = VideoAvatarFiles.Find(new[] { In("idle.mp4"), In("idle.sbs.mp4"), In("idle.mask.png") }, VideoAvatarClipNames.Idle);

            Assert.AreEqual(VideoMaskKind.Packed, packedOnly.Mask);
            Assert.AreEqual(In("idle.sbs.mp4"), packedOnly.ColorPath);
            Assert.AreEqual(VideoMaskKind.Packed, packedAndPlain.Mask);
            Assert.AreEqual(VideoMaskKind.MaskImage, maskBeatsPacked.Mask);
        }

        [Test]
        public void Files_WebmIsPreferred_BecauseItCanCarryAlpha()
        {
            var clip = VideoAvatarFiles.Find(new[] { In("idle.mp4"), In("idle.webm") }, VideoAvatarClipNames.Idle);

            Assert.AreEqual(In("idle.webm"), clip.ColorPath);
        }

        [Test]
        public void Files_OtherClipNamesDoNotMatchIdle()
        {
            var files = new[] { In("talk.mp4"), In("idle2.mp4"), In("my-idle.mp4"), In("idle.mask.png"), In("idle.txt") };

            Assert.IsFalse(VideoAvatarFiles.Find(files, VideoAvatarClipNames.Idle).Found);
            Assert.IsFalse(VideoAvatarFiles.Find(null, VideoAvatarClipNames.Idle).Found);
            Assert.IsFalse(VideoAvatarFiles.Find(files, " ").Found);
        }

        [Test]
        public void Files_ALaterClipIsFoundByItsOwnName()
        {
            // Talking and reaction clips are not played yet. The lookup already finds them.
            var clip = VideoAvatarFiles.Find(new[] { In("idle.mp4"), In("talk.mp4"), In("talk.mask.mp4") }, "talk");

            Assert.AreEqual(In("talk.mp4"), clip.ColorPath);
            Assert.AreEqual(VideoMaskKind.MaskVideo, clip.Mask);
        }

        [Test]
        public void Files_FileNameReadsBothSlashKinds()
        {
            Assert.AreEqual("idle.mp4", VideoAvatarFiles.FileName("C:\\clips\\idle.mp4"));
            Assert.AreEqual("idle.mp4", VideoAvatarFiles.FileName("/home/me/clips/idle.mp4"));
            Assert.AreEqual("idle.mp4", VideoAvatarFiles.FileName("idle.mp4"));
            Assert.AreEqual(string.Empty, VideoAvatarFiles.FileName(null));
        }

        [Test]
        public void Clip_DefaultValueIsNotFound()
        {
            var empty = default(VideoAvatarClip);

            Assert.IsFalse(empty.Found);
            Assert.IsTrue(empty.SameFiles(default(VideoAvatarClip)));
        }

        [Test]
        public void Folder_NamedFolderWinsWhenItExists()
        {
            var app = Path.Combine("C:", "Users", "me", "AppData", "Roaming");
            var persistent = Path.Combine("C:", "Users", "me", "AppData", "LocalLow", "AI Companion");
            var primary = VideoAvatarFolders.Primary(app, persistent);
            var custom = Path.Combine("D:", "clips");
            Func<string, bool> all = path => true;
            Func<string, bool> onlyPrimary = path => string.Equals(path, primary, StringComparison.OrdinalIgnoreCase);

            StringAssert.EndsWith("video-avatar", primary);
            Assert.AreEqual(custom, VideoAvatarFolders.Select(custom, app, persistent, all));
            Assert.AreEqual(primary, VideoAvatarFolders.Select(custom, app, persistent, onlyPrimary));
            Assert.AreEqual(primary, VideoAvatarFolders.Select("  ", app, persistent, all));
            Assert.IsNull(VideoAvatarFolders.Select(custom, app, persistent, path => false));
            Assert.IsNull(VideoAvatarFolders.Select(custom, app, persistent, null));
        }

        [Test]
        public void Folder_AWebAddressIsNeverUsed()
        {
            var app = Path.Combine("C:", "Users", "me", "AppData", "Roaming");
            var primary = VideoAvatarFolders.Primary(app, app);

            Assert.IsTrue(VideoAvatarFolders.LooksRemote("https://example.com/clips"));
            Assert.IsFalse(VideoAvatarFolders.LooksRemote(Path.Combine("D:", "clips")));
            Assert.AreEqual(primary, VideoAvatarFolders.Select("https://example.com/clips", app, app, path => true));
        }

        [Test]
        public void Layout_KeepsTheClipsAspectRatio()
        {
            // A 9:16 portrait clip in the 480x720 window is limited by the height.
            var tall = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 1f, 0f, 0f);
            // A wide clip is limited by the width.
            var wide = VideoAvatarLayout.Compute(480f, 720f, 1920f, 1080f, 1f, 0f, 0f);

            Assert.AreEqual(720f, tall.height, 0.01f);
            Assert.AreEqual(405f, tall.width, 0.01f);
            Assert.AreEqual(1080f / 1920f, tall.width / tall.height, 0.0001f);
            Assert.AreEqual(480f, wide.width, 0.01f);
            Assert.AreEqual(270f, wide.height, 0.01f);
            Assert.AreEqual(1920f / 1080f, wide.width / wide.height, 0.0001f);
        }

        [Test]
        public void Layout_StandsOnTheBottomEdgeInTheMiddle()
        {
            var rect = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 1f, 0f, 0f);

            Assert.AreEqual(0f, rect.y, 0.01f);
            Assert.AreEqual((480f - 405f) * 0.5f, rect.x, 0.01f);
        }

        [Test]
        public void Layout_SizeShrinksWithoutChangingTheShape()
        {
            var full = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 1f, 0f, 0f);
            var half = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 0.5f, 0f, 0f);

            Assert.AreEqual(full.height * 0.5f, half.height, 0.01f);
            Assert.AreEqual(full.width / full.height, half.width / half.height, 0.0001f);
            Assert.AreEqual(0f, half.y, 0.01f);
            Assert.AreEqual((480f - half.width) * 0.5f, half.x, 0.01f);
        }

        [Test]
        public void Layout_OffsetsMoveByAShareOfTheWindow()
        {
            var rect = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 0.5f, 0.25f, 0.1f);
            var centred = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 0.5f, 0f, 0f);

            Assert.AreEqual(centred.x + 120f, rect.x, 0.01f);
            Assert.AreEqual(72f, rect.y, 0.01f);
            Assert.AreEqual(centred.width, rect.width, 0.01f);
        }

        [Test]
        public void Layout_OutOfRangeValuesAreClamped()
        {
            var big = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 50f, 9f, -9f);
            var tiny = VideoAvatarLayout.Compute(480f, 720f, 1080f, 1920f, 0f, 0f, 0f);

            Assert.AreEqual(720f, big.height, 0.01f);
            Assert.AreEqual((480f - 405f) * 0.5f + 240f, big.x, 0.01f);
            Assert.AreEqual(-VideoAvatarLayout.MaxOffsetY * 720f, big.y, 0.01f);
            Assert.AreEqual(720f * VideoAvatarLayout.MinSize, tiny.height, 0.01f);
        }

        [Test]
        public void Layout_NoSizeYet_IsAnEmptyRect()
        {
            Assert.AreEqual(0f, VideoAvatarLayout.Compute(0f, 720f, 1080f, 1920f, 1f, 0f, 0f).width, 0.0001f);
            Assert.AreEqual(0f, VideoAvatarLayout.Compute(480f, 720f, 0f, 0f, 1f, 0f, 0f).height, 0.0001f);
            Assert.AreEqual(0f, VideoAvatarLayout.Compute(480f, 720f, float.NaN, 1920f, 1f, 0f, 0f).width, 0.0001f);
        }

        [Test]
        public void Layout_PackedClipIsHalfAsWideAsItsFile()
        {
            Assert.AreEqual(1080f, VideoAvatarLayout.DisplayWidth(2160f, VideoMaskKind.Packed), 0.01f);
            Assert.AreEqual(1080f, VideoAvatarLayout.DisplayWidth(1080f, VideoMaskKind.MaskVideo), 0.01f);
            Assert.AreEqual(1080f, VideoAvatarLayout.DisplayWidth(1080f, VideoMaskKind.VideoAlpha), 0.01f);
        }

        [TestCase(10L, 10L, 120L, false)]
        [TestCase(10L, 11L, 120L, false)]
        [TestCase(10L, 13L, 120L, true)]
        [TestCase(40L, 12L, 120L, true)]
        // At the loop point the two clips are one frame apart, not 119.
        [TestCase(0L, 119L, 120L, false)]
        [TestCase(119L, 0L, 120L, false)]
        [TestCase(2L, 118L, 120L, true)]
        // A decoder reports -1 before its first frame.
        [TestCase(-1L, 30L, 120L, false)]
        [TestCase(30L, -1L, 120L, false)]
        // An unknown length falls back to the plain difference.
        [TestCase(10L, 13L, 0L, true)]
        public void MaskSync_ResyncsOnlyWhenTheMaskIsMoreThanOneFrameOff(long color, long mask, long count, bool expected)
        {
            Assert.AreEqual(expected, VideoMaskSync.NeedsResync(color, mask, count));
        }

        [Test]
        public void Status_OnlyAPlayingClipHidesThe3DModel()
        {
            Assert.IsTrue(VideoAvatarStatus.HidesModel(VideoAvatarState.Playing));
            Assert.IsFalse(VideoAvatarStatus.HidesModel(VideoAvatarState.Off));
            Assert.IsFalse(VideoAvatarStatus.HidesModel(VideoAvatarState.NoFolder));
            Assert.IsFalse(VideoAvatarStatus.HidesModel(VideoAvatarState.NoClip));
            Assert.IsFalse(VideoAvatarStatus.HidesModel(VideoAvatarState.Loading));
            Assert.IsFalse(VideoAvatarStatus.HidesModel(VideoAvatarState.Failed));
        }

        [Test]
        public void Status_SaysWhatIsPlayingAndWhereTransparencyComesFrom()
        {
            var masked = new VideoAvatarClip("idle", In("idle.mp4"), In("idle.mask.mp4"), VideoMaskKind.MaskVideo);
            var plain = new VideoAvatarClip("idle", In("idle.mp4"), null, VideoMaskKind.VideoAlpha);

            var playing = VideoAvatarStatus.Describe(VideoAvatarState.Playing, Folder, masked, null);
            var unmasked = VideoAvatarStatus.Describe(VideoAvatarState.Playing, Folder, plain, null);

            StringAssert.Contains("Playing idle.mp4", playing);
            StringAssert.Contains("idle.mask.mp4", playing);
            StringAssert.Contains("No mask file", unmasked);
        }

        [Test]
        public void Status_ProblemsNameTheFolderOrTheErrorAndThe3DFallback()
        {
            var clip = new VideoAvatarClip("idle", In("idle.mp4"), null, VideoMaskKind.VideoAlpha);

            var noFolder = VideoAvatarStatus.Describe(VideoAvatarState.NoFolder, Folder, default(VideoAvatarClip), null);
            var noClip = VideoAvatarStatus.Describe(VideoAvatarState.NoClip, Folder, default(VideoAvatarClip), null);
            var failed = VideoAvatarStatus.Describe(VideoAvatarState.Failed, Folder, clip, "Unsupported codec");

            StringAssert.Contains(Folder, noFolder);
            StringAssert.Contains("3D avatar", noFolder);
            StringAssert.Contains("idle.mp4", noClip);
            StringAssert.Contains("Unsupported codec", failed);
            StringAssert.Contains("3D avatar", failed);
        }

        [Test]
        public void Build_AlwaysIncludesTheVideoDecodeShaders()
        {
            // The VideoPlayer is created in script, with no VideoClip asset and nothing in the
            // scene. Unity only keeps its decode shaders in a player build when this is Always.
            // With Never, the clip fails to prepare in the built player and only there.
            Assert.AreEqual(VideoShadersIncludeMode.Always, GraphicsSettings.videoShadersIncludeMode);
        }

        [Test]
        public void Shader_IsInResourcesWithThePropertiesTheScriptSets()
        {
            var shader = Resources.Load<Shader>("VideoAvatar");

            Assert.IsNotNull(shader, "Assets/_Project/Resources/VideoAvatar.shader must load, or the clip is drawn without its mask.");
            Assert.AreEqual("AICompanion/VideoAvatar", shader.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "The VideoAvatar shader has a compile error.");
            foreach (var property in new[] { "_MainTex", "_MaskTex", "_ColorRect", "_MaskRect", "_MaskSource", "_MaskDecode", "_MaskLow", "_MaskHigh" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(property), 0, property);
        }
    }
}
