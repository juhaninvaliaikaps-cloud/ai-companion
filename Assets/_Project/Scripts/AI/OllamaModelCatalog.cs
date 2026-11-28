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
