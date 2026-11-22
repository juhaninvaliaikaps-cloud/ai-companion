using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    public sealed class WebSearchResult
    {
        public string Title = string.Empty;
        public string Snippet = string.Empty;
        public string Url = string.Empty;
        public string Source = string.Empty;
        public string Published = string.Empty;
    }

    public sealed class WebSearchResponse
    {
        public bool Ok;
        public string Error = string.Empty;
        public string Query = string.Empty;
        public bool FromCache;
        public List<WebSearchResult> Results = new List<WebSearchResult>();
    }

    /// <summary>
    /// Courteous, keyless web context for a desktop tool. Google News RSS is used because it
    /// is a public feed and returns current game-patch coverage without an API key. Requests
    /// are cached and rate-limited; nothing runs in the background.
    /// </summary>
    public static class WebSearchTool
    {
        public const int MaxResults = 5;
        public const int MaxQueryLength = 240;
        public const float RequestTimeoutSeconds = 15f;
        public const double CacheSeconds = 600d;
        public const double CooldownSeconds = 8d;

        static readonly Regex ItemPattern = new Regex("<item>(.*?)</item>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        static readonly Regex TitlePattern = new Regex("<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        static readonly Regex LinkPattern = new Regex("<link>(.*?)</link>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        static readonly Regex DescriptionPattern = new Regex("<description>(.*?)</description>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        static readonly Regex SourcePattern = new Regex("<source\\s+url=\"([^\"]*)\"[^>]*>(.*?)</source>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        static readonly Regex TagPattern = new Regex("<[^>]+>", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        static string _cachedQuery = string.Empty;
        static WebSearchResponse _cached;
        static DateTime _cachedAtUtc;
        static DateTime _lastRequestUtc;

        public static async System.Threading.Tasks.Task<WebSearchResponse> SearchAsync(string query)
        {
            var cleaned = CleanQuery(query);
            if (cleaned.Length == 0)
                return Fail("Type something to search for.");

            if (_cached != null && string.Equals(_cachedQuery, cleaned, StringComparison.OrdinalIgnoreCase)
                && (DateTime.UtcNow - _cachedAtUtc).TotalSeconds < CacheSeconds)
            {
                return new WebSearchResponse
                {
                    Ok = _cached.Ok,
                    Error = _cached.Error,
                    Query = _cached.Query,
                    FromCache = true,
                    Results = new List<WebSearchResult>(_cached.Results),
                };
            }

            if ((DateTime.UtcNow - _lastRequestUtc).TotalSeconds < CooldownSeconds)
                return Fail("Give the last web lookup a moment before searching again.");

            _lastRequestUtc = DateTime.UtcNow;
            var url = "https://news.google.com/rss/search?q=" + Uri.EscapeDataString(cleaned)
                + "&hl=en-US&gl=US&ceid=US:en";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.CeilToInt(RequestTimeoutSeconds);
                request.SetRequestHeader("User-Agent", "Happi/1.0 (desktop companion; courteous search)");
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var failure = Fail("The web search could not be reached.");
                    _cached = failure;
                    _cachedQuery = cleaned;
                    _cachedAtUtc = DateTime.UtcNow;
                    return failure;
                }

                var response = Parse(cleaned, request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
                _cached = response;
                _cachedQuery = cleaned;
                _cachedAtUtc = DateTime.UtcNow;
                return response;
            }
        }

        public static System.Threading.Tasks.Task<WebSearchResponse> SearchForGameAsync(string gameName)
        {
            return SearchAsync((gameName ?? string.Empty).Trim() + " latest patch notes what changed meta");
        }

        /// <summary>Permission-gated game context for the game companion.</summary>
        public static void RequestForGame(SettingsPanel panel, string gameName, Action<WebSearchResponse> done)
        {
            Request(panel, (gameName ?? string.Empty).Trim() + " latest patch notes what changed meta", done);
        }

        public static void Request(SettingsPanel panel, string query, Action<WebSearchResponse> done)
        {
            if (panel == null)
            {
                done?.Invoke(Fail("Web search is unavailable without Settings."));
                return;
            }

            DesktopToolPermissions.Request(panel, DesktopToolKind.Web, "search the web", allowed =>
            {
                if (!allowed)
                {
                    done?.Invoke(Fail("Web search is not allowed. Enable it in Settings > Senses > Tools."));
                    return;
                }

                SearchAsync(query).ContinueWith(task =>
                {
                    var response = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result != null
                        ? task.Result
                        : Fail("The web search failed.");
                    done?.Invoke(response);
                }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            });
        }

        public static string BuildContext(WebSearchResponse response, int max = MaxResults)
        {
            if (response == null || !response.Ok || response.Results.Count == 0)
                return string.Empty;
            var sb = new StringBuilder();
            sb.Append("Current web context for \"").Append(response.Query).Append("\" (public search results; use only as background and cite sources):");
            var count = Math.Min(max, response.Results.Count);
            for (var i = 0; i < count; i++)
            {
                var result = response.Results[i];
                sb.Append("\n- ").Append(result.Title);
                if (!string.IsNullOrEmpty(result.Snippet))
                    sb.Append(": ").Append(result.Snippet);
                if (!string.IsNullOrEmpty(result.Url))
                    sb.Append(" (").Append(result.Url).Append(')');
            }

            return sb.ToString();
        }

        public static WebSearchResponse Parse(string query, string xml)
        {
            var response = new WebSearchResponse { Ok = true, Query = query };
            if (string.IsNullOrWhiteSpace(xml))
            {
                response.Ok = false;
                response.Error = "The web search returned no results.";
                return response;
            }

            var items = ItemPattern.Matches(xml);
            for (var i = 0; i < items.Count && response.Results.Count < MaxResults; i++)
            {
                var block = items[i].Groups[1].Value;
                var title = Decode(TitlePattern.Match(block).Groups[1].Value);
                var link = Decode(LinkPattern.Match(block).Groups[1].Value);
                var description = Decode(DescriptionPattern.Match(block).Groups[1].Value);
                var sourceMatch = SourcePattern.Match(block);
                var sourceName = Decode(sourceMatch.Groups[2].Value);
                var sourceUrl = Decode(sourceMatch.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                    continue;

                response.Results.Add(new WebSearchResult
                {
                    Title = title,
                    Snippet = description,
                    Url = string.IsNullOrWhiteSpace(sourceUrl) ? link : sourceUrl,
                    Source = sourceName,
                });
            }

            if (response.Results.Count == 0)
            {
                response.Ok = false;
                response.Error = "No current web results were found.";
            }

            return response;
        }

        static string CleanQuery(string query)
        {
            var text = (query ?? string.Empty).Trim();
            if (text.Length > MaxQueryLength)
                text = text.Substring(0, MaxQueryLength).TrimEnd();
            return text;
        }

        static string Decode(string value)
        {
            var decoded = WebUtility.HtmlDecode(value ?? string.Empty);
            decoded = TagPattern.Replace(decoded, string.Empty);
            return WebUtility.HtmlDecode(decoded).Trim();
        }

        static WebSearchResponse Fail(string message)
        {
            return new WebSearchResponse { Ok = false, Error = message ?? string.Empty };
        }
    }
}
