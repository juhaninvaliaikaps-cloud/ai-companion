using System;
using System.Collections.Generic;
using System.IO;

namespace AICompanion.Settings
{
    /// <summary>
    /// The local Ollama models installed on this PC, read from Ollama's manifest folder.
    /// The settings dropdown uses this instead of a free-text model name, so a typo cannot
    /// silently point Private mode at a model that is not installed.
    /// </summary>
    public static class LocalModelCatalog
    {
        /// <summary>Installed model names, sorted, without duplicates.</summary>
        public static List<string> Installed()
        {
            var names = Scan(Path.Combine(ModelsRoot(), "manifests-v2"));
            Merge(names, Scan(Path.Combine(ModelsRoot(), "manifests")));
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>Reads a manifest folder directly. Used by the settings dropdown and its test.</summary>
        public static List<string> FromManifestFolder(string root)
        {
            return Scan(root);
        }

        /// <summary>The installed models plus the current value, so an existing setting is never hidden.</summary>
        public static List<string> Choices(string current)
        {
            var choices = Installed();
            Add(choices, current);
            return choices;
        }

        /// <summary>Choices from an explicit manifest folder. Used by tests.</summary>
        public static List<string> ChoicesFromFolder(string root, string current)
        {
            var choices = Scan(root);
            Add(choices, current);
            return choices;
        }

        static string ModelsRoot()
        {
            var configured = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models")
                : configured.Trim();
        }

        static List<string> Scan(string root)
        {
            var names = new List<string>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return names;

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    var parts = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    // registry.ollama.ai/library/llama3.2/3b -> llama3.2:3b
                    // Ollama 0.40 also stores the same layout under ollama.com/library.
                    if (parts.Length >= 4
                        && (string.Equals(parts[0], "registry.ollama.ai", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(parts[0], "ollama.com", StringComparison.OrdinalIgnoreCase))
                        && string.Equals(parts[1], "library", StringComparison.OrdinalIgnoreCase))
                    {
                        Add(names, parts[2] + ":" + parts[3]);
                        continue;
                    }

                    // hf.co/user/repo/Q4_K_M -> hf.co/user/repo:Q4_K_M
                    var name = string.Empty;
                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        if (name.Length > 0)
                            name += "/";
                        name += parts[i];
                    }

                    Add(names, name + ":" + parts[parts.Length - 1]);
                }
            }
            catch (IOException)
            {
                // A model folder that disappears while reading is not a settings error.
            }
            catch (UnauthorizedAccessException)
            {
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        static void Add(List<string> names, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            var value = name.Trim();
            foreach (var existing in names)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            names.Add(value);
        }

        static void Merge(List<string> into, List<string> from)
        {
            for (var i = 0; i < from.Count; i++)
                Add(into, from[i]);
        }
    }
}
