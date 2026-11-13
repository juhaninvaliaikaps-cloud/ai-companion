using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>What a clip is for. The role decides when it plays and whether it loops by default.</summary>
    public enum VideoClipRole
    {
        /// <summary>The default standing loop. Exactly one. Without it the 3D avatar stays.</summary>
        Idle = 0,

        /// <summary>An optional idle that plays once in place of a default idle loop.</summary>
        IdleVariation = 1,

        /// <summary>The walking loop. It is mirrored for the other direction.</summary>
        Walk = 2,

        /// <summary>Plays once when a walk ends. Its last frame is held.</summary>
        WalkStop = 3,

        /// <summary>An optional side-facing standing loop shown after the walk-stop.</summary>
        SideIdle = 4,

        /// <summary>A named full-body reaction such as "happy". Played once from the default idle.</summary>
        Reaction = 5,
    }

    /// <summary>One entry of video-avatar.json. Field names are the JSON keys.</summary>
    [Serializable]
    public sealed class VideoAvatarManifestClip
    {
        public string name;
        public string role;
        public string color;
        public string mask;
        public string playback;
        public string facing;
        public float scale;
        public float offsetX;
        public float offsetY;
        public float speed;
        public float weight;
        public float moveStart;
        public float moveEnd;
        public float moveRamp;
        public string after;
        /// <summary>A pose that follows a reaction: seconds to hold it before facing front. 0 uses the default.</summary>
        public float hold;
        /// <summary>True plays the clip's own sound track (with the "Play clip sounds" setting on).</summary>
        public bool sound;
    }

    /// <summary>The whole video-avatar.json file.</summary>
    [Serializable]
    public sealed class VideoAvatarManifestFile
    {
        public VideoAvatarManifestClip[] clips;
        public float variationMinSeconds;
        public float variationMaxSeconds;
        /// <summary>Where her head is in the default idle's frame, as shares from the left and the top. 0 uses the default.</summary>
        public float headX;
        public float headY;
    }

    /// <summary>One clip that exists on disk, with every value filled in.</summary>
    public sealed class VideoClipSpec
    {
        public string Name = string.Empty;
        public VideoClipRole Role;
        public string ColorPath = string.Empty;
        public string MaskPath = string.Empty;
        public VideoMaskKind Mask;
        public bool Loop;

        /// <summary>True when the character in the file faces right. Walking the other way mirrors the clip.</summary>
        public bool FacesRight;

        /// <summary>Size against the default idle. 1 is the same height on screen.</summary>
        public float Scale = 1f;

        /// <summary>Sideways shift as a share of the clip's height on screen. Positive is right in the file. It flips with the clip when the clip is mirrored.</summary>
        public float OffsetX;

        /// <summary>Upward shift as a share of the clip's height on screen. Use it to put the feet on the same line as the idle.</summary>
        public float OffsetY;

        /// <summary>Walking speed in clip heights per second. Only read on the walk clip.</summary>
        public float Speed = VideoAvatarManifest.DefaultWalkSpeed;

        /// <summary>Seconds into the clip at which her feet start to carry her. The window does not move before it. Read on the walk and the walk-stop.</summary>
        public float MoveStart;

        /// <summary>Seconds into the clip at which her feet stop carrying her. 0 means the end of the clip. Read on the walk and the walk-stop.</summary>
        public float MoveEnd;

        /// <summary>Seconds the window takes to reach full speed after <see cref="MoveStart"/> and to lose it before <see cref="MoveEnd"/>.</summary>
        public float MoveRamp = VideoAvatarManifest.DefaultMoveRamp;

        /// <summary>
        /// Only read on the walk-stop. True when the clip ends facing front in the idle pose,
        /// so the default idle may follow it. False holds its last frame.
        /// </summary>
        public bool ThenIdle;

        /// <summary>
        /// Only read on a reaction. The name of a looping reaction clip that follows it as a
        /// held pose ("turn_around_back" then "back_idle"). Empty: back to the default idle.
        /// </summary>
        public string After = string.Empty;

        /// <summary>Only read on a pose clip: seconds it is held before she faces front again, at a loop point.</summary>
        public float HoldSeconds = VideoAvatarManifest.DefaultHoldSeconds;

        /// <summary>The clip's own sound track is played (manifest "sound": true). Without a manifest, never.</summary>
        public bool Sound;

        /// <summary>How often a variation is picked against the others.</summary>
        public float Weight = 1f;

        public bool SameAs(VideoClipSpec other)
        {
            return other != null
                && Role == other.Role
                && Mask == other.Mask
                && Loop == other.Loop
                && FacesRight == other.FacesRight
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(ColorPath, other.ColorPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(MaskPath, other.MaskPath, StringComparison.OrdinalIgnoreCase)
                && Mathf.Approximately(Scale, other.Scale)
                && Mathf.Approximately(OffsetX, other.OffsetX)
                && Mathf.Approximately(OffsetY, other.OffsetY)
                && Mathf.Approximately(Speed, other.Speed)
                && Mathf.Approximately(MoveStart, other.MoveStart)
                && Mathf.Approximately(MoveEnd, other.MoveEnd)
                && Mathf.Approximately(MoveRamp, other.MoveRamp)
                && ThenIdle == other.ThenIdle
                && string.Equals(After, other.After, StringComparison.OrdinalIgnoreCase)
                && Mathf.Approximately(HoldSeconds, other.HoldSeconds)
                && Sound == other.Sound
                && Mathf.Approximately(Weight, other.Weight);
        }
    }

    /// <summary>Every usable clip in the folder, with the roles the player needs picked out. -1 means "none".</summary>
    public sealed class VideoAvatarClipSet
    {
        public readonly List<VideoClipSpec> Clips = new List<VideoClipSpec>();
        public readonly List<int> Variations = new List<int>();
        public readonly List<string> Warnings = new List<string>();
        public int Idle = -1;
        public int Walk = -1;
        public int WalkStop = -1;
        public int SideIdle = -1;
        public float VariationMinSeconds = VideoAvatarManifest.DefaultVariationMinSeconds;
        public float VariationMaxSeconds = VideoAvatarManifest.DefaultVariationMaxSeconds;
        /// <summary>Her head in the default idle's frame, for the expression portrait. See <see cref="PortraitAnchor"/>.</summary>
        public float HeadX = PortraitAnchor.DefaultHeadX;
        public float HeadY = PortraitAnchor.DefaultHeadY;
        public bool FromManifest;

        /// <summary>False when there is no default idle. The 3D avatar stays on screen then.</summary>
        public bool Found
        {
            get { return Idle >= 0; }
        }

        /// <summary>True when the clip is a pose held after another reaction ("back_idle" after "turn_around_back").</summary>
        public bool IsPoseTarget(int clip)
        {
            if (clip < 0 || clip >= Clips.Count)
                return false;
            for (var i = 0; i < Clips.Count; i++)
            {
                if (i != clip && Clips[i].Role == VideoClipRole.Reaction
                    && string.Equals(Clips[i].After, Clips[clip].Name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The reactions the companion may ask for in a cue: every reaction clip except a pose
        /// held after another reaction, and except walk turns ("walk_pivot180"), which are not
        /// played on their own yet.
        /// </summary>
        public List<string> OfferedReactions()
        {
            var names = new List<string>();
            for (var i = 0; i < Clips.Count; i++)
            {
                var clip = Clips[i];
                if (clip.Role != VideoClipRole.Reaction || IsPoseTarget(i))
                    continue;
                if (clip.Name.StartsWith("walk", StringComparison.OrdinalIgnoreCase))
                    continue;
                names.Add(clip.Name);
            }

            return names;
        }

        /// <summary>Index of a reaction clip by name, or -1.</summary>
        public int FindReaction(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return -1;

            var wanted = name.Trim();
            for (var i = 0; i < Clips.Count; i++)
            {
                if (Clips[i].Role == VideoClipRole.Reaction
                    && string.Equals(Clips[i].Name, wanted, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public bool SameAs(VideoAvatarClipSet other)
        {
            if (other == null || other.Clips.Count != Clips.Count)
                return false;
            if (!Mathf.Approximately(VariationMinSeconds, other.VariationMinSeconds)
                || !Mathf.Approximately(VariationMaxSeconds, other.VariationMaxSeconds))
                return false;

            for (var i = 0; i < Clips.Count; i++)
            {
                if (!Clips[i].SameAs(other.Clips[i]))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Turns a folder listing, and video-avatar.json when the folder has one, into a
    /// <see cref="VideoAvatarClipSet"/>. A clip whose file or mask is missing is left out
    /// with a warning. It never stops the other clips from loading.
    /// </summary>
    public static class VideoAvatarManifest
    {
        public const string FileName = "video-avatar.json";
        public const float DefaultWalkSpeed = 0.3f;
        public const float MaxWalkSpeed = 3f;
        public const float MinScale = 0.25f;
        public const float MaxScale = 4f;
        public const float MaxClipOffset = 1f;
        public const float MaxMoveSeconds = 60f;
        public const float DefaultMoveRamp = 0.25f;
        public const float MaxMoveRamp = 5f;
        public const float DefaultVariationMinSeconds = 12f;
        public const float DefaultVariationMaxSeconds = 30f;

        /// <summary>Seconds a pose after a reaction is held before she faces front again.</summary>
        public const float DefaultHoldSeconds = 12f;
        public const float MaxHoldSeconds = 120f;

        static readonly string[] VideoExtensions = { ".webm", ".mp4", ".mov", ".m4v" };
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
        static readonly string[] MaskSuffixes = { ".mask", "_mask", "-mask" };
        const string PackedSuffix = ".sbs";

        /// <summary>
        /// <paramref name="manifestJson"/> is the text of video-avatar.json, or null when the
        /// folder has none. Without it the clips are found by file name.
        /// </summary>
        public static VideoAvatarClipSet Resolve(string manifestJson, IList<string> filePaths)
        {
            var files = filePaths ?? new string[0];
            if (!string.IsNullOrWhiteSpace(manifestJson))
            {
                VideoAvatarManifestFile parsed = null;
                try
                {
                    parsed = JsonUtility.FromJson<VideoAvatarManifestFile>(manifestJson);
                }
                catch (Exception)
                {
                    parsed = null;
                }

                if (parsed != null && parsed.clips != null && parsed.clips.Length > 0)
                    return FromManifest(parsed, files);

                var fallback = FromFileNames(files);
                fallback.Warnings.Insert(0, FileName + " could not be read or lists no clips. Clips were found by file name.");
                return fallback;
            }

            return FromFileNames(files);
        }

        static VideoAvatarClipSet FromManifest(VideoAvatarManifestFile file, IList<string> files)
        {
            var set = new VideoAvatarClipSet { FromManifest = true };
            ApplyVariationTiming(set, file.variationMinSeconds, file.variationMaxSeconds);
            if (file.headX > 0f && file.headX < 1f)
                set.HeadX = file.headX;
            if (file.headY > 0f && file.headY < 1f)
                set.HeadY = file.headY;

            for (var i = 0; i < file.clips.Length; i++)
            {
                var entry = file.clips[i];
                if (entry == null)
                    continue;

                var colorName = VideoAvatarFiles.FileName(Clean(entry.color));
                var name = Clean(entry.name);
                if (name.Length == 0)
                    name = Stem(colorName);
                if (colorName.Length == 0)
                {
                    set.Warnings.Add("Clip \"" + name + "\" has no \"color\" file name. It was left out.");
                    continue;
                }

                var colorPath = FindFile(files, colorName);
                if (colorPath == null || !HasExtension(colorName, VideoExtensions))
                {
                    set.Warnings.Add("Clip \"" + name + "\": " + colorName + " is not a video file in the folder. It was left out.");
                    continue;
                }

                var spec = new VideoClipSpec { Name = name, ColorPath = colorPath };
                spec.Role = ParseRole(entry.role, name);

                var maskName = VideoAvatarFiles.FileName(Clean(entry.mask));
                if (maskName.Length > 0)
                {
                    var maskPath = FindFile(files, maskName);
                    if (maskPath == null)
                    {
                        // Without its mask the clip would show its whole background.
                        set.Warnings.Add("Clip \"" + name + "\": mask " + maskName + " is not in the folder. It was left out.");
                        continue;
                    }

                    spec.MaskPath = maskPath;
                    spec.Mask = HasExtension(maskName, ImageExtensions) ? VideoMaskKind.MaskImage : VideoMaskKind.MaskVideo;
                }
                else
                {
                    FillMaskByConvention(spec, files);
                }

                spec.Loop = ParseLoop(entry.playback, spec.Role);
                spec.FacesRight = string.Equals(Clean(entry.facing), "right", StringComparison.OrdinalIgnoreCase);
                spec.Scale = entry.scale > 0f && IsNumber(entry.scale) ? Mathf.Clamp(entry.scale, MinScale, MaxScale) : 1f;
                spec.OffsetX = IsNumber(entry.offsetX) ? Mathf.Clamp(entry.offsetX, -MaxClipOffset, MaxClipOffset) : 0f;
                spec.OffsetY = IsNumber(entry.offsetY) ? Mathf.Clamp(entry.offsetY, -MaxClipOffset, MaxClipOffset) : 0f;
                spec.Speed = entry.speed > 0f && IsNumber(entry.speed) ? Mathf.Min(entry.speed, MaxWalkSpeed) : DefaultWalkSpeed;
                spec.MoveStart = entry.moveStart > 0f && IsNumber(entry.moveStart) ? Mathf.Min(entry.moveStart, MaxMoveSeconds) : 0f;
                spec.MoveEnd = entry.moveEnd > 0f && IsNumber(entry.moveEnd) ? Mathf.Min(entry.moveEnd, MaxMoveSeconds) : 0f;
                spec.MoveRamp = entry.moveRamp > 0f && IsNumber(entry.moveRamp) ? Mathf.Min(entry.moveRamp, MaxMoveRamp) : DefaultMoveRamp;
                spec.ThenIdle = string.Equals(Clean(entry.after), "idle", StringComparison.OrdinalIgnoreCase);
                // On a reaction, "after" names the pose that follows it. "idle" is the default anyway.
                if (spec.Role == VideoClipRole.Reaction && !spec.ThenIdle)
                    spec.After = Clean(entry.after);
                spec.Sound = entry.sound;
                spec.HoldSeconds = entry.hold > 0f && IsNumber(entry.hold) ? Mathf.Min(entry.hold, MaxHoldSeconds) : DefaultHoldSeconds;
                spec.Weight = entry.weight > 0f && IsNumber(entry.weight) ? entry.weight : 1f;
                Add(set, spec);
            }

            PromoteIdle(set);
            CheckPoses(set);
            return set;
        }

        /// <summary>A reaction whose "after" names no usable looping reaction falls back to the default idle, with a warning.</summary>
        static void CheckPoses(VideoAvatarClipSet set)
        {
            for (var i = 0; i < set.Clips.Count; i++)
            {
                var spec = set.Clips[i];
                if (spec.Role != VideoClipRole.Reaction || string.IsNullOrEmpty(spec.After))
                    continue;

                var pose = set.FindReaction(spec.After);
                if (pose < 0 || pose == i || !set.Clips[pose].Loop)
                {
                    set.Warnings.Add("Clip \"" + spec.Name + "\" is followed by \"" + spec.After
                        + "\", which is not a looping reaction clip in the folder. She returns to the default idle instead.");
                    spec.After = string.Empty;
                }
            }
        }

        /// <summary>
        /// No manifest: every video in the folder that is not a mask is a clip (a packed
        /// "name.sbs.mp4" in preference to a "name.mp4" with a separate mask), and its role
        /// comes from its name. "idle" is the default idle. "idle-2" or "idle_look" is a
        /// variation. "idle-side" is the side idle. A name starting with "walk" is the walk,
        /// unless it also contains "stop". Any other name is a reaction.
        /// </summary>
        static VideoAvatarClipSet FromFileNames(IList<string> files)
        {
            var set = new VideoAvatarClipSet();
            var names = new List<string>();
            for (var i = 0; i < files.Count; i++)
            {
                var fileName = VideoAvatarFiles.FileName(files[i]);
                if (!HasExtension(fileName, VideoExtensions))
                    continue;

                var stem = Stem(fileName);
                if (IsMaskStem(stem))
                    continue;
                names.Add(fileName);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < names.Count; i++)
            {
                var stem = Stem(names[i]);
                var packed = stem.EndsWith(PackedSuffix, StringComparison.OrdinalIgnoreCase);
                var name = packed ? stem.Substring(0, stem.Length - PackedSuffix.Length) : stem;
                if (name.Length == 0 || IndexOfName(set, name) >= 0)
                    continue;
                // With both forms in the folder the packed clip is used. One decoder makes
                // its picture and its mask, so the outline cannot slip against her.
                if (!packed && FindWithExtensions(files, stem + PackedSuffix, VideoExtensions) != null)
                    continue;

                var spec = new VideoClipSpec { Name = name, ColorPath = FindFile(files, names[i]) };
                spec.Role = ParseRole(null, name);
                spec.Loop = ParseLoop(null, spec.Role);
                if (packed)
                    spec.Mask = VideoMaskKind.Packed;
                else
                    FillMaskByConvention(spec, files);
                Add(set, spec);
            }

            PromoteIdle(set);
            return set;
        }

        /// <summary>
        /// With idle variations but no default idle, the first variation becomes the default,
        /// so a folder that only has "idle-1", "idle-2", "idle-3" still plays.
        /// </summary>
        static void PromoteIdle(VideoAvatarClipSet set)
        {
            if (set.Idle >= 0 || set.Variations.Count == 0)
                return;

            var index = set.Variations[0];
            set.Variations.RemoveAt(0);
            set.Idle = index;
            set.Clips[index].Role = VideoClipRole.Idle;
            set.Clips[index].Loop = true;
            set.Warnings.Add("No clip is the default idle. \"" + set.Clips[index].Name + "\" is used as the default.");
        }

        static void Add(VideoAvatarClipSet set, VideoClipSpec spec)
        {
            if (IndexOfName(set, spec.Name) >= 0)
            {
                set.Warnings.Add("Clip name \"" + spec.Name + "\" is used twice. Only the first one is used.");
                return;
            }

            var index = set.Clips.Count;
            switch (spec.Role)
            {
                case VideoClipRole.Idle:
                    if (set.Idle >= 0)
                    {
                        // A second default idle is still useful: play it as a variation.
                        spec.Role = VideoClipRole.IdleVariation;
                        spec.Loop = false;
                        set.Variations.Add(index);
                    }
                    else
                    {
                        set.Idle = index;
                    }

                    break;
                case VideoClipRole.IdleVariation:
                    set.Variations.Add(index);
                    break;
                case VideoClipRole.Walk:
                    if (set.Walk >= 0)
                    {
                        set.Warnings.Add("More than one walk clip. \"" + spec.Name + "\" was left out.");
                        return;
                    }

                    set.Walk = index;
                    break;
                case VideoClipRole.WalkStop:
                    if (set.WalkStop >= 0)
                    {
                        set.Warnings.Add("More than one walk-stop clip. \"" + spec.Name + "\" was left out.");
                        return;
                    }

                    set.WalkStop = index;
                    break;
                case VideoClipRole.SideIdle:
                    if (set.SideIdle >= 0)
                    {
                        set.Warnings.Add("More than one side idle clip. \"" + spec.Name + "\" was left out.");
                        return;
                    }

                    set.SideIdle = index;
                    break;
            }

            set.Clips.Add(spec);
        }

        static void FillMaskByConvention(VideoClipSpec spec, IList<string> files)
        {
            var colorName = VideoAvatarFiles.FileName(spec.ColorPath);
            var stem = Stem(colorName);
            if (stem.EndsWith(PackedSuffix, StringComparison.OrdinalIgnoreCase))
            {
                spec.Mask = VideoMaskKind.Packed;
                return;
            }

            for (var s = 0; s < MaskSuffixes.Length; s++)
            {
                var video = FindWithExtensions(files, stem + MaskSuffixes[s], VideoExtensions);
                if (video != null)
                {
                    spec.MaskPath = video;
                    spec.Mask = VideoMaskKind.MaskVideo;
                    return;
                }
            }

            for (var s = 0; s < MaskSuffixes.Length; s++)
            {
                var image = FindWithExtensions(files, stem + MaskSuffixes[s], ImageExtensions);
                if (image != null)
                {
                    spec.MaskPath = image;
                    spec.Mask = VideoMaskKind.MaskImage;
                    return;
                }
            }

            spec.Mask = VideoMaskKind.VideoAlpha;
        }

        public static VideoClipRole ParseRole(string role, string name)
        {
            var text = Normal(role);
            switch (text)
            {
                case "idle":
                    return VideoClipRole.Idle;
                case "idlevariation":
                case "variation":
                    return VideoClipRole.IdleVariation;
                case "walk":
                    return VideoClipRole.Walk;
                case "walkstop":
                case "stop":
                    return VideoClipRole.WalkStop;
                case "sideidle":
                    return VideoClipRole.SideIdle;
                case "reaction":
                    return VideoClipRole.Reaction;
            }

            // No role given, or an unknown word: go by the clip name.
            var plain = Normal(name);
            if (plain == "idle")
                return VideoClipRole.Idle;
            if (plain.StartsWith("idle", StringComparison.Ordinal))
                return plain.IndexOf("side", StringComparison.Ordinal) >= 0 ? VideoClipRole.SideIdle : VideoClipRole.IdleVariation;
            if (plain.StartsWith("walk", StringComparison.Ordinal))
            {
                // "walk_pivot180", "walk_turn": a change of direction, not the walk loop.
                // It is registered and not played yet.
                if (plain.IndexOf("pivot", StringComparison.Ordinal) >= 0 || plain.IndexOf("turn", StringComparison.Ordinal) >= 0)
                    return VideoClipRole.Reaction;
                return plain.IndexOf("stop", StringComparison.Ordinal) >= 0 ? VideoClipRole.WalkStop : VideoClipRole.Walk;
            }
            if (plain.StartsWith("stop", StringComparison.Ordinal))
                return VideoClipRole.WalkStop;
            return VideoClipRole.Reaction;
        }

        /// <summary>"loop" or "once" wins. Otherwise the idle, the walk, and the side idle loop, and everything else plays once.</summary>
        public static bool ParseLoop(string playback, VideoClipRole role)
        {
            var text = Normal(playback);
            if (text == "loop")
                return true;
            if (text == "once")
                return false;
            return role == VideoClipRole.Idle || role == VideoClipRole.Walk || role == VideoClipRole.SideIdle;
        }

        static void ApplyVariationTiming(VideoAvatarClipSet set, float min, float max)
        {
            if (min > 0f && IsNumber(min))
                set.VariationMinSeconds = min;
            if (max > 0f && IsNumber(max))
                set.VariationMaxSeconds = max;
            if (set.VariationMaxSeconds < set.VariationMinSeconds)
                set.VariationMaxSeconds = set.VariationMinSeconds;
        }

        static int IndexOfName(VideoAvatarClipSet set, string name)
        {
            for (var i = 0; i < set.Clips.Count; i++)
            {
                if (string.Equals(set.Clips[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        static bool IsMaskStem(string stem)
        {
            for (var s = 0; s < MaskSuffixes.Length; s++)
            {
                if (stem.EndsWith(MaskSuffixes[s], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string FindFile(IList<string> files, string fileName)
        {
            for (var i = 0; i < files.Count; i++)
            {
                if (string.Equals(VideoAvatarFiles.FileName(files[i]), fileName, StringComparison.OrdinalIgnoreCase))
                    return files[i];
            }

            return null;
        }

        static string FindWithExtensions(IList<string> files, string stem, string[] extensions)
        {
            for (var e = 0; e < extensions.Length; e++)
            {
                var found = FindFile(files, stem + extensions[e]);
                if (found != null)
                    return found;
            }

            return null;
        }

        static bool HasExtension(string fileName, string[] extensions)
        {
            for (var e = 0; e < extensions.Length; e++)
            {
                if (fileName.EndsWith(extensions[e], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>The file name without its last extension: "idle.mask.mp4" gives "idle.mask".</summary>
        static string Stem(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;
            var dot = fileName.LastIndexOf('.');
            return dot <= 0 ? fileName : fileName.Substring(0, dot);
        }

        static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>Lower case with spaces, dashes, and underscores removed, so "walk-stop", "Walk_Stop" and "walkStop" are equal.</summary>
        static string Normal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return value.Trim().ToLowerInvariant().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
        }

        static bool IsNumber(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
