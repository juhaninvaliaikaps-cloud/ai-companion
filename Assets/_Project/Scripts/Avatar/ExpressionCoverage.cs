using System.Collections.Generic;
using System.Text;
using AICompanion.Core;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Startup lines that say which expression channels actually found a blendshape.
    /// </summary>
    public static class ExpressionCoverage
    {
        public static string Format(Emotion emotion, FacePose pose, ICollection<ExpressionPreset> bound)
        {
            var needed = 0;
            var found = 0;
            var missing = new List<string>();
            for (var i = (int)ExpressionPreset.Aa; i <= (int)ExpressionPreset.Blush; i++)
            {
                var preset = (ExpressionPreset)i;
                if (pose.Get(preset) <= 0.001f)
                    continue;

                needed++;
                if (Covers(preset, bound))
                    found++;
                else
                    missing.Add(ChannelName(preset));
            }

            var line = "Expressions: " + emotion.ToString().ToLowerInvariant()
                + " -> " + found + "/" + needed + " shapes found";
            if (missing.Count == 0)
                return line;

            return line + ", missing " + string.Join(", ", missing);
        }

        public static string FormatBound(IEnumerable<KeyValuePair<ExpressionPreset, string>> shapes)
        {
            var grouped = new Dictionary<ExpressionPreset, List<string>>();
            if (shapes != null)
            {
                foreach (var pair in shapes)
                {
                    if (pair.Key == ExpressionPreset.None || string.IsNullOrEmpty(pair.Value))
                        continue;
                    if (!grouped.TryGetValue(pair.Key, out var list))
                    {
                        list = new List<string>();
                        grouped.Add(pair.Key, list);
                    }

                    var seen = false;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] == pair.Value)
                        {
                            seen = true;
                            break;
                        }
                    }

                    if (!seen)
                        list.Add(pair.Value);
                }
            }

            if (grouped.Count == 0)
                return "Expressions: bound none";

            var builder = new StringBuilder("Expressions: bound ");
            var first = true;
            for (var i = (int)ExpressionPreset.Aa; i <= (int)ExpressionPreset.Blush; i++)
            {
                var preset = (ExpressionPreset)i;
                if (!grouped.TryGetValue(preset, out var list))
                    continue;
                if (!first)
                    builder.Append("; ");
                first = false;
                builder.Append(ChannelName(preset));
                builder.Append('=');
                builder.Append(string.Join(", ", list));
            }

            return builder.ToString();
        }

        public static string FormatUnclassified(int count, IReadOnlyList<string> sample)
        {
            if (count <= 0)
                return "Expressions: unclassified 0";

            var builder = new StringBuilder();
            builder.Append("Expressions: unclassified ");
            builder.Append(count);
            if (sample != null && sample.Count > 0)
            {
                builder.Append(", sample ");
                builder.Append(string.Join(", ", sample));
            }

            return builder.ToString();
        }

        /// <summary>
        /// A channel counts when a blendshape is bound to it. Surprised also counts when
        /// wide eyes and raised brows can stand in for the missing whole-face shape.
        /// Fun counts when a squint can take it. Blush counts because the cheek overlay
        /// is the stand-in when the mesh has no blush shape. The separate Blush line
        /// still says which of those paths ran.
        /// </summary>
        public static bool Covers(ExpressionPreset preset, ICollection<ExpressionPreset> bound)
        {
            if (bound != null && bound.Contains(preset))
                return true;
            if (preset == ExpressionPreset.Blush)
                return true;
            if (preset == ExpressionPreset.Fun)
                return bound != null && bound.Contains(ExpressionPreset.EyeSquint);
            if (preset == ExpressionPreset.Surprised)
            {
                return bound != null
                    && bound.Contains(ExpressionPreset.EyeWide)
                    && bound.Contains(ExpressionPreset.BrowInnerUp)
                    && (bound.Contains(ExpressionPreset.BrowOuterUp) || bound.Contains(ExpressionPreset.BrowSide));
            }

            return false;
        }

        public static string ChannelName(ExpressionPreset preset)
        {
            var name = preset.ToString();
            if (name.Length == 0)
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
