using System;
using System.Collections.Generic;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Maps blendshape names onto <see cref="ExpressionPreset"/>.
    /// Accepts VRM 1.0 presets, VRM 0.x / VRoid names, and the 52 ARKit names.
    /// </summary>
    public static class ExpressionPresetMap
    {
        static readonly Dictionary<string, ExpressionPreset> Exact = BuildExact();

        public static ExpressionPreset Classify(string blendShapeName)
        {
            var normalized = Normalize(blendShapeName);
            if (normalized.Length == 0)
                return ExpressionPreset.None;

            if (Exact.TryGetValue(normalized, out var preset))
                return preset;

            if (normalized.StartsWith("vrm", StringComparison.Ordinal) && normalized.Length > 3)
            {
                var rest = normalized.Substring(3);
                if (Exact.TryGetValue(rest, out preset))
                    return preset;
            }

            if (normalized.StartsWith("jawopen", StringComparison.Ordinal))
                return ExpressionPreset.Aa;
            if (normalized.StartsWith("mouthfunnel", StringComparison.Ordinal))
                return ExpressionPreset.Oh;
            if (normalized.StartsWith("mouthpucker", StringComparison.Ordinal))
                return ExpressionPreset.Ou;
            if (normalized.StartsWith("mouthclose", StringComparison.Ordinal))
                return ExpressionPreset.Ih;
            if (normalized.StartsWith("mouthstretch", StringComparison.Ordinal))
                return ExpressionPreset.Ee;
            if (normalized.StartsWith("eyeblink", StringComparison.Ordinal) || normalized.StartsWith("blink", StringComparison.Ordinal))
                return ExpressionPreset.Blink;
            if (normalized.StartsWith("mouthsmile", StringComparison.Ordinal))
                return ExpressionPreset.Joy;
            if (normalized.StartsWith("browdown", StringComparison.Ordinal))
                return ExpressionPreset.Angry;
            if (normalized.StartsWith("mouthfrown", StringComparison.Ordinal))
                return ExpressionPreset.Sorrow;
            if (normalized.StartsWith("browinnerup", StringComparison.Ordinal)
                || normalized.StartsWith("browouterup", StringComparison.Ordinal)
                || normalized.StartsWith("eyewide", StringComparison.Ordinal))
                return ExpressionPreset.Surprised;

            return ExpressionPreset.None;
        }

        public static IReadOnlyDictionary<ExpressionPreset, IReadOnlyList<string>> Map(IEnumerable<string> blendShapeNames)
        {
            var grouped = new Dictionary<ExpressionPreset, List<string>>();
            if (blendShapeNames == null)
                return Freeze(grouped);

            foreach (var name in blendShapeNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var preset = Classify(name);
                if (preset == ExpressionPreset.None)
                    continue;

                Add(grouped, preset, name);
            }

            return Freeze(grouped);
        }

        public static string Normalize(string blendShapeName)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName))
                return string.Empty;

            var source = blendShapeName.Trim();
            var slash = source.LastIndexOf('/');
            if (slash >= 0 && slash < source.Length - 1)
                source = source.Substring(slash + 1);

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

        static void Add(Dictionary<ExpressionPreset, List<string>> grouped, ExpressionPreset preset, string name)
        {
            if (!grouped.TryGetValue(preset, out var list))
            {
                list = new List<string>();
                grouped.Add(preset, list);
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], name, StringComparison.Ordinal))
                    return;
            }

            list.Add(name);
        }

        static IReadOnlyDictionary<ExpressionPreset, IReadOnlyList<string>> Freeze(Dictionary<ExpressionPreset, List<string>> grouped)
        {
            var frozen = new Dictionary<ExpressionPreset, IReadOnlyList<string>>(grouped.Count);
            foreach (var pair in grouped)
                frozen.Add(pair.Key, pair.Value);
            return frozen;
        }

        static Dictionary<string, ExpressionPreset> BuildExact()
        {
            var map = new Dictionary<string, ExpressionPreset>(StringComparer.Ordinal);
            Add(map, ExpressionPreset.Aa, "aa", "a", "vrmaa", "fclmtha", "jawopen", "mouthopen");
            Add(map, ExpressionPreset.Ih, "ih", "i", "vrmih", "fclmthi", "mouthclose");
            Add(map, ExpressionPreset.Ou, "ou", "u", "vrmou", "fclmthu", "mouthpucker");
            Add(map, ExpressionPreset.Ee, "ee", "e", "vrmee", "fclmthe");
            Add(map, ExpressionPreset.Oh, "oh", "o", "vrmoh", "fclmtho", "mouthfunnel");
            Add(map, ExpressionPreset.Blink, "blink", "blinkleft", "blinkright", "eyeblinkleft", "eyeblinkright", "fcleyeclose", "fcleyeclosel", "fcleyecloser");
            Add(map, ExpressionPreset.Joy, "joy", "happy", "fclalljoy", "mouthsmileleft", "mouthsmileright", "mouthsmile");
            Add(map, ExpressionPreset.Angry, "angry", "fclallangry", "browdownleft", "browdownright", "browdown");
            Add(map, ExpressionPreset.Sorrow, "sorrow", "sad", "fclallsorrow", "mouthfrownleft", "mouthfrownright", "mouthfrown");
            Add(map, ExpressionPreset.Fun, "fun", "relaxed", "fclallfun");
            Add(map, ExpressionPreset.Surprised, "surprised", "surprise", "fclallsurprised", "browinnerup", "browouterup", "browouterupleft", "browouterupright", "eyewide", "eyewideleft", "eyewideright");
            return map;
        }

        static void Add(Dictionary<string, ExpressionPreset> map, ExpressionPreset preset, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
                map[names[i]] = preset;
        }
    }
}
