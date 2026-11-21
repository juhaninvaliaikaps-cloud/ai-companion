using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-148: where the history link is cached. The game's Unity log (under AppData\LocalLow)
    /// names its data folder; the link is in that folder's web cache:
    /// <c>&lt;data&gt;\webCaches\&lt;version&gt;\Cache\Cache_Data\data_2</c> (older installs have
    /// no version folder). Files are only read, never changed.
    /// </summary>
    public static class GachaCacheFiles
    {
        /// <summary>The log files to try, Global then CN, under the user's LocalLow folder.</summary>
        public static List<string> LogFiles(GameTitle game, string localLow)
        {
            var files = new List<string>();
            if (string.IsNullOrEmpty(localLow))
                return files;
            switch (game)
            {
                case GameTitle.GenshinImpact:
                    files.Add(Path.Combine(Path.Combine(Path.Combine(localLow, "miHoYo"), "Genshin Impact"), "output_log.txt"));
                    files.Add(Path.Combine(Path.Combine(Path.Combine(localLow, "miHoYo"), "原神"), "output_log.txt"));
                    break;
                case GameTitle.HonkaiStarRail:
                    files.Add(Path.Combine(Path.Combine(Path.Combine(localLow, "Cognosphere"), "Star Rail"), "Player.log"));
                    files.Add(Path.Combine(Path.Combine(Path.Combine(localLow, "miHoYo"), "崩坏：星穹铁道"), "Player.log"));
                    break;
                case GameTitle.ZenlessZoneZero:
                    files.Add(Path.Combine(Path.Combine(Path.Combine(localLow, "miHoYo"), "ZenlessZoneZero"), "Player.log"));
                    files.Add(Path.Combine(Path.Combine(Path.Combine(localLow, "miHoYo"), "绝区零"), "Player.log"));
                    break;
            }

            return files;
        }

        static readonly Regex Genshin = new Regex(@"([A-Za-z]:[/\\][^\r\n""<>|?*]*?(GenshinImpact_Data|YuanShen_Data))", RegexOptions.CultureInvariant);
        static readonly Regex StarRail = new Regex(@"([A-Za-z]:[/\\][^\r\n""<>|?*]*?StarRail_Data)", RegexOptions.CultureInvariant);
        static readonly Regex Zzz = new Regex(@"([A-Za-z]:[/\\][^\r\n""<>|?*]*?ZenlessZoneZero_Data)", RegexOptions.CultureInvariant);

        /// <summary>The game's data folder named in its log, or null.</summary>
        public static string DataFolder(GameTitle game, string logText)
        {
            if (string.IsNullOrEmpty(logText))
                return null;
            Regex pattern;
            switch (game)
            {
                case GameTitle.GenshinImpact: pattern = Genshin; break;
                case GameTitle.HonkaiStarRail: pattern = StarRail; break;
                case GameTitle.ZenlessZoneZero: pattern = Zzz; break;
                default: return null;
            }

            var match = pattern.Match(logText);
            return match.Success ? match.Groups[1].Value.Replace('\\', '/') : null;
        }

        /// <summary>
        /// The cache files to read, newest version first: each <c>webCaches\&lt;version&gt;</c>
        /// folder (sorted by version number, highest first), then the old unversioned place.
        /// </summary>
        public static List<string> CacheFiles(string dataFolder, IList<string> versionFolders)
        {
            var files = new List<string>();
            if (string.IsNullOrEmpty(dataFolder))
                return files;
            var webCaches = Path.Combine(dataFolder, "webCaches");
            var versions = new List<string>(versionFolders ?? new string[0]);
            versions.Sort(CompareVersions);
            for (var i = versions.Count - 1; i >= 0; i--)
                files.Add(Data2(Path.Combine(webCaches, versions[i])));
            files.Add(Data2(webCaches));
            return files;
        }

        static string Data2(string root)
        {
            return Path.Combine(Path.Combine(Path.Combine(root, "Cache"), "Cache_Data"), "data_2");
        }

        /// <summary>"2.42.0.0" style folder names, by number; anything else sorts first.</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = (a ?? string.Empty).Split('.');
            var pb = (b ?? string.Empty).Split('.');
            for (var i = 0; i < pa.Length || i < pb.Length; i++)
            {
                int x;
                int y;
                if (i >= pa.Length || !int.TryParse(pa[i], out x))
                    x = -1;
                if (i >= pb.Length || !int.TryParse(pb[i], out y))
                    y = -1;
                if (x != y)
                    return x.CompareTo(y);
            }

            return 0;
        }
    }
}
