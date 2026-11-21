using System;
using System.Collections.Generic;
using System.IO;

namespace AICompanion.GameCompanion
{
    public enum GsiInstallStatus
    {
        Written = 0,
        AlreadyThere = 1,
        Removed = 2,
        NotInstalled = 3,
        GameNotFound = 4,
        Failed = 5,
        NotSupported = 6,
    }

    public struct GsiInstallResult
    {
        public GsiInstallStatus Status;
        public string Path;
        public string Message;

        public bool Ok
        {
            get { return Status == GsiInstallStatus.Written || Status == GsiInstallStatus.AlreadyThere || Status == GsiInstallStatus.Removed || Status == GsiInstallStatus.NotInstalled; }
        }
    }

    /// <summary>
    /// JUH-146 (Jussi, 2026-11-21: automatic): writes the game state integration file for CS2 and
    /// Dota 2 into the game's own cfg folder when the user presses Set up. It writes only
    /// <see cref="GsiConfig.FileName"/>, only inside the game's folder, and only on a button press.
    /// Remove deletes only our own file. Dota 2's <c>-gamestateintegration</c> launch option is
    /// left to the user: editing Steam's own config is fragile.
    /// </summary>
    public static class GsiInstaller
    {
        public const string Dota2LaunchOption = "-gamestateintegration";

        public const string Dota2LaunchHelp =
            "Dota 2 also needs a launch option: in Steam, right-click Dota 2, choose Properties, and add " +
            Dota2LaunchOption + " under Launch options.";

        public const string RestartHelp = "Restart the game if it is running.";

        /// <summary>The first line of our file. Remove checks for it before deleting anything.</summary>
        public const string Marker = "\"Happi game companion\"";

        public static bool Supports(GameTitle game)
        {
            return game == GameTitle.CounterStrike2 || game == GameTitle.Dota2;
        }

        /// <summary>The cfg folder under the game's install folder.</summary>
        public static string CfgFolder(GameTitle game, string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir))
                return null;
            switch (game)
            {
                case GameTitle.CounterStrike2:
                    return Join(gameDir, "game", "csgo", "cfg");
                case GameTitle.Dota2:
                    return Join(gameDir, "game", "dota", "cfg", "gamestate_integration");
                default:
                    return null;
            }
        }

        public static string FileText(GameTitle game, int port, string token)
        {
            return game == GameTitle.Dota2 ? GsiConfig.Dota2(port, token) : GsiConfig.Cs2(port, token);
        }

        /// <summary>Where our file is for this game, or null when the game isn't found.</summary>
        public static string TargetPath(GameTitle game, IList<string> steamRoots, IGameFiles files)
        {
            var gameDir = SteamLibraries.FindGame(game, steamRoots, files);
            var folder = CfgFolder(game, gameDir);
            return folder == null ? null : Path.Combine(folder, GsiConfig.FileName);
        }

        public static GsiInstallResult Install(GameTitle game, IList<string> steamRoots, int port, string token, IGameFiles files)
        {
            if (!Supports(game))
                return Result(GsiInstallStatus.NotSupported, null, CommentaryPrompt.GameName(game) + " has no config file to install.");
            if (string.IsNullOrEmpty(token))
                return Result(GsiInstallStatus.Failed, null, "There is no token for this install yet.");

            var gameDir = SteamLibraries.FindGame(game, steamRoots, files);
            if (gameDir == null)
                return Result(GsiInstallStatus.GameNotFound, null, CommentaryPrompt.GameName(game) + " wasn't found in your Steam libraries.");

            var folder = CfgFolder(game, gameDir);
            var path = Path.Combine(folder, GsiConfig.FileName);
            var text = FileText(game, port, token);
            try
            {
                if (game == GameTitle.Dota2)
                {
                    // Create only the last folder; the game's own cfg folder must already be there.
                    var parent = Join(gameDir, "game", "dota", "cfg");
                    if (!files.DirectoryExists(parent))
                        return Result(GsiInstallStatus.Failed, null, "Dota 2's cfg folder is missing. Verify the game files in Steam and try again.");
                    if (!files.DirectoryExists(folder))
                        files.CreateDirectory(folder);
                }
                else if (!files.DirectoryExists(folder))
                {
                    return Result(GsiInstallStatus.Failed, null, "CS2's cfg folder is missing. Verify the game files in Steam and try again.");
                }

                if (files.FileExists(path) && files.ReadAllText(path) == text)
                    return Result(GsiInstallStatus.AlreadyThere, path, Done(game, "is already set up."));

                files.WriteAllText(path, text);
                return Result(GsiInstallStatus.Written, path, Done(game, "is set up. " + RestartHelp));
            }
            catch (Exception ex)
            {
                return Result(GsiInstallStatus.Failed, path, "Couldn't write the config file: " + ex.Message);
            }
        }

        /// <summary>Deletes our file, and only when it is ours.</summary>
        public static GsiInstallResult Remove(GameTitle game, IList<string> steamRoots, IGameFiles files)
        {
            if (!Supports(game))
                return Result(GsiInstallStatus.NotSupported, null, CommentaryPrompt.GameName(game) + " has no config file.");
            var path = TargetPath(game, steamRoots, files);
            if (path == null)
                return Result(GsiInstallStatus.GameNotFound, null, CommentaryPrompt.GameName(game) + " wasn't found in your Steam libraries.");
            try
            {
                if (!files.FileExists(path))
                    return Result(GsiInstallStatus.NotInstalled, path, "There was nothing to remove.");
                var text = files.ReadAllText(path) ?? string.Empty;
                if (!text.TrimStart().StartsWith(Marker, StringComparison.Ordinal))
                    return Result(GsiInstallStatus.Failed, path, "That file wasn't written by Happi, so it was left alone.");
                files.DeleteFile(path);
                return Result(GsiInstallStatus.Removed, path, "The config file was removed. " + RestartHelp);
            }
            catch (Exception ex)
            {
                return Result(GsiInstallStatus.Failed, path, "Couldn't remove the config file: " + ex.Message);
            }
        }

        /// <summary>True when our file is there with this port and token.</summary>
        public static bool IsInstalled(GameTitle game, IList<string> steamRoots, int port, string token, IGameFiles files)
        {
            var path = Supports(game) ? TargetPath(game, steamRoots, files) : null;
            if (path == null)
                return false;
            try
            {
                return files.FileExists(path) && files.ReadAllText(path) == FileText(game, port, token);
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string Done(GameTitle game, string what)
        {
            var line = CommentaryPrompt.GameName(game) + " " + what;
            return game == GameTitle.Dota2 ? line + " " + Dota2LaunchHelp : line;
        }

        static GsiInstallResult Result(GsiInstallStatus status, string path, string message)
        {
            return new GsiInstallResult { Status = status, Path = path, Message = message };
        }

        static string Join(string root, params string[] parts)
        {
            var path = root;
            for (var i = 0; i < parts.Length; i++)
                path = Path.Combine(path, parts[i]);
            return path;
        }
    }
}
