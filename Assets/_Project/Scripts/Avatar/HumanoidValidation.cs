using System;
using System.Collections.Generic;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Checks a bone-name list against the bones a Unity Humanoid needs
    /// before shared animations can retarget onto it.
    /// </summary>
    public static class HumanoidValidation
    {
        public const string RigHint = "Rig it as Humanoid (Mixamo, AccuRIG, or Blender) and reimport. See docs/AVATARS.md.";

        public static readonly string[] RequiredBoneNames =
        {
            "Hips",
            "Spine",
            "Head",
            "LeftUpperArm",
            "LeftLowerArm",
            "LeftHand",
            "RightUpperArm",
            "RightLowerArm",
            "RightHand",
            "LeftUpperLeg",
            "LeftLowerLeg",
            "LeftFoot",
            "RightUpperLeg",
            "RightLowerLeg",
            "RightFoot"
        };

        static readonly Dictionary<string, string> Aliases = BuildAliases();

        public static HumanoidReport ValidateBoneNames(IEnumerable<string> boneNames)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            if (boneNames != null)
            {
                foreach (var boneName in boneNames)
                {
                    if (TryCanonical(boneName, out var canonical))
                        present.Add(canonical);
                }
            }

            var missing = new List<string>();
            for (var i = 0; i < RequiredBoneNames.Length; i++)
            {
                if (!present.Contains(RequiredBoneNames[i]))
                    missing.Add(RequiredBoneNames[i]);
            }

            if (missing.Count == 0)
                return HumanoidReport.Valid();

            return HumanoidReport.Missing(missing);
        }

        public static bool TryCanonical(string boneName, out string canonical)
        {
            canonical = null;
            var normalized = Normalize(boneName);
            if (normalized.Length == 0)
                return false;

            if (normalized.StartsWith("mixamorig", StringComparison.Ordinal))
                normalized = normalized.Substring("mixamorig".Length);

            return Aliases.TryGetValue(normalized, out canonical);
        }

        public static string Normalize(string boneName)
        {
            if (string.IsNullOrWhiteSpace(boneName))
                return string.Empty;

            var source = boneName.Trim();
            var colon = source.LastIndexOf(':');
            if (colon >= 0 && colon < source.Length - 1)
                source = source.Substring(colon + 1);

            var chars = new char[source.Length];
            var count = 0;
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '_' || c == '-' || c == ' ' || c == '.')
                    continue;
                chars[count++] = char.ToLowerInvariant(c);
            }

            return new string(chars, 0, count);
        }

        static Dictionary<string, string> BuildAliases()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            Add(map, "Hips", "hips", "hip", "pelvis");
            Add(map, "Spine", "spine");
            Add(map, "Head", "head");
            Add(map, "LeftUpperArm", "leftupperarm", "leftarm");
            Add(map, "LeftLowerArm", "leftlowerarm", "leftforearm");
            Add(map, "LeftHand", "lefthand");
            Add(map, "RightUpperArm", "rightupperarm", "rightarm");
            Add(map, "RightLowerArm", "rightlowerarm", "rightforearm");
            Add(map, "RightHand", "righthand");
            Add(map, "LeftUpperLeg", "leftupperleg", "leftupleg", "leftthigh");
            Add(map, "LeftLowerLeg", "leftlowerleg", "leftleg", "leftshin");
            Add(map, "LeftFoot", "leftfoot");
            Add(map, "RightUpperLeg", "rightupperleg", "rightupleg", "rightthigh");
            Add(map, "RightLowerLeg", "rightlowerleg", "rightleg", "rightshin");
            Add(map, "RightFoot", "rightfoot");
            return map;
        }

        static void Add(Dictionary<string, string> map, string canonical, params string[] aliases)
        {
            for (var i = 0; i < aliases.Length; i++)
                map[aliases[i]] = canonical;
        }
    }

    public sealed class HumanoidReport
    {
        HumanoidReport(bool isValid, IReadOnlyList<string> missingRequired, string message)
        {
            IsValid = isValid;
            MissingRequired = missingRequired ?? Array.Empty<string>();
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public IReadOnlyList<string> MissingRequired { get; }
        public string Message { get; }

        public static HumanoidReport Valid()
        {
            return new HumanoidReport(true, Array.Empty<string>(), "Humanoid avatar is ready for shared animations.");
        }

        public static HumanoidReport Missing(IReadOnlyList<string> missingRequired)
        {
            var names = missingRequired == null ? Array.Empty<string>() : missingRequired;
            var listed = names.Count == 0 ? "the required humanoid set" : string.Join(", ", names);
            var message = "The model is not a Unity Humanoid avatar, so shared animations cannot retarget onto it. Missing required bones: "
                + listed
                + ". "
                + HumanoidValidation.RigHint;
            return new HumanoidReport(false, names, message);
        }

        public static HumanoidReport Unmapped()
        {
            return new HumanoidReport(
                false,
                Array.Empty<string>(),
                "The model is not a Unity Humanoid avatar, so shared animations cannot retarget onto it. Unity could not map the skeleton. "
                + HumanoidValidation.RigHint);
        }
    }
}
