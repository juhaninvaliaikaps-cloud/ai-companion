using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Names the blink shapes on a mesh. Classification accepts Blink, eyeBlink_L/R,
    /// EyeClose, and the VRM / VRoid names <see cref="ExpressionPresetMap"/> already maps.
    /// Names that do not match are ignored so a model with no blink shape is unchanged.
    /// </summary>
    public static class BlinkShapeSelection
    {
        static readonly string[] None = new string[0];

        public static bool IsBlinkName(string blendShapeName)
        {
            return ExpressionPresetMap.Classify(blendShapeName) == ExpressionPreset.Blink;
        }

        public static string[] Collect(IReadOnlyList<string> meshNames, IReadOnlyList<string> mappedBlinkNames)
        {
            var found = new List<string>();
            if (meshNames != null)
            {
                for (var i = 0; i < meshNames.Count; i++)
                {
                    var name = meshNames[i];
                    if (IsBlinkName(name))
                        Add(found, name);
                }
            }

            if (mappedBlinkNames != null)
            {
                for (var i = 0; i < mappedBlinkNames.Count; i++)
                    Add(found, mappedBlinkNames[i]);
            }

            return found.Count == 0 ? None : found.ToArray();
        }

        public static string FormatLog(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
                return "ProceduralLife: no blink blendshape, blinking skipped";

            var builder = new StringBuilder(names.Count == 1
                ? "ProceduralLife: blink blendshape "
                : "ProceduralLife: blink blendshapes ");
            builder.Append(names[0]);
            for (var i = 1; i < names.Count; i++)
            {
                builder.Append(", ");
                builder.Append(names[i]);
            }

            return builder.ToString();
        }

        static void Add(List<string> found, string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            for (var i = 0; i < found.Count; i++)
            {
                if (string.Equals(found[i], name, StringComparison.Ordinal))
                    return;
            }

            found.Add(name);
        }
    }
}
