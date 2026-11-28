using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// Pure helpers for the installed-model list from a local Ollama server. The settings UI
    /// fetches once per visit or on Refresh; the chat path never depends on discovery.
    /// </summary>
    public static class OllamaModelCatalog
    {
        /// <summary>Accept both http://host:11434 and http://host:11434/v1.</summary>
        public static string TagsUrl(string baseUrl)
        {
            var value = string.IsNullOrWhiteSpace(baseUrl) ? ChatDefaults.LocalBaseUrl : baseUrl.Trim();
            value = value.TrimEnd('/');
            if (value.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 3).TrimEnd('/');
            if (value.EndsWith("/api/tags", StringComparison.OrdinalIgnoreCase))
                return value;
            return value + "/api/tags";
        }

        /// <summary>Names in the server's order, without blank or duplicate entries.</summary>
        public static List<string> Parse(string json)
        {
            var names = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
                return names;
            OllamaTagsReply reply;
            try
            {
                reply = JsonUtility.FromJson<OllamaTagsReply>(json);
            }
            catch (ArgumentException)
            {
                return names;
            }

            if (reply == null || reply.models == null)
                return names;
            for (var i = 0; i < reply.models.Length; i++)
            {
                var entry = reply.models[i];
                if (entry == null)
                    continue;
                var name = (entry.name ?? entry.model ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;
                var seen = false;
                for (var j = 0; j < names.Count; j++)
                {
                    if (string.Equals(names[j], name, StringComparison.OrdinalIgnoreCase))
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    names.Add(name);
            }

            return names;
        }

        /// <summary>
        /// JUH-243: the same model, whether written with or without Ollama's default ":latest"
        /// tag ("happi-qwen35-9b" is "happi-qwen35-9b:latest"). Case doesn't matter.
        /// </summary>
        public static bool SameTag(string a, string b)
        {
            return string.Equals(Bare(a), Bare(b), StringComparison.OrdinalIgnoreCase);
        }

        static string Bare(string tag)
        {
            var value = (tag ?? string.Empty).Trim();
            return value.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - 7) : value;
        }

        /// <summary>True when Ollama listed this tag (with or without ":latest").</summary>
        public static bool IsInstalled(string tag, IReadOnlyList<string> installed)
        {
            if (string.IsNullOrWhiteSpace(tag) || installed == null)
                return false;
            for (var i = 0; i < installed.Count; i++)
            {
                if (SameTag(tag, installed[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// JUH-243: the dropdown's choices. "" (use the global model) first when
        /// <paramref name="withGlobal"/>, then the current choice when Ollama doesn't list it (so
        /// it stays visible, marked), then the installed models. The current choice is shown in
        /// the installed spelling, so it is marked as chosen.
        /// </summary>
        public static List<string> Options(string current, IReadOnlyList<string> installed, bool withGlobal)
        {
            var options = new List<string>();
            if (withGlobal)
                options.Add(string.Empty);
            var value = (current ?? string.Empty).Trim();
            if (value.Length > 0 && !IsInstalled(value, installed))
                options.Add(value);
            if (installed != null)
            {
                for (var i = 0; i < installed.Count; i++)
                {
                    var tag = (installed[i] ?? string.Empty).Trim();
                    if (tag.Length == 0)
                        continue;
                    var seen = false;
                    for (var j = 0; j < options.Count; j++)
                    {
                        if (options[j].Length > 0 && SameTag(options[j], tag))
                        {
                            seen = true;
                            break;
                        }
                    }

                    if (!seen)
                        options.Add(tag);
                }
            }

            return options;
        }

        /// <summary>The option that stands for <paramref name="current"/> (its installed spelling), or the value itself.</summary>
        public static string Selected(string current, IReadOnlyList<string> options)
        {
            var value = (current ?? string.Empty).Trim();
            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    if (value.Length == 0 ? options[i].Length == 0 : options[i].Length > 0 && SameTag(options[i], value))
                        return options[i];
                }
            }

            return value;
        }

        /// <summary>
        /// The text for an option: "Use the global model (tag)" for "", and "(not installed)" after
        /// a tag Ollama didn't list, once the list has loaded.
        /// </summary>
        public static string Label(string option, IReadOnlyList<string> installed, bool listLoaded, string globalModel)
        {
            if (string.IsNullOrEmpty(option))
                return "Use the global model" + (string.IsNullOrWhiteSpace(globalModel) ? string.Empty : " (" + globalModel.Trim() + ")");
            return listLoaded && !IsInstalled(option, installed) ? option + " (not installed)" : option;
        }

        /// <summary>
        /// JUH-243: the line under the global choice that says which local model is used now, and
        /// why when a persona overrides it. Empty when there is nothing to say.
        /// </summary>
        public static string InUse(string globalModel, string effectiveModel, string personaName, bool localRoute)
        {
            if (!localRoute || string.IsNullOrWhiteSpace(effectiveModel))
                return string.Empty;
            if (SameTag(globalModel, effectiveModel))
                return "In use now: " + effectiveModel.Trim() + ".";
            var who = string.IsNullOrWhiteSpace(personaName) ? "The active persona" : personaName.Trim();
            return who + " overrides this and uses " + effectiveModel.Trim() + ". Change it in AI \u2192 This persona.";
        }

        [Serializable]
        sealed class OllamaTagsReply
        {
            public OllamaTag[] models;
        }

        [Serializable]
        sealed class OllamaTag
        {
            public string name;
            public string model;
        }
    }
}
