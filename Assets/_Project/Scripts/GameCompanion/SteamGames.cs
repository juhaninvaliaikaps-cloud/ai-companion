using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AICompanion.GameCompanion
{
    /// <summary>One key in a Valve KeyValues (VDF/ACF) file: a string value or a block of children.</summary>
    public sealed class VdfNode
    {
        public string Key = string.Empty;
        public string Value;
        public readonly List<VdfNode> Children = new List<VdfNode>();

        public bool IsBlock
        {
            get { return Value == null; }
        }

        /// <summary>The first child with this key, case-insensitive, or null.</summary>
        public VdfNode Child(string key)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                if (string.Equals(Children[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return Children[i];
            }

            return null;
        }

        public string ChildValue(string key)
        {
            var child = Child(key);
            return child != null && !child.IsBlock ? child.Value : null;
        }
    }

    /// <summary>
    /// A small reader for Steam's KeyValues text files: quoted keys and values, blocks in braces,
    /// <c>//</c> comments, and the escapes <c>\\</c>, <c>\"</c>, <c>\n</c>, <c>\t</c>. It never
    /// throws; a broken file gives whatever was read before the break.
    /// </summary>
    public static class VdfReader
    {
        const int MaxDepth = 16;

        public static VdfNode Parse(string text)
        {
            var root = new VdfNode();
            if (string.IsNullOrEmpty(text))
                return root;
            var i = 0;
            ReadBlock(text, ref i, root, 0);
            return root;
        }

        static void ReadBlock(string text, ref int i, VdfNode into, int depth)
        {
            while (true)
            {
                string key;
                var token = Next(text, ref i, out key);
                if (token == Token.End || token == Token.Close)
                    return;
                if (token != Token.String)
                    continue;

                string value;
                var after = Next(text, ref i, out value);
                var node = new VdfNode { Key = key };
                if (after == Token.String)
                {
                    node.Value = value;
                    into.Children.Add(node);
                }
                else if (after == Token.Open)
                {
                    into.Children.Add(node);
                    if (depth >= MaxDepth)
                        return;
                    ReadBlock(text, ref i, node, depth + 1);
                }
                else
                {
                    return;
                }
            }
        }

        enum Token
        {
            End,
            String,
            Open,
            Close,
        }

        static Token Next(string text, ref int i, out string value)
        {
            value = null;
            while (i < text.Length)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n')
                        i++;
                    continue;
                }

                if (c == '{')
                {
                    i++;
                    return Token.Open;
                }

                if (c == '}')
                {
                    i++;
                    return Token.Close;
                }

                if (c == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < text.Length && text[i] != '"')
                    {
                        if (text[i] == '\\' && i + 1 < text.Length)
                        {
                            var e = text[i + 1];
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                            i += 2;
                            continue;
                        }

                        sb.Append(text[i]);
                        i++;
                    }

                    i++;
                    value = sb.ToString();
                    return Token.String;
                }

                // An unquoted word (rare in Steam's files): up to whitespace or a brace.
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"')
                    i++;
                value = text.Substring(start, i - start);
                return Token.String;
            }

            return Token.End;
        }
    }

    /// <summary>Files on disk, behind an interface so the installer can be tested without a disk.</summary>
    public interface IGameFiles
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string text);
        void CreateDirectory(string path);
        void DeleteFile(string path);
    }

    public sealed class DiskGameFiles : IGameFiles
    {
        public bool FileExists(string path) { return File.Exists(path); }
        public bool DirectoryExists(string path) { return Directory.Exists(path); }
        public string ReadAllText(string path) { return File.ReadAllText(path); }
        public void WriteAllText(string path, string text) { File.WriteAllText(path, text, new UTF8Encoding(false)); }
        public void CreateDirectory(string path) { Directory.CreateDirectory(path); }
        public void DeleteFile(string path) { File.Delete(path); }
    }

    /// <summary>
    /// JUH-146: where Steam keeps a game. Steam lists its libraries in
    /// <c>steamapps/libraryfolders.vdf</c>, and each installed game has
    /// <c>steamapps/appmanifest_&lt;appid&gt;.acf</c> with its folder name.
    /// </summary>
    public static class SteamLibraries
    {
        public const int Cs2AppId = 730;
        public const int Dota2AppId = 570;

        public static readonly string[] DefaultRoots =
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        };

        public static int AppId(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.CounterStrike2: return Cs2AppId;
                case GameTitle.Dota2: return Dota2AppId;
                default: return 0;
            }
        }

        /// <summary>The folder name Steam uses when the manifest can't be read.</summary>
        public static string DefaultInstallDir(GameTitle game)
        {
            switch (game)
            {
                case GameTitle.CounterStrike2: return "Counter-Strike Global Offensive";
                case GameTitle.Dota2: return "dota 2 beta";
                default: return null;
            }
        }

        /// <summary>The Steam folders to try: the registry value first, then the default installs.</summary>
        public static List<string> RootCandidates(string registrySteamPath)
        {
            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(registrySteamPath))
                roots.Add(registrySteamPath.Trim());
            for (var i = 0; i < DefaultRoots.Length; i++)
            {
                if (!Contains(roots, DefaultRoots[i]))
                    roots.Add(DefaultRoots[i]);
            }

            return roots;
        }

        /// <summary>
        /// The library folders in <c>libraryfolders.vdf</c>. Reads the current format
        /// (<c>"0" { "path" "…" }</c>) and the old one (<c>"1" "D:\\SteamLibrary"</c>).
        /// </summary>
        public static List<string> LibraryFolders(string vdfText)
        {
            var folders = new List<string>();
            var root = VdfReader.Parse(vdfText);
            var block = root.Child("libraryfolders") ?? root.Child("LibraryFolders");
            if (block == null)
                return folders;
            for (var i = 0; i < block.Children.Count; i++)
            {
                var entry = block.Children[i];
                int number;
                if (!int.TryParse(entry.Key, out number))
                    continue;
                var path = entry.IsBlock ? entry.ChildValue("path") : entry.Value;
                if (!string.IsNullOrWhiteSpace(path) && !Contains(folders, path))
                    folders.Add(path.Trim());
            }

            return folders;
        }

        /// <summary>The game's folder name from its app manifest, or null. A name that could leave the library is refused.</summary>
        public static string InstallDir(string acfText)
        {
            var root = VdfReader.Parse(acfText);
            var state = root.Child("AppState");
            var dir = state != null ? state.ChildValue("installdir") : null;
            return SafeFolderName(dir) ? dir.Trim() : null;
        }

        public static bool SafeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var trimmed = name.Trim();
            if (trimmed == "." || trimmed.Contains(".."))
                return false;
            return trimmed.IndexOfAny(new[] { '/', '\\', ':' }) < 0;
        }

        /// <summary>
        /// The game's install folder, or null when it isn't installed in any library under these
        /// Steam roots.
        /// </summary>
        public static string FindGame(GameTitle game, IList<string> steamRoots, IGameFiles files)
        {
            var appId = AppId(game);
            if (appId == 0 || steamRoots == null || files == null)
                return null;

            var libraries = new List<string>();
            for (var r = 0; r < steamRoots.Count; r++)
            {
                var steam = steamRoots[r];
                if (string.IsNullOrWhiteSpace(steam) || !Safe(() => files.DirectoryExists(steam)))
                    continue;
                if (!Contains(libraries, steam))
                    libraries.Add(steam);
                var vdf = Path.Combine(Path.Combine(steam, "steamapps"), "libraryfolders.vdf");
                var text = Read(files, vdf);
                if (text == null)
                    continue;
                var listed = LibraryFolders(text);
                for (var i = 0; i < listed.Count; i++)
                {
                    if (!Contains(libraries, listed[i]))
                        libraries.Add(listed[i]);
                }
            }

            for (var i = 0; i < libraries.Count; i++)
            {
                var steamapps = Path.Combine(libraries[i], "steamapps");
                var manifest = Read(files, Path.Combine(steamapps, "appmanifest_" + appId + ".acf"));
                if (manifest == null)
                    continue;
                var dir = InstallDir(manifest) ?? DefaultInstallDir(game);
                var gameDir = Path.Combine(Path.Combine(steamapps, "common"), dir);
                if (Safe(() => files.DirectoryExists(gameDir)))
                    return gameDir;
            }

            return null;
        }

        static string Read(IGameFiles files, string path)
        {
            try
            {
                return files.FileExists(path) ? files.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool Safe(Func<bool> check)
        {
            try
            {
                return check();
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool Contains(List<string> paths, string path)
        {
            var key = Normal(path);
            for (var i = 0; i < paths.Count; i++)
            {
                if (Normal(paths[i]) == key)
                    return true;
            }

            return false;
        }

        /// <summary>Windows paths: one kind of slash, no trailing slash, case-insensitive.</summary>
        public static string Normal(string path)
        {
            return (path ?? string.Empty).Trim().Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }
}
