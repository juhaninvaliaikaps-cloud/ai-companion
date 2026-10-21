using System;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Engine-agnostic description of a loaded humanoid: rig status, every blendshape
    /// name, and the expression presets those names map to.
    /// </summary>
    public sealed class AvatarDescriptor
    {
        public AvatarDescriptor(
            string modelName,
            bool isHumanoid,
            string humanoidMessage,
            IReadOnlyList<string> blendShapeNames,
            IReadOnlyDictionary<ExpressionPreset, IReadOnlyList<string>> expressions)
        {
            ModelName = modelName ?? string.Empty;
            IsHumanoid = isHumanoid;
            HumanoidMessage = humanoidMessage ?? string.Empty;
            BlendShapeNames = blendShapeNames ?? Array.Empty<string>();
            Expressions = expressions ?? new Dictionary<ExpressionPreset, IReadOnlyList<string>>();
        }

        public string ModelName { get; }
        public bool IsHumanoid { get; }
        public string HumanoidMessage { get; }
        public IReadOnlyList<string> BlendShapeNames { get; }
        public IReadOnlyDictionary<ExpressionPreset, IReadOnlyList<string>> Expressions { get; }

        public static AvatarDescriptor Create(string modelName, HumanoidReport rig, IEnumerable<string> blendShapeNames)
        {
            var names = CopyNames(blendShapeNames);
            var report = rig ?? HumanoidReport.Missing(HumanoidValidation.RequiredBoneNames);
            return new AvatarDescriptor(modelName, report.IsValid, report.Message, names, ExpressionPresetMap.Map(names));
        }

        public AvatarDescriptor WithExpressions(IEnumerable<KeyValuePair<ExpressionPreset, string>> extra)
        {
            var grouped = new Dictionary<ExpressionPreset, List<string>>();
            foreach (var pair in Expressions)
            {
                var copy = new List<string>(pair.Value.Count);
                for (var i = 0; i < pair.Value.Count; i++)
                    copy.Add(pair.Value[i]);
                grouped.Add(pair.Key, copy);
            }

            if (extra != null)
            {
                foreach (var pair in extra)
                {
                    if (pair.Key == ExpressionPreset.None || string.IsNullOrWhiteSpace(pair.Value))
                        continue;
                    if (!grouped.TryGetValue(pair.Key, out var list))
                    {
                        list = new List<string>();
                        grouped.Add(pair.Key, list);
                    }

                    var exists = false;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (string.Equals(list[i], pair.Value, StringComparison.Ordinal))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                        list.Add(pair.Value);
                }
            }

            var frozen = new Dictionary<ExpressionPreset, IReadOnlyList<string>>(grouped.Count);
            foreach (var pair in grouped)
                frozen.Add(pair.Key, pair.Value);

            return new AvatarDescriptor(ModelName, IsHumanoid, HumanoidMessage, BlendShapeNames, frozen);
        }

        public bool TryGetExpression(ExpressionPreset preset, out IReadOnlyList<string> blendShapeNames)
        {
            if (preset == ExpressionPreset.None)
            {
                blendShapeNames = Array.Empty<string>();
                return false;
            }

            return Expressions.TryGetValue(preset, out blendShapeNames);
        }

        public string Summary()
        {
            var builder = new StringBuilder();
            builder.Append(ModelName);
            builder.Append(IsHumanoid ? " humanoid=True" : " humanoid=False");
            builder.Append(" shapes=");
            builder.Append(BlendShapeNames.Count);
            AppendPreset(builder, ExpressionPreset.Aa);
            AppendPreset(builder, ExpressionPreset.Ih);
            AppendPreset(builder, ExpressionPreset.Ou);
            AppendPreset(builder, ExpressionPreset.Ee);
            AppendPreset(builder, ExpressionPreset.Oh);
            AppendPreset(builder, ExpressionPreset.Blink);
            AppendPreset(builder, ExpressionPreset.Joy);
            AppendPreset(builder, ExpressionPreset.Angry);
            AppendPreset(builder, ExpressionPreset.Sorrow);
            AppendPreset(builder, ExpressionPreset.Fun);
            AppendPreset(builder, ExpressionPreset.Surprised);
            return builder.ToString();
        }

        void AppendPreset(StringBuilder builder, ExpressionPreset preset)
        {
            if (!Expressions.TryGetValue(preset, out var names) || names.Count == 0)
                return;

            builder.Append(' ');
            builder.Append(preset.ToString().ToLowerInvariant());
            builder.Append('=');
            builder.Append(string.Join(",", names));
        }

        static IReadOnlyList<string> CopyNames(IEnumerable<string> blendShapeNames)
        {
            var names = new List<string>();
            if (blendShapeNames == null)
                return names;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in blendShapeNames)
            {
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                    continue;
                names.Add(name);
            }

            return names;
        }
    }
}
