using System;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-147: the gacha games. None has an official data feed, and all ban third-party tools
    /// that touch the game, so Happi only recognises the game window (process name or title)
    /// and, with consent, captures it the way OBS does. Nothing reads the game's memory or input:
    /// the process name comes from the same light query Task Manager and Discord use (JUH-161).
    /// </summary>
    public static class GachaGames
    {
        public static readonly GameTitle[] All =
        {
            GameTitle.GenshinImpact,
            GameTitle.HonkaiStarRail,
            GameTitle.ZenlessZoneZero,
            GameTitle.ArknightsEndfield,
            GameTitle.NevernessToEverness,
        };

        public static bool IsGacha(GameTitle game)
        {
            return game == GameTitle.GenshinImpact || game == GameTitle.HonkaiStarRail || game == GameTitle.ZenlessZoneZero
                   || game == GameTitle.ArknightsEndfield || game == GameTitle.NevernessToEverness;
        }

        /// <summary>
        /// Process names without ".exe", lower case. Genshin, Star Rail and ZZZ are well known;
        /// Endfield and NTE are best guesses, so the window title backs them up.
        /// </summary>
        static string[] Processes(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.GenshinImpact: return new[] { "genshinimpact", "yuanshen" };
                case GameTitle.HonkaiStarRail: return new[] { "starrail" };
                case GameTitle.ZenlessZoneZero: return new[] { "zenlesszonezero" };
                case GameTitle.ArknightsEndfield: return new[] { "endfield" };
                case GameTitle.NevernessToEverness: return new[] { "htgame", "nte" };
                default: return new string[0];
            }
        }

        /// <summary>Window titles, compared whole and case-insensitive (Global and CN names).</summary>
        static string[] Titles(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.GenshinImpact: return new[] { "Genshin Impact", "原神" };
                case GameTitle.HonkaiStarRail: return new[] { "Honkai: Star Rail", "崩坏：星穹铁道" };
                case GameTitle.ZenlessZoneZero: return new[] { "ZenlessZoneZero", "Zenless Zone Zero", "绝区零" };
                // JUH-161: the shipped window is titled just "Endfield".
                case GameTitle.ArknightsEndfield: return new[] { "Arknights: Endfield", "Arknights Endfield", "Endfield", "明日方舟：终末地" };
                case GameTitle.NevernessToEverness: return new[] { "Neverness to Everness", "NTE", "异环" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// Which gacha game a window belongs to, by its process name or its exact title, or
        /// <see cref="GameTitle.Unknown"/>. A browser tab or a wiki page titled "Genshin Impact
        /// tier list" does not match: titles must be exact, and the process name wins.
        /// </summary>
        public static GameTitle Match(string processName, string windowTitle)
        {
            var process = Normal(processName);
            if (process.EndsWith(".exe", StringComparison.Ordinal))
                process = process.Substring(0, process.Length - 4);
            var title = (windowTitle ?? string.Empty).Trim();
            for (var g = 0; g < All.Length; g++)
            {
                var names = Processes(All[g]);
                for (var i = 0; i < names.Length; i++)
                {
                    if (process == names[i])
                        return All[g];
                }
            }

            // JUH-161: a folder or a browser tab with the game's name is not the game.
            if (NotAGame(process))
                return GameTitle.Unknown;

            for (var g = 0; g < All.Length; g++)
            {
                var titles = Titles(All[g]);
                for (var i = 0; i < titles.Length; i++)
                {
                    if (string.Equals(title, titles[i], StringComparison.OrdinalIgnoreCase))
                        return All[g];
                }
            }

            return GameTitle.Unknown;
        }

        static string Normal(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Programs whose windows are titled after what they show: Explorer titles a window by its
        /// folder (the install folder is "Endfield"), browsers by the page.
        /// </summary>
        static readonly string[] NotGames =
        {
            "explorer", "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore",
            "discord", "steam", "steamwebhelper", "notepad", "notepad++", "code",
        };

        static bool NotAGame(string process)
        {
            if (process.Length == 0)
                return false;
            for (var i = 0; i < NotGames.Length; i++)
                if (process == NotGames[i])
                    return true;
            return false;
        }

        /// <summary>JUH-161: "C:\Games\Endfield\Endfield.exe" to "Endfield"; empty for nothing.</summary>
        public static string ExeName(string imagePath)
        {
            var path = (imagePath ?? string.Empty).Trim();
            if (path.Length == 0)
                return string.Empty;
            var slash = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            var name = slash >= 0 ? path.Substring(slash + 1) : path;
            var dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        /// <summary>
        /// JUH-161: whether the game counts as in front for a screen look. Either the game is the
        /// foreground window, or Happi's own overlay is (it sits on top, so clicking it takes the
        /// focus) while the game fills its monitor behind it. A full-screen game behind a browser
        /// the user switched to does not count.
        /// </summary>
        public static bool InFront(bool minimized, bool gameIsForeground, bool happiIsForeground, bool coversMonitor)
        {
            if (minimized)
                return false;
            return gameIsForeground || (happiIsForeground && coversMonitor);
        }
    }

    /// <summary>
    /// JUH-147: when she looks at a gacha game. Every <see cref="IntervalSeconds"/> while the
    /// game is in front (Jussi: every 2 minutes), and on the hotkey, but never twice within
    /// <see cref="MinGapSeconds"/>. Plain C#; the caller passes the time.
    /// </summary>
    public sealed class GachaLookSchedule
    {
        public const double IntervalSeconds = 120;
        public const double MinGapSeconds = 10;

        /// <summary>The first look after the game comes to the front waits this long, so a quick alt-tab is not a look.</summary>
        public const double SettleSeconds = 20;

        DateTime _lastLookUtc = DateTime.MinValue;
        DateTime _frontSinceUtc = DateTime.MinValue;
        bool _asked;

        /// <summary>The game window is in front now, or not.</summary>
        public void Front(bool inFront, DateTime nowUtc)
        {
            if (!inFront)
            {
                _frontSinceUtc = DateTime.MinValue;
                return;
            }

            if (_frontSinceUtc == DateTime.MinValue)
                _frontSinceUtc = nowUtc;
        }

        /// <summary>The hotkey: look as soon as the gap allows.</summary>
        public void AskNow()
        {
            _asked = true;
        }

        public bool Due(DateTime nowUtc, out bool asked)
        {
            asked = false;
            if (_frontSinceUtc == DateTime.MinValue)
            {
                _asked = false;
                return false;
            }

            var sinceLast = _lastLookUtc == DateTime.MinValue ? double.MaxValue : (nowUtc - _lastLookUtc).TotalSeconds;
            if (sinceLast < MinGapSeconds)
                return false;
            if (_asked)
            {
                asked = true;
                return true;
            }

            if ((nowUtc - _frontSinceUtc).TotalSeconds < SettleSeconds)
                return false;
            return sinceLast >= IntervalSeconds;
        }

        public void Looked(DateTime nowUtc)
        {
            _lastLookUtc = nowUtc;
            _asked = false;
        }
    }

    /// <summary>JUH-147: the words for a screen look at a gacha game.</summary>
    public static class GachaLookPrompt
    {
        public const string Silent = "SILENT";

        public const string Rules =
            "You can see the user's screen while they play. React to what they are doing right now in one or two short spoken sentences, " +
            "like a friend watching: a fight, a boss, exploring, a story scene, a menu, building their team. " +
            "Do not read out user IDs, names of other players, chat messages or anything personal on screen. " +
            "Do not give puzzle solutions or spoilers unless asked. If nothing is worth saying, reply with exactly " + Silent + ".";

        public static string Turn(GameTitle game, bool asked)
        {
            var name = CommentaryPrompt.GameName(game);
            return asked
                ? "The user asked you to look at their screen. They are playing " + name + ". Say your line."
                : "The user is playing " + name + ". This is their screen right now. Say your line, or " + Silent + ".";
        }

        /// <summary>True when the model chose to say nothing.</summary>
        public static bool IsSilent(string reply)
        {
            var text = GameCommentarySession.CleanReply(reply).Trim().TrimEnd('.', '!').Trim();
            return text.Length == 0 || string.Equals(text, Silent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
