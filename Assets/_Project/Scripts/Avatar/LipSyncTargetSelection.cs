using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Avatar
{
    public struct MouthShapeHit
    {
        public string Name;
        public ExpressionPreset Preset;
    }

    /// <summary>
    /// Picks VRM, named blendshapes, a jaw bone, or nothing. The scan is name-only so tests
    /// can run it without a mesh.
    /// </summary>
    public static class LipSyncTargetSelection
    {
        public static LipSyncTargetKind Choose(bool vrmMouth, int mouthBlendShapeCount, bool jawBone)
        {
            if (vrmMouth)
                return LipSyncTargetKind.Vrm;
            if (mouthBlendShapeCount > 0)
                return LipSyncTargetKind.BlendShapes;
            if (jawBone)
                return LipSyncTargetKind.JawBone;
            return LipSyncTargetKind.None;
        }

        public static bool DrivesSharedMouth(LipSyncTargetKind kind)
        {
            return kind == LipSyncTargetKind.Vrm
                || kind == LipSyncTargetKind.BlendShapes
                || kind == LipSyncTargetKind.Custom;
        }

        public static bool TryClassifyMouth(string blendShapeName, out ExpressionPreset preset)
        {
            preset = ExpressionPresetMap.Classify(blendShapeName);
            if (IsMouthPreset(preset))
                return true;

            preset = ExpressionPreset.None;
            var normalized = ExpressionPresetMap.Normalize(blendShapeName);
            if (normalized.Length == 0 || ContainsEmotion(normalized))
                return false;

            if (IsJawOpenName(normalized))
            {
                preset = ExpressionPreset.Aa;
                return true;
            }

            if (normalized.IndexOf("viseme", StringComparison.Ordinal) < 0
                && normalized.IndexOf("mouth", StringComparison.Ordinal) < 0)
                return false;

            if (HasToken(normalized, "ih") || EndsWith(normalized, 'i'))
                preset = ExpressionPreset.Ih;
            else if (HasToken(normalized, "ou") || HasToken(normalized, "uw") || EndsWith(normalized, 'u'))
                preset = ExpressionPreset.Ou;
            else if (HasToken(normalized, "ee") || EndsWith(normalized, 'e'))
                preset = ExpressionPreset.Ee;
            else if (HasToken(normalized, "oh") || HasToken(normalized, "ao") || EndsWith(normalized, 'o'))
                preset = ExpressionPreset.Oh;
            else if (HasToken(normalized, "aa") || HasToken(normalized, "open") || EndsWith(normalized, 'a'))
                preset = ExpressionPreset.Aa;
            else
                return false;

            return true;
        }

        public static bool IsJawBoneName(string transformName)
        {
            var normalized = ExpressionPresetMap.Normalize(transformName);
            if (normalized.Length == 0)
                return false;
            if (normalized.IndexOf("left", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("right", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("forward", StringComparison.Ordinal) >= 0)
                return false;

            return normalized == "jaw" || normalized.EndsWith("jaw", StringComparison.Ordinal) || normalized.StartsWith("jaw", StringComparison.Ordinal);
        }

        public static MouthShapeHit[] Collect(IEnumerable<string> blendShapeNames)
        {
            var found = new List<MouthShapeHit>();
            if (blendShapeNames == null)
                return new MouthShapeHit[0];

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in blendShapeNames)
            {
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;
                if (!TryClassifyMouth(name, out var preset))
                    continue;
                found.Add(new MouthShapeHit
                {
                    Name = name,
                    Preset = preset
                });
            }

            return found.ToArray();
        }

        public static string JoinNames(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(names[0]);
            for (var i = 1; i < names.Count; i++)
            {
                builder.Append(", ");
                builder.Append(names[i]);
            }

            return builder.ToString();
        }

        public static string VrmList(bool aa, bool ih, bool ou, bool ee, bool oh)
        {
            var builder = new StringBuilder();
            Append(builder, aa, "Aa");
            Append(builder, ih, "Ih");
            Append(builder, ou, "Ou");
            Append(builder, ee, "Ee");
            Append(builder, oh, "Oh");
            return builder.ToString();
        }

        public static string FormatDiagnostic(LipSyncTargetKind kind, string detail)
        {
            switch (kind)
            {
                case LipSyncTargetKind.Vrm:
                    return string.IsNullOrEmpty(detail) ? "LipSync: VRM" : "LipSync: VRM " + detail;
                case LipSyncTargetKind.BlendShapes:
                    return string.IsNullOrEmpty(detail) ? "LipSync: blendshapes" : "LipSync: blendshapes " + detail;
                case LipSyncTargetKind.JawBone:
                    return string.IsNullOrEmpty(detail) ? "LipSync: jaw bone" : "LipSync: jaw bone " + detail;
                case LipSyncTargetKind.Custom:
                    return string.IsNullOrEmpty(detail) ? "LipSync: custom" : "LipSync: " + detail;
                default:
                    return "LipSync: none";
            }
        }

        public static bool IsMouthPreset(ExpressionPreset preset)
        {
            return preset == ExpressionPreset.Aa
                || preset == ExpressionPreset.Ih
                || preset == ExpressionPreset.Ou
                || preset == ExpressionPreset.Ee
                || preset == ExpressionPreset.Oh;
        }

        static bool IsJawOpenName(string normalized)
        {
            if (normalized.IndexOf("left", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("right", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("forward", StringComparison.Ordinal) >= 0)
                return false;

            return normalized == "jaw" || normalized.EndsWith("jaw", StringComparison.Ordinal);
        }

        static bool ContainsEmotion(string normalized)
        {
            return normalized.IndexOf("smile", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("frown", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("dimple", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("press", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("shrug", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("blink", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("brow", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("cheek", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("nose", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("tongue", StringComparison.Ordinal) >= 0;
        }

        static bool HasToken(string normalized, string token)
        {
            return normalized.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        static bool EndsWith(string normalized, char vowel)
        {
            return normalized.Length > 0 && normalized[normalized.Length - 1] == vowel;
        }

        static void Append(StringBuilder builder, bool include, string name)
        {
            if (!include)
                return;
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(name);
        }
    }
}
