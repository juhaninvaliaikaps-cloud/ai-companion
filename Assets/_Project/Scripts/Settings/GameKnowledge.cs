using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AICompanion.Core;
using AICompanion.GameCompanion;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-29 / JUH-160: public game background from sources the user named. The result is
    /// cached in memory and handed to the game context; it is not a memory and is not sent
    /// anywhere except the normal model prompt.
    /// </summary>
    public static class GameKnowledge
    {
        const int MaxSourceChars = 4200;
        const double CacheSeconds = 86400d;

        sealed class Entry
        {
            public string Text = string.Empty;
            public DateTime AtUtc;
        }

        static readonly Dictionary<GameTitle, Entry> Cache = new Dictionary<GameTitle, Entry>();
        static readonly Dictionary<GameTitle, bool> Running = new Dictionary<GameTitle, bool>();

        static readonly Regex Script = new Regex("<script.*?</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static readonly Regex Style = new Regex("<style.*?</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Singleline);
        static readonly Regex Space = new Regex("[ \t\r\f\v]+");
        static readonly Regex Blank = new Regex("\n{3,}");

        public static void Request(SettingsPanel panel, GameTitle game, Action<string> done)
        {
            if (game == GameTitle.Unknown)
            {
                done?.Invoke(string.Empty);
                return;
            }

            Entry cached;
            if (Cache.TryGetValue(game, out cached)
                && (DateTime.UtcNow - cached.AtUtc).TotalSeconds < CacheSeconds)
            {
                ApplyPrimer(game, cached.Text);
                done?.Invoke(cached.Text);
                return;
            }

            bool running;
            if (Running.TryGetValue(game, out running) && running)
                return;

            if (panel == null)
            {
                done?.Invoke(string.Empty);
                return;
            }

            DesktopToolPermissions.Request(panel, DesktopToolKind.Web, "read public game pages", allowed =>
            {
                if (!allowed)
                {
                    done?.Invoke(string.Empty);
                    return;
                }

                Running[game] = true;
                FetchAsync(game, text =>
                {
                    Running[game] = false;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Cache[game] = new Entry { Text = text, AtUtc = DateTime.UtcNow };
                        ApplyPrimer(game, text);
                    }
                    done?.Invoke(text ?? string.Empty);
                });
            });
        }

        /// <summary>True when <paramref name="game"/> is the title the companion is watching now.</summary>
        public static bool AppliesTo(GameTitle game)
        {
            if (game == GameTitle.Unknown)
                return false;
            return string.Equals(GameContextNow.Game, CommentaryPrompt.GameName(game), StringComparison.Ordinal);
        }

        /// <summary>
        /// Puts a cached or fetched page on the primer only while that game is still current.
        /// A page that arrives after a title switch is kept in the cache and not attached.
        /// </summary>
        public static bool ApplyPrimer(GameTitle game, string text)
        {
            if (string.IsNullOrEmpty(text) || !AppliesTo(game))
                return false;
            GameContextNow.SetPrimer(text);
            return true;
        }

        /// <summary>The same guard for the patch/meta section.</summary>
        public static bool ApplyPatch(GameTitle game, string text)
        {
            if (string.IsNullOrEmpty(text) || !AppliesTo(game))
                return false;
            GameContextNow.SetPatchContext(text);
            return true;
        }

        /// <summary>Remembers a page without fetching. Tests and a warm cache use this.</summary>
        public static void Remember(GameTitle game, string text)
        {
            if (game == GameTitle.Unknown)
                return;
            Cache[game] = new Entry { Text = text ?? string.Empty, AtUtc = DateTime.UtcNow };
        }

        /// <summary>Drops the in-memory page cache.</summary>
        public static void Forget()
        {
            Cache.Clear();
            Running.Clear();
        }

        static async void FetchAsync(GameTitle game, Action<string> done)
        {
            var sb = new StringBuilder();
            var urls = Urls(game);
            for (var i = 0; i < urls.Length; i++)
            {
                var text = await ReadPage(urls[i]);
                if (string.IsNullOrEmpty(text))
                    continue;
                sb.Append(i == 0 ? string.Empty : "\n\n");
                sb.Append("Source: ").Append(urls[i]).Append('\n');
                sb.Append(text);
                if (sb.Length > MaxSourceChars * 2)
                    break;
            }

            var value = sb.ToString().Trim();
            if (value.Length == 0)
                value = Fallback(game);
            if (value.Length > MaxSourceChars * 2)
                value = value.Substring(0, MaxSourceChars * 2).TrimEnd();
            done?.Invoke(value);
        }

        static string[] Urls(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.ArknightsEndfield:
                    return new[]
                    {
                        "https://game8.co/games/Arknights-Endfield/",
                        "https://endfield.hypergryph.com/",
                    };
                default:
                    return new string[0];
            }
        }

        static async System.Threading.Tasks.Task<string> ReadPage(string url)
        {
            try
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 20;
                    request.SetRequestHeader("User-Agent", "Happi/1.0 (desktop companion; public game reference)");
                    await request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                        return string.Empty;
                    return Extract(request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        static string Extract(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            var text = Script.Replace(html, " ");
            text = Style.Replace(text, " ");
            text = Tags.Replace(text, " ");
            text = WebUtility.HtmlDecode(text);
            text = Space.Replace(text, " ");
            text = Blank.Replace(text, "\n\n");
            text = text.Trim();
            if (text.Length > MaxSourceChars)
                text = text.Substring(0, MaxSourceChars).TrimEnd();
            return text;
        }

        static string Fallback(GameTitle game)
        {
            if (game == GameTitle.ArknightsEndfield)
            {
                return "Arknights: Endfield is a 3D role-playing game in the Arknights setting. " +
                    "The player is the Endministrator, leading a team of operators through exploration and combat. " +
                    "The game has a hub, open areas, squad-based combat, character progression and a gacha system. " +
                    "Use only what is visible in the current frame for commentary.";
            }
            return string.Empty;
        }
    }
}
