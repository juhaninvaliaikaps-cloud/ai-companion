using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Overlay
{
    public struct PrefRegistryValue
    {
        public string Name;
        public uint Type;
        public byte[] Data;
    }

    /// <summary>
    /// Which PlayerPrefs values to copy when productName changes.
    /// The Windows hive is copied byte for byte. Unity stores floats as an
    /// 8-byte REG_DWORD, and the Editor hashes value names, so re-encoding
    /// through PlayerPrefs would drop or corrupt them.
    /// </summary>
    public static class PrefsRenamePlan
    {
        public const string Marker = "backseat-catgirl.prefs-migrated";
        public const int MarkerValue = 1;

        public static string PlayerSubKey(string company, string product)
        {
            return "Software\\" + company + "\\" + product;
        }

        public static string EditorSubKey(string company, string product)
        {
            return "Software\\Unity\\UnityEditor\\" + company + "\\" + product;
        }

        public static string SubKey(bool editor, string company, string product)
        {
            return editor ? EditorSubKey(company, product) : PlayerSubKey(company, product);
        }

        public static PrefRegistryValue[] ValuesToCopy(PrefRegistryValue[] source, bool destinationHasMarker)
        {
            if (destinationHasMarker || source == null || source.Length == 0)
                return new PrefRegistryValue[0];

            var kept = new List<PrefRegistryValue>();
            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                if (string.IsNullOrEmpty(value.Name) || value.Name == Marker)
                    continue;
                if (value.Data == null || value.Data.Length == 0)
                    continue;
                kept.Add(value);
            }

            return kept.ToArray();
        }
    }

    /// <summary>
    /// Runs before any scene Awake so the copy lands before PlayerPrefs caches the new hive.
    /// </summary>
    static class PrefsRename
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnce()
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            var editor = false;
#if UNITY_EDITOR
            editor = true;
#endif
            var from = PrefsRenamePlan.SubKey(editor, AppName.Company, AppName.PreviousDisplay);
            var to = PrefsRenamePlan.SubKey(editor, AppName.Company, AppName.Display);
            WindowsPrefsHive.CopyIfNeeded(from, to);
