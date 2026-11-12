using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// Stored-only zip (method 0). Unity's player profile is not referenced to
    /// <c>System.IO.Compression</c>, and a store entry is enough for an export.
    /// </summary>
    public static class LocalZip
    {
        // 2026-11-12 12:00. A zero DOS date is rejected by Expand-Archive.
        const ushort DosTime = 12 << 11;
        const ushort DosDate = ((2026 - 1980) << 9) | (11 << 5) | 12;

        public static void WriteFolder(string folder, string zipPath)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(zipPath))
                throw new ArgumentException("A folder and a zip path are required.");
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("Export folder is missing.");

            var files = new List<string>();
            Collect(folder, files);

            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            {
                var entries = new List<Entry>();
                for (var i = 0; i < files.Count; i++)
                {
                    var data = File.ReadAllBytes(files[i]);
                    var name = Relative(folder, files[i]);
                    var nameBytes = Encoding.UTF8.GetBytes(name);
                    var entry = new Entry();
                    entry.Name = nameBytes;
                    entry.Data = data;
                    entry.Crc = Crc32(data);
                    entry.Offset = (uint)output.Position;
                    WriteLocal(writer, entry);
                    entries.Add(entry);
                }

                var directoryStart = (uint)output.Position;
                for (var i = 0; i < entries.Count; i++)
                    WriteCentral(writer, entries[i]);
                var directoryEnd = (uint)output.Position;
                WriteEnd(writer, (ushort)entries.Count, directoryEnd - directoryStart, directoryStart);

                var directory = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var temp = zipPath + ".writing";
                File.WriteAllBytes(temp, output.ToArray());
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                File.Move(temp, zipPath);
            }
        }

        static void Collect(string folder, List<string> files)
        {
            if (IsReparse(folder))
                return;

            string[] found;
            try
            {
                found = Directory.GetFiles(folder);
            }
            catch (Exception)
            {
                return;
            }

            Array.Sort(found, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < found.Length; i++)
            {
                if (!IsReparse(found[i]))
                    files.Add(found[i]);
            }

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(folder);
            }
            catch (Exception)
            {
                return;
            }

            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < dirs.Length; i++)
                Collect(dirs[i], files);
        }

        static string Relative(string root, string path)
        {
            var basePath = Path.GetFullPath(root);
            var full = Path.GetFullPath(path);
            var prefix = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return full.Substring(prefix.Length).Replace('\\', '/');
            return Path.GetFileName(full);
        }

        static void WriteLocal(BinaryWriter writer, Entry entry)
        {
            writer.Write(0x04034b50);
            writer.Write((ushort)20);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(DosTime);
            writer.Write(DosDate);
            writer.Write(entry.Crc);
            writer.Write((uint)entry.Data.Length);
            writer.Write((uint)entry.Data.Length);
            writer.Write((ushort)entry.Name.Length);
            writer.Write((ushort)0);
            writer.Write(entry.Name);
            writer.Write(entry.Data);
        }

        static void WriteCentral(BinaryWriter writer, Entry entry)
        {
            writer.Write(0x02014b50);
            writer.Write((ushort)20);
            writer.Write((ushort)20);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(DosTime);
            writer.Write(DosDate);
            writer.Write(entry.Crc);
            writer.Write((uint)entry.Data.Length);
            writer.Write((uint)entry.Data.Length);
            writer.Write((ushort)entry.Name.Length);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(0);
            writer.Write(entry.Offset);
            writer.Write(entry.Name);
        }

        static void WriteEnd(BinaryWriter writer, ushort count, uint size, uint offset)
        {
            writer.Write(0x06054b50);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(count);
            writer.Write(count);
            writer.Write(size);
            writer.Write(offset);
            writer.Write((ushort)0);
        }

        static uint Crc32(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            if (data != null)
            {
                for (var i = 0; i < data.Length; i++)
                {
                    crc ^= data[i];
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var mask = (uint)-(int)(crc & 1u);
                        crc = (crc >> 1) ^ (0xEDB88320u & mask);
                    }
                }
            }

            return ~crc;
        }

        static bool IsReparse(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception)
            {
                return true;
            }
        }

        struct Entry
        {
            public byte[] Name;
            public byte[] Data;
            public uint Crc;
            public uint Offset;
        }
    }
}
