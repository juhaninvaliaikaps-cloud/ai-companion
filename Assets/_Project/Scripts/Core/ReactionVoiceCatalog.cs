using System;
using System.Collections.Generic;
using System.IO;

namespace AICompanion.Core
{
    /// <summary>
    /// Where a reaction-voice pack lives. The wav files are not in the repository.
    /// They sit beside <c>manifest.json</c> under the ProjectLeia folder, the same
    /// way portraits do. A missing folder loads nothing.
    /// </summary>
    public static class ReactionVoicePaths
    {
        public const string ProjectFolder = "ProjectLeia";
        public const string FolderName = "reaction-voices";
        public const string ManifestFileName = "manifest.json";

        public static string Expected(string applicationData, string persistentDataPath)
        {
            if (!string.IsNullOrEmpty(applicationData))
                return Path.Combine(applicationData, ProjectFolder, FolderName);
            if (string.IsNullOrEmpty(persistentDataPath))
                return null;
            return Path.Combine(persistentDataPath, ProjectFolder, FolderName);
        }

        /// <summary>The pack folder that exists, or null. The roaming folder wins over the save folder.</summary>
        public static string Resolve(string applicationData, string persistentDataPath, Func<string, bool> directoryExists)
        {
            if (directoryExists == null)
                return null;

            var primary = Expected(applicationData, null);
            if (!string.IsNullOrEmpty(primary) && directoryExists(primary))
                return primary;

            var fallback = string.IsNullOrEmpty(persistentDataPath)
                ? null
                : Path.Combine(persistentDataPath, ProjectFolder, FolderName);
            if (string.IsNullOrEmpty(fallback))
                return null;
            if (string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase))
                return null;
            if (directoryExists(fallback))
                return fallback;
            return null;
        }

        public static string Manifest(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            return Path.Combine(folder, ManifestFileName);
        }
    }

    /// <summary>
    /// The clips a pack can play, grouped by portrait expression. A wav that is not
    /// in the folder is left out. Picking is random, and skips the file just played
    /// when another variation exists.
    /// </summary>
    public sealed class ReactionVoiceCatalog
    {
        public static readonly ReactionVoiceCatalog Empty = new ReactionVoiceCatalog();

        readonly Dictionary<string, List<ReactionVoiceClip>> _byExpression =
            new Dictionary<string, List<ReactionVoiceClip>>(StringComparer.Ordinal);

        public int ClipCount { get; private set; }

        public bool TryFind(string expression, string id, out ReactionVoiceClip clip)
        {
            if (expression != null && _byExpression.TryGetValue(expression, out var variants))
            {
                for (var i = 0; i < variants.Count; i++)
                {
                    if (string.Equals(variants[i].Id, id, StringComparison.Ordinal))
                    {
                        clip = variants[i];
                        return true;
                    }
                }
            }

            clip = default;
            return false;
        }

        public static ReactionVoiceCatalog FromClips(IEnumerable<ReactionVoiceClip> clips, Func<string, bool> fileExists, List<string> missing)
        {
            var catalog = new ReactionVoiceCatalog();
            if (clips == null)
                return catalog;

            foreach (var clip in clips)
            {
                var expression = ReactionVoiceNames.Expression(clip.Expression);
                var file = ReactionVoiceNames.WavFile(clip.FileName);
                if (expression.Length == 0 || file.Length == 0)
                    continue;
                if (fileExists != null && !fileExists(file))
                {
                    if (missing != null)
                        missing.Add(file);
                    continue;
                }

                List<ReactionVoiceClip> list;
                if (!catalog._byExpression.TryGetValue(expression, out list))
                {
                    list = new List<ReactionVoiceClip>();
                    catalog._byExpression.Add(expression, list);
                }

                list.Add(new ReactionVoiceClip(clip.Id, clip.Label, expression, file, clip.DurationSeconds));
                catalog.ClipCount++;
            }

            return catalog;
        }

        /// <summary>
        /// Reads <c>manifest.json</c> in <paramref name="folder"/>. Any failure returns an empty
        /// catalog and a short warning. The warning names the file, never chat text.
        /// </summary>
        public static ReactionVoiceCatalog Load(string folder, out List<string> warnings)
        {
            warnings = new List<string>();
            if (string.IsNullOrEmpty(folder))
                return Empty;

            var manifest = ReactionVoicePaths.Manifest(folder);
            if (string.IsNullOrEmpty(manifest) || !File.Exists(manifest))
            {
                warnings.Add("Reaction voice: manifest.json is missing. Reactions stay quiet.");
                return Empty;
            }

            string json;
            try
            {
                json = File.ReadAllText(manifest);
            }
            catch (Exception ex)
            {
                warnings.Add("Reaction voice: manifest.json could not be read (" + ex.GetType().Name + ").");
                return Empty;
            }

            var parsed = ReactionVoiceManifest.Parse(json);
            if (parsed.Count == 0)
            {
                warnings.Add("Reaction voice: manifest has no playable rows.");
                return Empty;
            }

            var missing = new List<string>();
            var catalog = FromClips(parsed, name => FileIn(folder, name), missing);
            for (var i = 0; i < missing.Count && i < 24; i++)
                warnings.Add("Reaction voice: missing '" + missing[i] + "'.");
            if (catalog.ClipCount == 0)
                warnings.Add("Reaction voice: none of the wav files are in the pack folder.");
            return catalog;
        }

        static bool FileIn(string folder, string name)
        {
            try
            {
                return File.Exists(Path.Combine(folder, name));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Has(string expression)
        {
            var name = ReactionVoiceNames.Expression(expression);
            List<ReactionVoiceClip> list;
            return name.Length > 0 && _byExpression.TryGetValue(name, out list) && list.Count > 0;
        }

        /// <summary>
        /// A clip for <paramref name="expression"/>. <paramref name="next"/> returns an index
        /// in <c>[0, count)</c>. When more than one file exists, the previous file is skipped.
        /// </summary>
        public bool TryPick(string expression, string avoidFile, Func<int, int> next, out ReactionVoiceClip clip)
        {
            clip = default(ReactionVoiceClip);
            var name = ReactionVoiceNames.Expression(expression);
            List<ReactionVoiceClip> list;
            if (name.Length == 0 || !_byExpression.TryGetValue(name, out list) || list.Count == 0)
                return false;

            var skipping = list.Count > 1 && !string.IsNullOrEmpty(avoidFile);
            var choices = 0;
            if (skipping)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (!SameFile(list[i].FileName, avoidFile))
                        choices++;
                }
            }

            if (choices == 0)
            {
                skipping = false;
                choices = list.Count;
            }

            var index = PickIndex(next, choices);
            if (!skipping)
            {
                clip = list[index];
                return true;
            }

            var seen = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (SameFile(list[i].FileName, avoidFile))
                    continue;
                if (seen == index)
                {
                    clip = list[i];
                    return true;
                }

                seen++;
            }

            clip = list[0];
            return true;
        }

        static int PickIndex(Func<int, int> next, int count)
        {
            if (count <= 1)
                return 0;
            if (next == null)
                return 0;
            var index = next(count);
            if (index < 0 || index >= count)
                return 0;
            return index;
        }

        static bool SameFile(string file, string other)
        {
            return string.Equals(file, other, StringComparison.OrdinalIgnoreCase);
        }
    }
}