#endif
        }
    }

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
    static class WindowsPrefsHive
    {
        const int ErrorSuccess = 0;
        const int ErrorFileNotFound = 2;
        const int ErrorMoreData = 234;
        const int ErrorNoMoreItems = 259;
        const uint RegDword = 4;
        const uint KeyRead = 0x20019;
        const uint KeyQueryValue = 0x0001;
        const uint KeySetValue = 0x0002;
        const uint RegOptionNonVolatile = 0;
        const int MaxBytes = 1024 * 1024;

        static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));

        public static void CopyIfNeeded(string fromSubKey, string toSubKey)
        {
            if (string.IsNullOrEmpty(fromSubKey) || string.IsNullOrEmpty(toSubKey) || fromSubKey == toSubKey)
                return;

            try
            {
                if (HasMarker(toSubKey))
                    return;

                var copy = PrefsRenamePlan.ValuesToCopy(ReadValues(fromSubKey), false);
                if (!WriteValues(toSubKey, copy))
                    return;
                WriteMarker(toSubKey);
            }
            catch (Exception)
            {
            }
        }

        static bool HasMarker(string subKey)
        {
            var key = IntPtr.Zero;
            var opened = RegOpenKeyExW(HkeyCurrentUser, subKey, 0, KeyQueryValue, out key);
            if (opened == ErrorFileNotFound || opened != ErrorSuccess || key == IntPtr.Zero)
                return false;

            try
            {
                uint type;
                uint size = 8;
                var data = new byte[8];
                var read = RegQueryValueExW(key, PrefsRenamePlan.Marker, IntPtr.Zero, out type, data, ref size);
                if (read != ErrorSuccess || type != RegDword || size < 4)
                    return false;
                return BitConverter.ToInt32(data, 0) == PrefsRenamePlan.MarkerValue;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        static PrefRegistryValue[] ReadValues(string subKey)
        {
            var key = IntPtr.Zero;
            var opened = RegOpenKeyExW(HkeyCurrentUser, subKey, 0, KeyRead, out key);
            if (opened == ErrorFileNotFound || opened != ErrorSuccess || key == IntPtr.Zero)
                return new PrefRegistryValue[0];

            try
            {
                var values = new List<PrefRegistryValue>();
                var name = new System.Text.StringBuilder(1024);
                for (uint index = 0; index < 4096; index++)
                {
                    name.Length = 0;
                    uint chars = (uint)name.Capacity;
                    uint type;
                    uint size = 0;
                    var probed = RegEnumValueW(key, index, name, ref chars, IntPtr.Zero, out type, null, ref size);
                    if (probed == ErrorNoMoreItems)
                        break;
                    if (probed != ErrorSuccess && probed != ErrorMoreData)
                        break;
                    if (size == 0 || size > MaxBytes)
                        continue;

                    var data = new byte[size];
                    chars = (uint)name.Capacity;
                    var got = size;
                    var read = RegEnumValueW(key, index, name, ref chars, IntPtr.Zero, out type, data, ref got);
                    if (read != ErrorSuccess || got == 0 || got > data.Length)
                        continue;
                    if (got < data.Length)
                    {
                        var trimmed = new byte[got];
                        Buffer.BlockCopy(data, 0, trimmed, 0, (int)got);
                        data = trimmed;
                    }

                    values.Add(new PrefRegistryValue { Name = name.ToString(), Type = type, Data = data });
                }

                return values.ToArray();
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        static bool WriteValues(string subKey, PrefRegistryValue[] values)
        {
            var key = OpenWritable(subKey);
            if (key == IntPtr.Zero)
                return false;

            try
            {
                if (values == null)
                    return true;
                for (var i = 0; i < values.Length; i++)
                {
                    var value = values[i];
                    if (value.Data == null || value.Data.Length == 0 || string.IsNullOrEmpty(value.Name))
                        continue;
                    var wrote = RegSetValueExW(key, value.Name, 0, value.Type, value.Data, (uint)value.Data.Length);
                    if (wrote != ErrorSuccess)
                        return false;
                }

                return true;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        static void WriteMarker(string subKey)
        {
            var key = OpenWritable(subKey);
            if (key == IntPtr.Zero)
                return;

            try
            {
                var data = BitConverter.GetBytes(PrefsRenamePlan.MarkerValue);
                RegSetValueExW(key, PrefsRenamePlan.Marker, 0, RegDword, data, (uint)data.Length);
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        static IntPtr OpenWritable(string subKey)
        {
            uint disposition;
            IntPtr key;
            var created = RegCreateKeyExW(
                HkeyCurrentUser,
                subKey,
                0,
                null,
                RegOptionNonVolatile,
                KeySetValue | KeyQueryValue,
                IntPtr.Zero,
                out key,
                out disposition);
            if (created != ErrorSuccess)
                return IntPtr.Zero;
            return key;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegOpenKeyExW(IntPtr hKey, string subKey, uint options, uint samDesired, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegCreateKeyExW(
            IntPtr hKey,
            string subKey,
            uint reserved,
            string className,
            uint options,
            uint samDesired,
            IntPtr securityAttributes,
            out IntPtr result,
            out uint disposition);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegEnumValueW(
            IntPtr hKey,
            uint index,
            System.Text.StringBuilder name,
            ref uint nameChars,
            IntPtr reserved,
            out uint type,
            byte[] data,
            ref uint dataBytes);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegQueryValueExW(
            IntPtr hKey,
            string valueName,
            IntPtr reserved,
            out uint type,
            byte[] data,
            ref uint dataBytes);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegSetValueExW(
            IntPtr hKey,
            string valueName,
            int reserved,
            uint type,
            byte[] data,
            uint dataBytes);

        [DllImport("advapi32.dll", ExactSpelling = true)]
        static extern int RegCloseKey(IntPtr hKey);
    }
#endif
}
