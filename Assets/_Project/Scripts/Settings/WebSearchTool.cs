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
    /// is a public feed and returns current game-patch coverage without an API key. A search
    /// the user typed is cached and rate-limited. The game companion's patch lookup is a
    /// separate lane: it does not start that cooldown or replace that cached result.
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

        /// <summary>What one claim of the user-search slot decided. A background claim leaves the slot alone.</summary>
        public struct SearchSlot
        {
            public bool Refused;
            public string Reason;
            public WebSearchResponse Cached;
        }

        public static async System.Threading.Tasks.Task<WebSearchResponse> SearchAsync(string query)
        {
            return await RunSearch(query, false);
        }

        /// <summary>
        /// Claims the user-search slot, or does nothing when <paramref name="background"/> is true.
        /// A background patch lookup must not refuse the next <c>/search</c> or replace its cache.
        /// </summary>
        public static SearchSlot Claim(bool background, string query, DateTime nowUtc)
        {
            if (background)
                return new SearchSlot();

            if (_cached != null && string.Equals(_cachedQuery, query ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && (nowUtc - _cachedAtUtc).TotalSeconds < CacheSeconds)
            {
                return new SearchSlot
                {
                    Cached = new WebSearchResponse
                    {
                        Ok = _cached.Ok,
                        Error = _cached.Error,
                        Query = _cached.Query,
                        FromCache = true,
                        Results = new List<WebSearchResult>(_cached.Results),
                    },
                };
            }

            if ((nowUtc - _lastRequestUtc).TotalSeconds < CooldownSeconds)
            {
                return new SearchSlot
                {
                    Refused = true,
                    Reason = "Give the last web lookup a moment before searching again.",
                };
            }

            _lastRequestUtc = nowUtc;
            return new SearchSlot();
        }

        /// <summary>Stores a user-search result. A background result is not stored in that slot.</summary>
        public static void Remember(bool background, string query, WebSearchResponse response, DateTime nowUtc)
        {
            if (background || response == null)
                return;
            _cached = response;
            _cachedQuery = query ?? string.Empty;
            _cachedAtUtc = nowUtc;
        }

        /// <summary>Clears the user-search cooldown and cache. Tests call this.</summary>
        public static void ResetLookupSlot()
        {
            _cached = null;
            _cachedQuery = string.Empty;
            _cachedAtUtc = DateTime.MinValue;
            _lastRequestUtc = DateTime.MinValue;
        }

        static async System.Threading.Tasks.Task<WebSearchResponse> RunSearch(string query, bool background)
        {
            var cleaned = CleanQuery(query);
            if (cleaned.Length == 0)
                return Fail("Type something to search for.");

            var now = DateTime.UtcNow;
            var slot = Claim(background, cleaned, now);
            if (slot.Cached != null)
                return slot.Cached;
            if (slot.Refused)
                return Fail(slot.Reason);

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
                    Remember(background, cleaned, failure, DateTime.UtcNow);
                    return failure;
                }

                var response = Parse(cleaned, request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
                Remember(background, cleaned, response, DateTime.UtcNow);
                return response;
            }
        }

        public static System.Threading.Tasks.Task<WebSearchResponse> SearchForGameAsync(string gameName)
        {
            return RunSearch(GameQuery(gameName), true);
        }

        /// <summary>Permission-gated patch lookup for the game companion. It does not use the /search slot.</summary>
        public static void RequestForGame(SettingsPanel panel, string gameName, Action<WebSearchResponse> done)
        {
            if (panel == null)
            {
                done?.Invoke(Fail("Web search is unavailable without Settings."));
                return;
            }

            var query = GameQuery(gameName);
            DesktopToolPermissions.Request(panel, DesktopToolKind.Web, "search the web", allowed =>
            {
                if (!allowed)
                {
                    done?.Invoke(Fail("Web search is not allowed. Enable it in Settings > Senses > Tools."));
                    return;
                }

                RunSearch(query, true).ContinueWith(task =>
                {
                    var response = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result != null
                        ? task.Result
                        : Fail("The web search failed.");
                    done?.Invoke(response);
                }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            });
        }

        static string GameQuery(string gameName)
        {
            return (gameName ?? string.Empty).Trim() + " latest patch notes what changed meta";
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
