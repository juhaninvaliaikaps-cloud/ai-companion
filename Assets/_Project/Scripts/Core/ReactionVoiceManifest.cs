using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    /// <summary>One row of a reaction-voice pack. The file itself stays on disk.</summary>
    public readonly struct ReactionVoiceClip
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string Expression;
        public readonly string FileName;
        public readonly double DurationSeconds;

        public ReactionVoiceClip(string id, string label, string expression, string fileName, double durationSeconds)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Expression = expression ?? string.Empty;
            FileName = fileName ?? string.Empty;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// Reads a reaction-voice <c>manifest.json</c>. A broken file, a missing field, or a
    /// path that leaves the pack folder drops that row and never throws. An expression may
    /// be written directly, or taken from <c>portrait_file</c> (the stem of <c>teary_happy.png</c>
    /// is <c>teary_happy</c>). The root may be an array, one reaction object, or an object
    /// that holds the array.
    /// </summary>
    public static class ReactionVoiceManifest
    {
        public const int MaxRows = 256;

        public static List<ReactionVoiceClip> Parse(string json)
        {
            var clips = new List<ReactionVoiceClip>();
            if (string.IsNullOrWhiteSpace(json))
                return clips;
            if (!JsonTree.TryParse(json, out var root) || root == null)
                return clips;

            try
            {
                ReadRoot(root, clips);
            }
            catch (Exception)
            {
                clips.Clear();
            }

            return clips;
        }

        static void ReadRoot(object root, List<ReactionVoiceClip> clips)
        {
            var array = root as List<object>;
            if (array != null)
            {
                ReadArray(array, clips);
                return;
            }

            var obj = root as Dictionary<string, object>;
            if (obj == null)
                return;

            var nested = FindArray(obj);
            if (nested != null)
                ReadArray(nested, clips);
            if (clips.Count > 0)
                return;

            ReactionVoiceClip clip;
            if (TryReadRow(obj, out clip))
                clips.Add(clip);
        }

        static void ReadArray(List<object> array, List<ReactionVoiceClip> clips)
        {
            for (var i = 0; i < array.Count && clips.Count < MaxRows; i++)
            {
                var row = array[i] as Dictionary<string, object>;
                ReactionVoiceClip clip;
                if (row != null && TryReadRow(row, out clip))
                    clips.Add(clip);
            }
        }

        /// <summary>The first array of objects, preferring a key named reactions.</summary>
        static List<object> FindArray(Dictionary<string, object> obj)
        {
            List<object> fallback = null;
            foreach (var pair in obj)
            {
                var list = pair.Value as List<object>;
                if (list == null)
                    continue;
                if (string.Equals(pair.Key, "reactions", StringComparison.OrdinalIgnoreCase))
                    return list;
                if (fallback == null)
                    fallback = list;
            }

            return fallback;
        }

        static bool TryReadRow(Dictionary<string, object> row, out ReactionVoiceClip clip)
        {
            clip = default(ReactionVoiceClip);
            var expression = ReactionVoiceNames.Expression(FirstText(row, "expression", "face"));
            if (expression.Length == 0)
                expression = ReactionVoiceNames.Expression(FirstText(row, "portrait_file", "portraitFile", "portrait"));
            var file = ReactionVoiceNames.WavFile(FirstText(row, "wav", "file", "filename", "path"));
            if (expression.Length == 0 || file.Length == 0)
                return false;

            var id = CleanId(FirstText(row, "reaction", "id", "name"));
            if (id.Length == 0)
                id = ReactionVoiceNames.Expression(file);
            if (id.Length == 0)
                return false;

            var label = CleanId(FirstText(row, "label"));
            double duration;
            if (!FirstNumber(row, out duration, "duration_s", "duration", "durationSeconds"))
                duration = 0d;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0d)
                duration = 0d;

            clip = new ReactionVoiceClip(id, label, expression, file, duration);
            return true;
        }

        static string FirstText(Dictionary<string, object> row, params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                foreach (var pair in row)
                {
                    if (!string.Equals(pair.Key, keys[i], StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (pair.Value is string text)
                        return text;
                    if (pair.Value is double number)
                        return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return string.Empty;
        }

        static bool FirstNumber(Dictionary<string, object> row, out double number, params string[] keys)
        {
            number = 0d;
            for (var i = 0; i < keys.Length; i++)
            {
                foreach (var pair in row)
                {
                    if (!string.Equals(pair.Key, keys[i], StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (pair.Value is double value)
                    {
                        number = value;
                        return true;
                    }

                    if (pair.Value is string text
                        && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number))
                        return true;
                }
            }

            return false;
        }

        static string CleanId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 80)
                return string.Empty;
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (char.IsControl(trimmed[i]))
                    return string.Empty;
            }

            return trimmed;
        }
    }

    /// <summary>Shared cleaning for expression stems and wav file names.</summary>
    public static class ReactionVoiceNames
    {
        /// <summary>
        /// Portrait expression id. A file name such as <c>teary_happy.png</c> becomes
        /// <c>teary_happy</c>. A path with <c>..</c>, or any other character, is empty.
        /// </summary>
        public static string Expression(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Trim();
            if (trimmed.IndexOf("..", StringComparison.Ordinal) >= 0)
                return string.Empty;

            var slash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
            var name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
            var dot = name.LastIndexOf('.');
            var stem = dot > 0 ? name.Substring(0, dot) : name;
            stem = stem.Trim().ToLowerInvariant();
            if (stem.Length == 0 || stem.Length > 64)
                return string.Empty;
            for (var i = 0; i < stem.Length; i++)
            {
                var c = stem[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return string.Empty;
            }

            return stem;
        }

        /// <summary>A wav file name in the pack folder. A path, or anything that is not a wav, is empty.</summary>
        public static string WavFile(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Trim();
            if (trimmed.IndexOf("..", StringComparison.Ordinal) >= 0)
                return string.Empty;
            if (trimmed.IndexOf('/') >= 0 || trimmed.IndexOf('\\') >= 0)
                return string.Empty;
            if (trimmed.Length == 0 || trimmed.Length > 128)
                return string.Empty;
            if (trimmed.IndexOf('.') < 0)
                trimmed += ".wav";
            if (!trimmed.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            if (trimmed.StartsWith(".", StringComparison.Ordinal))
                return string.Empty;
            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok)
                    return string.Empty;
            }

            return trimmed;
        }

        /// <summary>
        /// The expression a cue just applied: the portrait name when the director set one,
        /// otherwise the emotion name. Neutral stays empty so it does not play a sound.
        /// </summary>
        public static string FromFace(string portrait, Emotion emotion)
        {
            var fromPortrait = Expression(portrait);
            if (fromPortrait.Length > 0 && fromPortrait != "neutral")
                return fromPortrait;
            if (emotion == Emotion.Neutral || !Enum.IsDefined(typeof(Emotion), emotion))
                return string.Empty;
            return Expression(emotion.ToString());
        }
    }
}
