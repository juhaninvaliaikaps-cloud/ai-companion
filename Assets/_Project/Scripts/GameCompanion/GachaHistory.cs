using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AICompanion.GameCompanion
{
    /// <summary>One pull from a gacha log page, newest first in the page.</summary>
    public struct GachaPull
    {
        public string Id;
        public string Name;
        public bool Top;
        public string Banner;
    }

    /// <summary>
    /// JUH-148: the history link the game's own browser caches when the user opens the pull
    /// history (Genshin Impact, Honkai: Star Rail, Zenless Zone Zero). It carries a short-lived,
    /// read-only authkey. It is only ever sent to HoYoverse's own gacha log host, never logged
    /// and never saved.
    /// </summary>
    public static class GachaHistoryLink
    {
        public const int MaxUrlLength = 4096;

        public static bool Supports(GameTitle game)
        {
            return game == GameTitle.GenshinImpact || game == GameTitle.HonkaiStarRail || game == GameTitle.ZenlessZoneZero;
        }

        /// <summary>The game_biz prefix in the link: hk4e (Genshin), hkrpg (Star Rail), nap (ZZZ).</summary>
        public static string Biz(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.GenshinImpact: return "hk4e";
                case GameTitle.HonkaiStarRail: return "hkrpg";
                case GameTitle.ZenlessZoneZero: return "nap";
                default: return null;
            }
        }

        /// <summary>The banners to poll: gacha_type (Genshin, Star Rail) or real_gacha_type (ZZZ).</summary>
        public static string[] Banners(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.GenshinImpact: return new[] { "301", "302", "200", "500" };
                case GameTitle.HonkaiStarRail: return new[] { "11", "12", "1" };
                case GameTitle.ZenlessZoneZero: return new[] { "2001", "3001", "1001", "5001" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// The newest history link for this game in the cache text, or null. A link must be
        /// https, carry an authkey and name this game (its game_biz or its API host).
        /// </summary>
        public static string FindLatest(string cacheText, GameTitle game)
        {
            var biz = Biz(game);
            if (string.IsNullOrEmpty(cacheText) || biz == null)
                return null;
            string latest = null;
            var at = 0;
            while (true)
            {
                var start = cacheText.IndexOf("https://", at, StringComparison.Ordinal);
                if (start < 0)
                    break;
                var end = start;
                while (end < cacheText.Length && end - start < MaxUrlLength && IsUrlChar(cacheText[end]))
                    end++;
                var url = cacheText.Substring(start, end - start);
                at = end > start ? end : start + 8;
                if (url.IndexOf("authkey=", StringComparison.Ordinal) < 0)
                    continue;
                if (url.IndexOf("game_biz=" + biz, StringComparison.Ordinal) < 0 && url.IndexOf("public-operation-" + biz, StringComparison.Ordinal) < 0)
                    continue;
                if (!IsHoyoHost(HostOf(url)))
                    continue;
                latest = url;
            }

            return latest;
        }

        static bool IsUrlChar(char c)
        {
            return c > ' ' && c < 127 && c != '"' && c != '\'' && c != '<' && c != '>' && c != '\\';
        }

        public static string HostOf(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.Ordinal))
                return string.Empty;
            var rest = url.Substring(8);
            var cut = rest.IndexOfAny(new[] { '/', '?', '#', ':' });
            return (cut < 0 ? rest : rest.Substring(0, cut)).ToLowerInvariant();
        }

        /// <summary>Only HoYoverse's own hosts ever see the authkey.</summary>
        public static bool IsHoyoHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                return false;
            return host.EndsWith(".hoyoverse.com", StringComparison.Ordinal) || host.EndsWith(".mihoyo.com", StringComparison.Ordinal);
        }

        /// <summary>The gacha log endpoint: the cached API link's own, or the game's default (global or CN).</summary>
        public static string Endpoint(GameTitle game, string link)
        {
            var host = HostOf(link);
            if (host.StartsWith("public-operation-", StringComparison.Ordinal) && IsHoyoHost(host))
            {
                var path = PathOf(link);
                if (path.EndsWith("/getGachaLog", StringComparison.Ordinal))
                    return "https://" + host + path;
            }

            var cn = (QueryValue(link, "game_biz") ?? string.Empty).EndsWith("_cn", StringComparison.Ordinal);
            switch (game)
            {
                case GameTitle.GenshinImpact:
                    return cn ? "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog"
                              : "https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog";
                case GameTitle.HonkaiStarRail:
                    return cn ? "https://public-operation-hkrpg.mihoyo.com/common/gacha_record/api/getGachaLog"
                              : "https://public-operation-hkrpg-sg.hoyoverse.com/common/gacha_record/api/getGachaLog";
                case GameTitle.ZenlessZoneZero:
                    return cn ? "https://public-operation-nap.mihoyo.com/common/gacha_record/api/getGachaLog"
                              : "https://public-operation-nap-sg.hoyoverse.com/common/gacha_record/api/getGachaLog";
                default:
                    return null;
            }
        }

        static readonly string[] KeptParams = { "authkey_ver", "sign_type", "auth_appid", "authkey", "game_biz", "region", "plat_type" };

        /// <summary>
        /// The request for one page of one banner: the link's auth parameters, the banner,
        /// English names, and the cursor. Null when the link is unusable.
        /// </summary>
        public static string PageUrl(GameTitle game, string link, string banner, string endId, int size)
        {
            var endpoint = Endpoint(game, link);
            if (endpoint == null || string.IsNullOrEmpty(QueryValue(link, "authkey")))
                return null;
            var sb = new StringBuilder(endpoint.Length + 1024);
            sb.Append(endpoint).Append('?');
            for (var i = 0; i < KeptParams.Length; i++)
            {
                var value = QueryValue(link, KeptParams[i]);
                if (value == null)
                    continue;
                sb.Append(KeptParams[i]).Append('=').Append(value).Append('&');
            }

            sb.Append("lang=en&");
            sb.Append(game == GameTitle.ZenlessZoneZero ? "real_gacha_type=" : "gacha_type=").Append(Uri.EscapeDataString(banner ?? string.Empty));
            sb.Append("&page=1&size=").Append(Math.Max(1, Math.Min(20, size)));
            sb.Append("&end_id=").Append(Uri.EscapeDataString(string.IsNullOrEmpty(endId) ? "0" : endId));
            return sb.ToString();
        }

        static string PathOf(string url)
        {
            var rest = url.Substring(8);
            var slash = rest.IndexOf('/');
            if (slash < 0)
                return "/";
            var path = rest.Substring(slash);
            var q = path.IndexOfAny(new[] { '?', '#' });
            return q < 0 ? path : path.Substring(0, q);
        }

        /// <summary>A query value as it appears (still escaped), or null.</summary>
        public static string QueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            var q = url.IndexOf('?');
            if (q < 0)
                return null;
            var hash = url.IndexOf('#', q);
            var query = hash < 0 ? url.Substring(q + 1) : url.Substring(q + 1, hash - q - 1);
            var parts = query.Split('&');
            for (var i = 0; i < parts.Length; i++)
            {
                var eq = parts[i].IndexOf('=');
                if (eq > 0 && parts[i].Substring(0, eq) == key)
                    return parts[i].Substring(eq + 1);
            }

            return null;
        }
    }

    /// <summary>One gacha log page from HoYoverse's API.</summary>
    public sealed class GachaLogPage
    {
        /// <summary>The API's retcode: 0 is fine, -101 means the authkey ran out, -110 too many requests.</summary>
        public int RetCode = -1;
        public string Message = string.Empty;
        public readonly List<GachaPull> Pulls = new List<GachaPull>();

        public const int AuthkeyTimeout = -101;
        public const int TooFrequent = -110;

        public bool Ok
        {
            get { return RetCode == 0; }
        }

        public static GachaLogPage Parse(string json, GameTitle game)
        {
            var page = new GachaLogPage();
            if (string.IsNullOrEmpty(json))
                return page;
            try
            {
                var raw = JsonUtility.FromJson<Raw>(json);
                if (raw == null)
                    return page;
                page.RetCode = raw.retcode;
                page.Message = raw.message ?? string.Empty;
                if (raw.data == null || raw.data.list == null)
                    return page;
                for (var i = 0; i < raw.data.list.Length; i++)
                {
                    var item = raw.data.list[i];
                    if (item == null || string.IsNullOrEmpty(item.id))
                        continue;
                    page.Pulls.Add(new GachaPull
                    {
                        Id = item.id,
                        Name = item.name ?? string.Empty,
                        Top = IsTop(game, item.rank_type),
                        Banner = item.gacha_type ?? string.Empty,
                    });
                }
            }
            catch (Exception)
            {
                page.RetCode = -1;
            }

            return page;
        }

        /// <summary>Top rarity: rank 5 in Genshin and Star Rail, rank 4 (S) in ZZZ.</summary>
        public static bool IsTop(GameTitle game, string rankType)
        {
            return rankType == (game == GameTitle.ZenlessZoneZero ? "4" : "5");
        }

#pragma warning disable 0649
        [Serializable] sealed class Raw { public int retcode = -1; public string message = string.Empty; public RawData data; }
        [Serializable] sealed class RawData { public RawItem[] list; }
        [Serializable] sealed class RawItem { public string id = string.Empty; public string name = string.Empty; public string rank_type = string.Empty; public string gacha_type = string.Empty; }
#pragma warning restore 0649
    }

    /// <summary>
    /// JUH-148: one banner's pulls. The first page sets the baseline and the pity (pulls since the
    /// last top-rarity result; unknown past the first page). After that, each page's new pulls
    /// become events: a top pull with its pity and name, or a batch of pulls without one.
    /// </summary>
    public sealed class GachaBannerTracker
    {
        string _lastId;
        int _pity;
        bool _pityKnown;

        public bool HasBaseline
        {
            get { return _lastId != null; }
        }

        public int Pity
        {
            get { return _pity; }
        }

        public bool PityKnown
        {
            get { return _pityKnown; }
        }

        /// <summary>Feeds one page (newest first). Returns the events for pulls newer than the last page.</summary>
        public List<GameEvent> Read(GameTitle game, GachaLogPage page, DateTime nowUtc)
        {
            var events = new List<GameEvent>();
            if (page == null || !page.Ok)
                return events;

            if (_lastId == null)
            {
                _lastId = page.Pulls.Count > 0 ? page.Pulls[0].Id : string.Empty;
                _pity = 0;
                _pityKnown = false;
                for (var i = 0; i < page.Pulls.Count; i++)
                {
                    if (page.Pulls[i].Top)
                    {
                        _pityKnown = true;
                        break;
                    }

                    _pity++;
                }

                return events;
            }

            // The new pulls, oldest first.
            var fresh = new List<GachaPull>();
            for (var i = page.Pulls.Count - 1; i >= 0; i--)
            {
                if (Newer(page.Pulls[i].Id, _lastId))
                    fresh.Add(page.Pulls[i]);
            }

            if (fresh.Count == 0)
                return events;
            _lastId = fresh[fresh.Count - 1].Id;

            var withoutTop = 0;
            for (var i = 0; i < fresh.Count; i++)
            {
                _pity++;
                if (!fresh[i].Top)
                {
                    withoutTop++;
                    continue;
                }

                events.Add(GameEvent.Of(game, GameEventKind.GachaTopPull, nowUtc, _pityKnown ? _pity : 0, fresh[i].Name));
                _pity = 0;
                _pityKnown = true;
                withoutTop = 0;
            }

            if (withoutTop > 0 && events.Count == 0)
                events.Add(GameEvent.Of(game, GameEventKind.GachaNoTop, nowUtc, withoutTop));
            return events;
        }

        /// <summary>HoYoverse ids are growing decimal strings: longer is newer, then by text.</summary>
        public static bool Newer(string id, string than)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (string.IsNullOrEmpty(than))
                return true;
            if (id.Length != than.Length)
                return id.Length > than.Length;
            return string.CompareOrdinal(id, than) > 0;
        }
    }
}
